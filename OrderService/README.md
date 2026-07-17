# OrderService

OrderService owns the durable order record and coordinates checkout across CartService, CustomerService, and ProductService. It never reads another service's database and never stores payment-card data. All cross-service work uses HTTP through `IHttpClientFactory` and local contract DTOs.

## Architecture and boundaries

The service uses ASP.NET Core 10 controller APIs, EF Core 10, and its own PostgreSQL database. An order is an immutable historical snapshot after confirmation: product identity/name/SKU/image/price, quantities, calculated totals, and shipping/billing addresses are copied at creation. Customer, cart, and product IDs are external identifiers, not foreign keys.

There is no distributed transaction. Checkout is a small synchronous saga whose durable steps are recorded locally:

1. Claim the create `OperationId` and SHA-256 `RequestHash`.
2. Read the cart and prepare checkout using the cart's current concurrency token.
3. validate customer eligibility and select owned default/requested addresses.
4. Recalculate totals and save the complete local snapshot in a short transaction.
5. Decrease each product in deterministic product-ID order using a stable operation ID.
6. Complete the cart using its checkout token and the original checkout operation ID.
7. Confirm the order and mark the create operation successful.

External calls are never made inside a database transaction. HTTP resilience uses bounded exponential retry, timeout, and circuit breaker. POST retries are safe because the same stable operation IDs are reused.

## State and inventory lifecycle

`OrderStatus` is `PendingConfirmation`, `InventoryProcessing`, `CartCompletionPending`, `Confirmed`, `Cancelled`, `Failed`, or `Completed`. State changes are validated centrally and appended to `OrderStatusHistories`; invalid transitions return 409. Cancellation from `PendingConfirmation` is an explicit endpoint exception required by the cancellation contract.

Each item has `NotProcessed`, `Decreased`, `RestorePending`, `Restored`, or `Failed` inventory state. Its external IDs are deterministic:

```text
order:{orderId}:product:{productId}:decrease
order:{orderId}:product:{productId}:restore
```

If a product rejects a decrease, already-decreased items are restored, Cart checkout is cancelled when possible, and the order is retained as `Failed`. A temporary restore failure becomes `RestorePending` and is recovered later. A timeout after all stock decreases leaves the order in `CartCompletionPending`; stock is not immediately restored because CartService may already have committed checkout.

## Idempotency and concurrency

Create, cancel, complete, and retry commands require a globally unique `OperationId`. `OrderOperations` stores the operation type, SHA-256 hash of material non-personal request values, outcome, and optional order ID. Reusing an ID with the same hash returns the current/prior result; different data returns 409. Request bodies, addresses, email, phone, stack traces, and response bodies are not stored in operations or logs.

`Order`, `OrderItem`, and `OrderOperation` use GUID optimistic concurrency tokens. The database also enforces unique order numbers, operation IDs, per-order products, inventory operation IDs, and a filtered unique cart index for active/successful states. A stale command token or EF concurrency collision returns 409.

## Recovery

`OrderRecoveryBackgroundService` pages recoverable IDs, respects `NextRetryAtUtc`, caps attempts, applies exponential backoff, and uses an in-process claim plus EF optimistic concurrency. It resumes missing stock decreases, retries Cart completion with the same identity, and completes `RestorePending` compensation. One failed cycle does not stop the host. Configure `OrderRecovery` to control interval, batch size, maximum attempts, and delays.

## HTTP API

Base route: `/api/v1/orders` (expected gateway route: `/api/orders`).

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/v1/orders` | Create from a prepared cart; 201 completed, 200 idempotent, or 202 durable/in progress |
| GET | `/api/v1/orders` | Search, filter, sort, and paginate orders |
| GET | `/api/v1/orders/{orderId}` | Full order snapshot and history |
| GET | `/api/v1/orders/by-number/{orderNumber}` | Case-insensitive order-number lookup |
| GET | `/api/v1/orders/customer/{customerId}` | Customer order history without an external call |
| GET | `/api/v1/orders/{orderId}/status` | Compact processing status |
| POST | `/api/v1/orders/{orderId}/cancel` | Idempotent cancellation and inventory restoration |
| POST | `/api/v1/orders/{orderId}/complete` | Move a confirmed order to completed |
| POST | `/api/v1/orders/{orderId}/retry` | Explicit administrative retry of a recoverable order |

List parameters are `pageNumber`, `pageSize`, `search`, `customerId`, `cartId`, `status`, `createdFromUtc`, `createdToUtc`, `minTotal`, `maxTotal`, `sortBy`, and `sortDirection`. Page size is limited to 100. `CreateOrderRequest` accepts only cart/operation/address selection; it never accepts customer ID, products, prices, or totals.

Example:

```bash
curl -i http://localhost:5004/api/v1/orders \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: checkout-001" \
  -d '{"cartId":"00000000-0000-0000-0000-000000000000","operationId":"create-001","acceptPriceChanges":false,"shippingAddressId":null,"billingAddressId":null,"useShippingAddressForBilling":false}'
```

`202 Accepted` means the local order is durable and recovery is scheduled. The response and `Location` header identify the order to poll. Errors use RFC-style Problem Details with `traceId`, `correlationId`, validation errors, and safe failure metadata. Status mapping is 400 validation, 404 resource missing, 409 business/idempotency/concurrency conflict, 502 invalid dependency response, 503 unavailable, 504 timeout, and 500 unexpected.

## External contracts

- CartService: `GET /api/v1/carts/{id}` plus `/checkout/prepare`, `/checkout/complete`, and `/checkout/cancel`. The actual CartService contract requires its current concurrency token during prepare and requires the original checkout operation ID during complete/cancel.
- CustomerService: eligibility, customer details, address list, and individual address endpoints. OrderService verifies `CanPlaceOrder` and address ownership, then stores snapshots.
- ProductService: availability and idempotent stock decrease/increase endpoints.

`X-Correlation-ID` is accepted or generated, returned, placed in the logging scope, written to status history, and propagated to every dependency. Logs contain IDs and workflow outcomes, not full addresses, emails, phones, secrets, connection strings, or complete bodies.

## Database and migrations

Set `ConnectionStrings__OrderDatabase`. The migration `InitialOrderServiceSchema` creates `Orders`, `OrderItems`, `OrderAddressSnapshots`, `OrderStatusHistories`, and `OrderOperations` with PostgreSQL `numeric(18,2)` money columns and recovery/query indexes.

```powershell
dotnet ef migrations list --project OrderService --startup-project OrderService
dotnet ef database update --project OrderService --startup-project OrderService
```

Startup migration is off by default. Explicitly set `Database__ApplyMigrationsOnStartup=true` for controlled local/container environments. The database starts empty; there is no cross-service seed data.

## Local development and tests

Run ProductService on 5001, CustomerService on 5002, CartService on 5003, PostgreSQL on 5435, and then:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project OrderService
```

Swagger UI is enabled only in Development at `/swagger`. Health endpoints are:

- `/health/live`: host liveness only.
- `/health/ready`: OrderService database; dependency inclusion is configurable.
- `/health/dependencies`: ProductService, CustomerService, and CartService.

Health endpoints bypass rate limiting. CORS origins and the fixed-window limiter are configuration-driven.

## Docker

Copy `.env.example` values into your own untracked environment and change the development password. Then run:

```powershell
docker compose -f docker-compose.order.yml config
docker compose -f docker-compose.order.yml build
docker compose -f docker-compose.order.yml up -d
```

The container is `orderservice`, listens on 8080, maps to 5004 by default, runs as the .NET non-root user, and connects to independent `orderdb`. It joins the named `ecommerce-network` and expects the other service container names.

## Future integrations

PaymentService and ShippingService are intentionally absent. Future payment states can be introduced through the centralized transition policy and application-service boundary without storing card data. Shipping should consume the immutable address/item snapshot. API Gateway owns the external `/api/orders` route; it is not implemented here.
