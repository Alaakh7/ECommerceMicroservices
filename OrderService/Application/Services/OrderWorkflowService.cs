using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderService.Application.DTOs.ExternalServices;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces;
using OrderService.Application.Models;
using OrderService.Common.Exceptions;
using OrderService.Common.Middleware;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Data;

namespace OrderService.Application.Services;

public sealed class OrderWorkflowService(
    OrderDbContext db,
    IOrderRepository orders,
    IOrderOperationRepository operations,
    ICartServiceClient carts,
    ICustomerServiceClient customers,
    IProductServiceClient products,
    OrderTotalsCalculator totalsCalculator,
    OrderNumberGenerator numberGenerator,
    OrderStatusTransitionValidator transitions,
    RequestHashService hashes,
    IOptions<OrderRulesOptions> rulesOptions,
    IOptions<OrderRecoveryOptions> recoveryOptions,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor,
    ILogger<OrderWorkflowService> logger) : IOrderWorkflowService
{
    private readonly OrderRulesOptions rules = rulesOptions.Value;
    private readonly OrderRecoveryOptions recovery = recoveryOptions.Value;

    public async Task<OperationResult<CreateOrderResponse>> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var operationId = request.OperationId.Trim();
        var hash = hashes.ForCreate(request);
        var operation = await GetOrCreateOperationAsync(operationId, OrderOperationType.Create, hash, cancellationToken);
        if (operation.OrderId.HasValue)
        {
            var existing = await RequireOrderAsync(operation.OrderId.Value, false, cancellationToken);
            var status = existing.Status is OrderStatus.PendingConfirmation or OrderStatus.InventoryProcessing or OrderStatus.CartCompletionPending ? 202 : 200;
            logger.LogInformation("Returning idempotent create operation {OperationId} for order {OrderId}", operationId, existing.Id);
            return new(OrderMapper.ToCreate(existing, status == 202), status, true);
        }

        logger.LogInformation("Preparing cart {CartId} for create operation {OperationId}", request.CartId, operationId);
        CartExternalResponse cart;
        CartPrepareCheckoutExternalResponse prepared;
        try
        {
            cart = await carts.GetCartAsync(request.CartId, cancellationToken);
            prepared = await carts.PrepareCheckoutAsync(request.CartId, new(operationId, request.AcceptPriceChanges, cart.ConcurrencyToken), cancellationToken);
        }
        catch (Exception exception) when (exception is ExternalServiceTimeoutException or ExternalServiceUnavailableException)
        {
            operation.Status = OrderOperationStatus.Failed; operation.ResultStatusCode = 503; operation.ErrorCode = "cart_unavailable"; operation.CompletedAtUtc = Now; operation.ConcurrencyToken = Guid.NewGuid();
            await operations.SaveChangesAsync(cancellationToken);
            throw;
        }

        if (prepared.Items.Count == 0) throw await FailBeforeSnapshotAsync(operation, "cart_empty", "The cart is empty.", cancellationToken);
        if (prepared.Items.Count > rules.MaximumItemsPerOrder || prepared.Items.Any(x => x.Quantity > rules.MaximumQuantityPerItem))
            throw await FailBeforeSnapshotAsync(operation, "order_limits_exceeded", "The cart exceeds configured order limits.", cancellationToken);

        var eligibility = await customers.GetEligibilityAsync(prepared.CustomerId, cancellationToken);
        if (!eligibility.Exists) throw await FailBeforeSnapshotAsync(operation, "customer_not_found", "The customer was not found.", cancellationToken, true);
        if (!eligibility.CanPlaceOrder) throw await FailBeforeSnapshotAsync(operation, "customer_not_eligible", "The customer cannot place orders.", cancellationToken);
        var addresses = await customers.GetCustomerAddressesAsync(prepared.CustomerId, cancellationToken);
        var shipping = SelectAddress(addresses, request.ShippingAddressId, true, prepared.CustomerId);
        CustomerAddressExternalResponse billing;
        if (request.UseShippingAddressForBilling) billing = shipping;
        else billing = SelectAddress(addresses, request.BillingAddressId, false, prepared.CustomerId);

        var totals = totalsCalculator.Calculate(prepared.Items);
        if (totals.Subtotal != prepared.Subtotal || totals.TotalQuantity != prepared.TotalQuantity || prepared.Items.Any(x => x.LineTotal != decimal.Round(x.UnitPrice * x.Quantity, 2)))
            throw await FailBeforeSnapshotAsync(operation, "cart_totals_mismatch", "Cart totals do not match the item snapshot.", cancellationToken);

        var order = BuildOrder(operationId, prepared, cart, shipping, billing, totals);
        operation.OrderId = order.Id; operation.ConcurrencyToken = Guid.NewGuid();
        var snapshotSaved = false;
        for (var attempt = 1; attempt <= 3 && !snapshotSaved; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await orders.AddAsync(order, cancellationToken);
                await orders.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                snapshotSaved = true;
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                var existing = await orders.GetByCartIdAsync(request.CartId, cancellationToken);
                if (existing is not null) throw new DuplicateOrderException($"Cart '{request.CartId}' already has order '{existing.OrderNumber}'.");
                if (attempt == 3) throw new ConflictException("The order could not be created after retrying a uniqueness conflict.");
                order.OrderNumber = numberGenerator.Generate();
                operation = await operations.GetByOperationIdAsync(operationId, true, cancellationToken) ?? throw new ConflictException("The create operation could not be reloaded after a uniqueness conflict.");
                operation.OrderId = order.Id; operation.ConcurrencyToken = Guid.NewGuid();
            }
        }
        logger.LogInformation("Created order snapshot {OrderId} {OrderNumber} for cart {CartId}", order.Id, order.OrderNumber, order.CartId);

        try
        {
            await AdvanceAsync(order, "System", cancellationToken);
        }
        catch (Exception exception) when (exception is ExternalServiceTimeoutException or ExternalServiceUnavailableException)
        {
            await ScheduleRetryAsync(order, "external_service_temporary_failure", cancellationToken);
            return new(OrderMapper.ToCreate(order, true), 202);
        }
        return new(OrderMapper.ToCreate(order, false), order.Status == OrderStatus.Confirmed ? 201 : 202);
    }

    public async Task<OperationResult<OrderDetailsResponse>> RetryAsync(Guid orderId, RetryOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await RequireOrderAsync(orderId, true, cancellationToken);
        if (order.Status is OrderStatus.Confirmed or OrderStatus.Cancelled or OrderStatus.Completed) throw new InvalidOrderStateException($"Order in {order.Status} state cannot be retried.");
        var operation = await GetOrCreateOperationAsync(request.OperationId.Trim(), OrderOperationType.Retry, hashes.ForRetry(orderId, request), cancellationToken);
        if (operation.OrderId.HasValue)
        {
            if (operation.OrderId != orderId) throw new ConflictException("OperationId belongs to another order.");
            return new(OrderMapper.ToDetails(order), order.NextRetryAtUtc.HasValue ? 202 : 200, true);
        }
        operation.OrderId = order.Id;
        try { await RecoverAsync(order.Id, "Administrator", cancellationToken); operation.Status = OrderOperationStatus.Succeeded; operation.ResultStatusCode = 200; operation.CompletedAtUtc = Now; }
        catch (Exception exception) when (exception is ExternalServiceTimeoutException or ExternalServiceUnavailableException) { operation.ResultStatusCode = 202; await ScheduleRetryAsync(order, "retry_temporary_failure", cancellationToken); }
        operation.ConcurrencyToken = Guid.NewGuid(); await operations.SaveChangesAsync(cancellationToken);
        order = await RequireOrderAsync(orderId, false, cancellationToken);
        return new(OrderMapper.ToDetails(order), operation.ResultStatusCode ?? 202);
    }

    public async Task RecoverAsync(Guid orderId, string changedBy, CancellationToken cancellationToken)
    {
        var order = await RequireOrderAsync(orderId, true, cancellationToken);
        order.UpdatedAtUtc = Now; order.ConcurrencyToken = Guid.NewGuid();
        await SaveAsync(cancellationToken);
        if (order.Items.Any(x => x.InventoryStatus == OrderItemInventoryStatus.RestorePending))
        {
            await RestoreInventoryAsync(order, cancellationToken);
            if (order.Items.All(x => x.InventoryStatus is OrderItemInventoryStatus.Restored or OrderItemInventoryStatus.NotProcessed or OrderItemInventoryStatus.Failed) && !string.IsNullOrWhiteSpace(order.CancellationOperationId))
                await TransitionAsync(order, OrderStatus.Cancelled, "Inventory restoration completed.", changedBy, cancellationToken, allowCancellationFromPending: true);
            return;
        }
        if (order.Status == OrderStatus.Failed) return;
        await AdvanceAsync(order, changedBy, cancellationToken);
    }

    private async Task AdvanceAsync(Order order, string changedBy, CancellationToken cancellationToken)
    {
        if (order.Status == OrderStatus.PendingConfirmation)
            await TransitionAsync(order, OrderStatus.InventoryProcessing, "Inventory processing started.", changedBy, cancellationToken);
        if (order.Status == OrderStatus.InventoryProcessing)
        {
            foreach (var item in order.Items.OrderBy(x => x.ProductId))
            {
                if (item.InventoryStatus == OrderItemInventoryStatus.Decreased) continue;
                try
                {
                    await products.DecreaseStockAsync(item.ProductId, new(item.InventoryDecreaseOperationId, item.Quantity, "Order confirmed"), cancellationToken);
                    item.InventoryStatus = OrderItemInventoryStatus.Decreased; item.StockDecreasedAtUtc = Now; item.ConcurrencyToken = Guid.NewGuid(); order.UpdatedAtUtc = Now; order.ConcurrencyToken = Guid.NewGuid();
                    await SaveAsync(cancellationToken);
                    logger.LogInformation("Decreased stock for order {OrderId}, product {ProductId}, operation {OperationId}", order.Id, item.ProductId, item.InventoryDecreaseOperationId);
                }
                catch (Exception exception) when (exception is ExternalConflictException or ExternalResourceNotFoundException)
                {
                    item.InventoryStatus = OrderItemInventoryStatus.Failed; item.ConcurrencyToken = Guid.NewGuid(); await SaveAsync(cancellationToken);
                    await CompensateFailureAsync(order, "inventory_rejected", exception.Message, cancellationToken);
                    throw new InsufficientStockException(item.ProductId, exception.Message);
                }
            }
            await TransitionAsync(order, OrderStatus.CartCompletionPending, "Inventory decreased for every item.", changedBy, cancellationToken);
        }
        if (order.Status == OrderStatus.CartCompletionPending)
        {
            try
            {
                await carts.CompleteCheckoutAsync(order.CartId, new(order.CartCheckoutToken!.Value, order.CartCheckoutOperationId!, order.Id), cancellationToken);
                await TransitionAsync(order, OrderStatus.Confirmed, "Cart checkout completed.", changedBy, cancellationToken);
                order.ConfirmedAtUtc = Now; order.NextRetryAtUtc = null; order.FailureCode = null; order.FailureMessage = null; order.ConcurrencyToken = Guid.NewGuid();
                var createOperation = await operations.GetByOperationIdAsync(order.CreateOperationId, true, cancellationToken);
                if (createOperation is not null) { createOperation.Status = OrderOperationStatus.Succeeded; createOperation.ResultStatusCode = 201; createOperation.CompletedAtUtc = Now; createOperation.ErrorCode = null; createOperation.ConcurrencyToken = Guid.NewGuid(); }
                await SaveAsync(cancellationToken);
                logger.LogInformation("Confirmed order {OrderId} {OrderNumber}", order.Id, order.OrderNumber);
            }
            catch (ExternalConflictException exception)
            {
                await CompensateFailureAsync(order, "cart_completion_rejected", exception.Message, cancellationToken);
                throw new CartNotReadyException(exception.Message);
            }
        }
    }

    private async Task CompensateFailureAsync(Order order, string code, string detail, CancellationToken cancellationToken)
    {
        logger.LogWarning("Compensating order {OrderId} after {FailureCode}", order.Id, code);
        await RestoreInventoryAsync(order, cancellationToken);
        try
        {
            if (order.CartCheckoutToken.HasValue && order.Status != OrderStatus.Confirmed)
                await carts.CancelCheckoutAsync(order.CartId, new(order.CartCheckoutToken.Value, order.CartCheckoutOperationId!, $"Order failed: {code}"), cancellationToken);
        }
        catch (Exception exception) when (exception is ExternalServiceTimeoutException or ExternalServiceUnavailableException) { await ScheduleRetryAsync(order, "cart_cancel_pending", cancellationToken); }
        order.FailureCode = code; order.FailureMessage = Truncate(detail, 500); order.FailedAtUtc = Now;
        if (order.Status != OrderStatus.Failed) await TransitionAsync(order, OrderStatus.Failed, detail, "System", cancellationToken);
        var operation = await operations.GetByOperationIdAsync(order.CreateOperationId, true, cancellationToken);
        if (operation is not null) { operation.Status = OrderOperationStatus.Failed; operation.ResultStatusCode = 409; operation.ErrorCode = code; operation.CompletedAtUtc = Now; operation.ConcurrencyToken = Guid.NewGuid(); await SaveAsync(cancellationToken); }
    }

    internal async Task RestoreInventoryAsync(Order order, CancellationToken cancellationToken)
    {
        foreach (var item in order.Items.Where(x => x.InventoryStatus is OrderItemInventoryStatus.Decreased or OrderItemInventoryStatus.RestorePending).OrderBy(x => x.ProductId))
        {
            try
            {
                await products.IncreaseStockAsync(item.ProductId, new(item.InventoryRestoreOperationId, item.Quantity, "Order cancelled or creation compensated"), cancellationToken);
                item.InventoryStatus = OrderItemInventoryStatus.Restored; item.StockRestoredAtUtc = Now; item.ConcurrencyToken = Guid.NewGuid(); await SaveAsync(cancellationToken);
                logger.LogInformation("Restored stock for order {OrderId}, product {ProductId}, operation {OperationId}", order.Id, item.ProductId, item.InventoryRestoreOperationId);
            }
            catch (Exception exception) when (exception is ExternalServiceTimeoutException or ExternalServiceUnavailableException)
            {
                item.InventoryStatus = OrderItemInventoryStatus.RestorePending; item.ConcurrencyToken = Guid.NewGuid(); await ScheduleRetryAsync(order, "inventory_restore_pending", cancellationToken);
            }
        }
    }

    private Order BuildOrder(string operationId, CartPrepareCheckoutExternalResponse prepared, CartExternalResponse cart, CustomerAddressExternalResponse shipping, CustomerAddressExternalResponse billing, OrderTotals totals)
    {
        var id = Guid.NewGuid(); var imageByProduct = cart.Items.ToDictionary(x => x.ProductId, x => x.ImageUrl);
        var order = new Order
        {
            Id = id, OrderNumber = numberGenerator.Generate(), CustomerId = prepared.CustomerId, CartId = prepared.CartId, Currency = prepared.Currency.ToUpperInvariant(), Status = OrderStatus.PendingConfirmation,
            Subtotal = totals.Subtotal, DiscountAmount = totals.DiscountAmount, TaxAmount = totals.TaxAmount, ShippingAmount = totals.ShippingAmount, TotalAmount = totals.TotalAmount,
            TotalQuantity = totals.TotalQuantity, DistinctItemCount = totals.DistinctItemCount, CreateOperationId = operationId, CartCheckoutToken = prepared.CheckoutToken,
            CartCheckoutOperationId = prepared.CheckoutOperationId, CreatedAtUtc = Now, ConcurrencyToken = Guid.NewGuid()
        };
        order.Items = prepared.Items.Select(x => new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = id, ProductId = x.ProductId, Sku = x.Sku, ProductName = x.ProductName, ImageUrl = imageByProduct.GetValueOrDefault(x.ProductId),
            UnitPrice = x.UnitPrice, Quantity = x.Quantity, LineTotal = decimal.Round(x.UnitPrice * x.Quantity, 2), InventoryDecreaseOperationId = $"order:{id}:product:{x.ProductId}:decrease",
            InventoryRestoreOperationId = $"order:{id}:product:{x.ProductId}:restore", InventoryStatus = OrderItemInventoryStatus.NotProcessed, CreatedAtUtc = Now, ConcurrencyToken = Guid.NewGuid()
        }).ToList();
        order.Addresses = [ToAddress(id, shipping, OrderAddressType.Shipping), ToAddress(id, billing, OrderAddressType.Billing)];
        order.StatusHistory = [new() { Id = Guid.NewGuid(), OrderId = id, NewStatus = OrderStatus.PendingConfirmation, ChangedBy = "System", CreatedAtUtc = Now, CorrelationId = CorrelationId }];
        return order;
    }

    private OrderAddressSnapshot ToAddress(Guid orderId, CustomerAddressExternalResponse x, OrderAddressType type) => new() { Id = Guid.NewGuid(), OrderId = orderId, AddressType = type, SourceAddressId = x.Id, RecipientName = x.RecipientName, AddressLine1 = x.AddressLine1, AddressLine2 = x.AddressLine2, City = x.City, StateOrProvince = x.StateOrProvince, PostalCode = x.PostalCode, CountryCode = x.CountryCode.ToUpperInvariant(), PhoneNumber = x.PhoneNumber, CreatedAtUtc = Now };
    private static CustomerAddressExternalResponse SelectAddress(IReadOnlyList<CustomerAddressExternalResponse> addresses, Guid? requestedId, bool shipping, Guid customerId)
    {
        var address = requestedId.HasValue ? addresses.FirstOrDefault(x => x.Id == requestedId) : addresses.FirstOrDefault(x => shipping ? x.IsDefaultShipping : x.IsDefaultBilling);
        if (address is null) throw new InvalidCustomerAddressException($"A valid {(shipping ? "shipping" : "billing")} address is required.");
        if (address.CustomerId != customerId) throw new InvalidCustomerAddressException("The selected address does not belong to the customer.");
        return address;
    }

    internal async Task TransitionAsync(Order order, OrderStatus next, string? reason, string changedBy, CancellationToken cancellationToken, bool allowCancellationFromPending = false)
    {
        if (order.Status == next) return;
        if (!(allowCancellationFromPending && order.Status == OrderStatus.PendingConfirmation && next == OrderStatus.Cancelled)) transitions.EnsureAllowed(order.Status, next);
        var previous = order.Status; order.Status = next; order.UpdatedAtUtc = Now; order.ConcurrencyToken = Guid.NewGuid();
        var history = new OrderStatusHistory { Id = Guid.NewGuid(), OrderId = order.Id, PreviousStatus = previous, NewStatus = next, Reason = Truncate(reason, 500), ChangedBy = changedBy, CreatedAtUtc = Now, CorrelationId = CorrelationId };
        order.StatusHistory.Add(history);
        db.OrderStatusHistories.Add(history);
        await SaveAsync(cancellationToken);
    }

    internal async Task ScheduleRetryAsync(Order order, string code, CancellationToken cancellationToken)
    {
        order.RetryCount++; order.LastRetryAtUtc = Now; var seconds = recovery.InitialRetryDelaySeconds * Math.Pow(2, Math.Max(0, order.RetryCount - 1));
        order.NextRetryAtUtc = Now.AddSeconds(Math.Min(seconds, recovery.MaximumRetryDelayMinutes * 60)); order.FailureCode = code; order.UpdatedAtUtc = Now; order.ConcurrencyToken = Guid.NewGuid();
        await SaveAsync(cancellationToken);
    }

    private async Task<OrderOperation> GetOrCreateOperationAsync(string operationId, OrderOperationType type, string hash, CancellationToken cancellationToken)
    {
        var existing = await operations.GetByOperationIdAsync(operationId, true, cancellationToken);
        if (existing is not null)
        {
            if (existing.OperationType != type || !string.Equals(existing.RequestHash, hash, StringComparison.Ordinal)) throw new ConflictException($"OperationId '{operationId}' was already used with different data.");
            return existing;
        }
        var operation = new OrderOperation { Id = Guid.NewGuid(), OperationId = operationId, OperationType = type, RequestHash = hash, Status = OrderOperationStatus.InProgress, CreatedAtUtc = Now, ConcurrencyToken = Guid.NewGuid() };
        await operations.AddAsync(operation, cancellationToken);
        try { await operations.SaveChangesAsync(cancellationToken); return operation; }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            existing = await operations.GetByOperationIdAsync(operationId, true, cancellationToken) ?? throw new ConflictException($"OperationId '{operationId}' was created concurrently but could not be loaded.");
            if (existing.OperationType != type || existing.RequestHash != hash) throw new ConflictException($"OperationId '{operationId}' was already used with different data.");
            return existing;
        }
    }

    private async Task<Exception> FailBeforeSnapshotAsync(OrderOperation operation, string code, string message, CancellationToken cancellationToken, bool notFound = false)
    {
        operation.Status = OrderOperationStatus.Failed; operation.ResultStatusCode = notFound ? 404 : 409; operation.ErrorCode = code; operation.CompletedAtUtc = Now; operation.ConcurrencyToken = Guid.NewGuid(); await operations.SaveChangesAsync(cancellationToken);
        return notFound ? new NotFoundException(message) : new ConflictException(message);
    }
    private async Task<Order> RequireOrderAsync(Guid id, bool tracking, CancellationToken cancellationToken) => await orders.GetDetailsByIdAsync(id, tracking, cancellationToken) ?? throw new NotFoundException($"Order '{id}' was not found.");
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new ConcurrencyConflictException($"The order was changed by another request. Conflicting entities: {string.Join(", ", exception.Entries.Select(x => x.Metadata.ClrType.Name))}."); }
    }
    private DateTimeOffset Now => timeProvider.GetUtcNow();
    private string? CorrelationId => httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
    private static string? Truncate(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
