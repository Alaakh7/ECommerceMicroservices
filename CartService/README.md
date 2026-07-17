# CartService

CartService owns shopping-cart state for the e-commerce platform. It is a controller-based ASP.NET Core 10 Web API backed by its own PostgreSQL database. It communicates with ProductService and CustomerService only through HTTP; it has no project references or database access to either service.

## Responsibility and boundaries

CartService creates and retrieves carts, stores display snapshots of products, maintains totals, refreshes and validates carts, expires abandoned work, and implements an idempotent checkout lock. ProductService remains the source of truth for product identity, active state, price, and availability. CustomerService remains the source of truth for customer existence and eligibility.

Adding an item does **not** reserve or reduce inventory. A future OrderService should prepare the cart, create the order, ask ProductService to reduce stock with its own idempotency key, and then complete or cancel the cart checkout. No distributed transaction is used.

CartService does not authenticate users, issue tokens, process payments, create orders, or maintain customer/product master data.

## Data model

`Cart` stores the customer ID, status, currency, totals, expiration, checkout lock data, completed order ID, timestamps, and a GUID optimistic-concurrency token. `CartItem` stores a product ID plus the SKU, name, image, price, and product version observed when the cart was last updated. It also stores quantity, line total, timestamps, and its own concurrency token.

`CartStatus` values are:

- `Active`: items can be modified.
- `CheckoutPending`: temporarily locked for an OrderService operation.
- `CheckedOut`: immutable history linked to an order.
- `Abandoned`: manually closed history.
- `Expired`: closed by expiration processing.

The PostgreSQL schema has a partial unique index (`UX_Carts_Customer_Open`) on `CustomerId` for `Active` and `CheckoutPending` rows, so a customer has at most one open cart. Historical carts are not constrained. `CartItems` has a unique index on `(CartId, ProductId)`.

Totals are always calculated internally:

```text
LineTotal        = round(UnitPrice * Quantity, 2)
Subtotal         = sum(LineTotal)
TotalQuantity    = sum(Quantity)
DistinctItemCount = count(items)
```

## External integrations

Customer eligibility uses:

```http
GET /api/v1/customers/{customerId}/eligibility
```

Product data and availability use:

```http
GET  /api/v1/products/{productId}
GET  /api/v1/products/{productId}/availability?quantity=5
POST /api/v1/products/availability/batch
```

Typed clients are created by `IHttpClientFactory`. Base URLs and timeouts come from configuration. `X-Correlation-ID` is propagated to both dependencies. The standard .NET HTTP resilience pipeline provides exponential-backoff retries for safe methods only, a circuit breaker, and timeout handling. A real 404 is kept distinct from malformed responses (502), unavailability (503), and timeouts (504). POST batch validation is not retried automatically.

## Cart workflows

Creating a cart first verifies `canCreateCart`. An existing unexpired active cart is returned with `wasCreated: false`. An expired checkout lock is released; a live checkout lock produces 409. Currency currently must match `CartRules:DefaultCurrency`.

Refresh calls the batch availability API and retrieves current product snapshots. Names, SKUs, images, and product version fields are refreshed. Prices change only when `updatePrices` is true. Missing or unavailable products are reported and are never silently removed.

Validate performs a read-only customer and product check. It returns customer eligibility, per-item results, availability issues, stored versus current totals, and price changes. It neither changes state nor reduces inventory.

Prepare checkout requires a non-empty active cart, a checkout-eligible customer with a default shipping address, active products, sufficient quantities, and accepted price changes. It transitions the cart to `CheckoutPending`, stores the operation ID and a generated token, and returns an immutable item snapshot. Repeating the same operation for the same cart returns the same token; reusing it for another cart returns 409.

Complete verifies the token, operation, and lock expiry, sets `CheckedOut`, stores the order ID, retains items and the operation ID for audit, and clears the secret token. Repeating completion for the same operation/order is successful. Cancel verifies the checkout identity, clears lock data, and returns the cart to `Active` unless the cart itself has expired.

## API

All routes are under `/api/v1/carts`.

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/` | Create or return the customer's open cart |
| GET | `/{cartId}` | Get a cart and its stored item snapshots |
| GET | `/customer/{customerId}/active` | Get the active, unexpired cart |
| GET | `/customer/{customerId}` | Paginated cart history |
| POST | `/{cartId}/items` | Add a product or increase its quantity |
| PUT | `/{cartId}/items/{productId}` | Set product quantity |
| DELETE | `/{cartId}/items/{productId}` | Remove one product |
| DELETE | `/{cartId}/items` | Clear all items |
| POST | `/{cartId}/refresh` | Refresh product snapshots and optionally prices |
| POST | `/{cartId}/validate` | Read-only final validation |
| POST | `/{cartId}/checkout/prepare` | Validate and lock the cart idempotently |
| POST | `/{cartId}/checkout/complete` | Mark a prepared cart checked out |
| POST | `/{cartId}/checkout/cancel` | Release a prepared cart |
| POST | `/{cartId}/abandon` | Close an active cart |

History query parameters are `pageNumber`, `pageSize` (1-100), `status`, `createdFromUtc`, `createdToUtc`, and `sortDirection` (`asc`/`desc`). The default order is newest first.

Mutating calls require the current GUID concurrency token where documented. A stale token returns RFC 7807 Problem Details with HTTP 409. Validation errors return 400; missing carts, customers, and products return 404; state, eligibility, stock, and price conflicts return 409.

Every error includes `type`, `title`, `status`, `detail`, `instance`, `traceId`, and `correlationId`; validation failures also include `errors`. Production responses do not expose stack traces.

## Configuration

Important environment-variable overrides include:

```text
ConnectionStrings__CartDatabase
Database__ApplyMigrationsOnStartup
Services__ProductService__BaseUrl
Services__CustomerService__BaseUrl
CartRules__DefaultCurrency
CartExpiration__Enabled
```

`CartRules` controls maximum distinct items, maximum quantity per item, cart lifetime, checkout lock duration, and whether modification refreshes expiration. `CartExpiration` controls the background interval and batch size. The background service uses a new DI scope per cycle and processes bounded batches.

## Local execution and migrations

Set a PostgreSQL connection string, then run:

```powershell
dotnet restore
dotnet ef database update --project CartService --startup-project CartService
dotnet run --project CartService
```

Migrations are applied at startup only when `Database:ApplyMigrationsOnStartup=true`. Startup migration retries are bounded and disabled by default. The initial migration is `InitialCartServiceSchema`. No fake cart seed data is created.

Swagger UI is available in Development at `/swagger`. Health endpoints are:

- `/health/live`: process liveness only.
- `/health/ready`: CartService database, plus dependencies only when configured.
- `/health/dependencies`: ProductService and CustomerService.

Health endpoints bypass rate limiting. CORS allows only configured origins. The global fixed-window rate limit defaults to 100 requests per 60 seconds.

Example:

```bash
curl -X POST http://localhost:5003/api/v1/carts \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: demo-001" \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","currency":"USD"}'
```

## Docker

Copy `.env.example` to an untracked `.env`, change the password, and run from the solution directory:

```powershell
docker compose -f docker-compose.cart.yml config
docker compose -f docker-compose.cart.yml build
docker compose -f docker-compose.cart.yml up -d
```

The compose file creates only CartService and its independent `cartdb`; it does not duplicate ProductService or CustomerService. All services must join `ecommerce-network`. CartService listens on `http://localhost:5003` and uses `http://productservice:8080` and `http://customerservice:8080` inside Docker.

## Future platform integration

A future API gateway can expose `/api/carts` and forward it to `http://cartservice:8080/api/v1/carts`. OrderService should supply stable operation IDs and follow prepare/create-order/decrease-stock/complete, calling cancel when order creation fails. Retries are safe because checkout prepare and complete are idempotent for the documented keys.
