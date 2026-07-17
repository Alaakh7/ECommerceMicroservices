using Microsoft.EntityFrameworkCore;
using OrderService.Application.DTOs.ExternalServices;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces;
using OrderService.Application.Models;
using OrderService.Common.Exceptions;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Data;

namespace OrderService.Application.Services;

public sealed class OrderCancellationService(
    OrderDbContext db,
    IOrderRepository orders,
    IOrderOperationRepository operations,
    ICartServiceClient carts,
    OrderWorkflowService workflow,
    RequestHashService hashes,
    TimeProvider timeProvider,
    ILogger<OrderCancellationService> logger) : IOrderCancellationService
{
    public async Task<OperationResult<OrderDetailsResponse>> CancelAsync(Guid orderId, CancelOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await RequireAsync(orderId, cancellationToken);
        var operation = await GetOrCreateAsync(request.OperationId.Trim(), OrderOperationType.Cancel, hashes.ForCancel(orderId, request), cancellationToken);
        if (operation.OrderId.HasValue)
        {
            if (operation.OrderId != orderId) throw new ConflictException("OperationId belongs to another order.");
            var status = order.Status == OrderStatus.Cancelled ? 200 : 202;
            return new(OrderMapper.ToDetails(order), status, true);
        }
        EnsureConcurrency(order, request.ConcurrencyToken);
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.Completed) throw new InvalidOrderStateException($"Order in {order.Status} state cannot be cancelled.");
        order.CancellationOperationId = operation.OperationId; operation.OrderId = order.Id; order.ConcurrencyToken = Guid.NewGuid(); order.UpdatedAtUtc = Now; await SaveAsync(cancellationToken);

        await workflow.RestoreInventoryAsync(order, cancellationToken);
        if (order.Items.Any(x => x.InventoryStatus == OrderItemInventoryStatus.RestorePending))
        {
            operation.ResultStatusCode = 202; operation.ConcurrencyToken = Guid.NewGuid(); await SaveAsync(cancellationToken);
            return new(OrderMapper.ToDetails(order), 202);
        }
        if (order.Status != OrderStatus.Confirmed && order.CartCheckoutToken.HasValue)
        {
            try { await carts.CancelCheckoutAsync(order.CartId, new(order.CartCheckoutToken.Value, order.CartCheckoutOperationId!, request.Reason), cancellationToken); }
            catch (Exception exception) when (exception is ExternalServiceTimeoutException or ExternalServiceUnavailableException)
            {
                await workflow.ScheduleRetryAsync(order, "cart_cancel_pending", cancellationToken); operation.ResultStatusCode = 202; operation.ConcurrencyToken = Guid.NewGuid(); await SaveAsync(cancellationToken);
                return new(OrderMapper.ToDetails(order), 202);
            }
            catch (ExternalConflictException) { logger.LogInformation("Cart {CartId} was no longer checkout-pending while cancelling order {OrderId}", order.CartId, order.Id); }
        }
        await workflow.TransitionAsync(order, OrderStatus.Cancelled, request.Reason, "Customer", cancellationToken, allowCancellationFromPending: true);
        order.CancelledAtUtc = Now; operation.Status = OrderOperationStatus.Succeeded; operation.ResultStatusCode = 200; operation.CompletedAtUtc = Now; operation.ConcurrencyToken = Guid.NewGuid(); order.ConcurrencyToken = Guid.NewGuid();
        await SaveAsync(cancellationToken);
        logger.LogInformation("Cancelled order {OrderId} with operation {OperationId}", order.Id, operation.OperationId);
        return new(OrderMapper.ToDetails(order), 200);
    }

    public async Task<OperationResult<OrderDetailsResponse>> CompleteAsync(Guid orderId, CompleteOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await RequireAsync(orderId, cancellationToken);
        var operation = await GetOrCreateAsync(request.OperationId.Trim(), OrderOperationType.Complete, hashes.ForComplete(orderId, request), cancellationToken);
        if (operation.OrderId.HasValue)
        {
            if (operation.OrderId != orderId) throw new ConflictException("OperationId belongs to another order.");
            return new(OrderMapper.ToDetails(order), 200, true);
        }
        EnsureConcurrency(order, request.ConcurrencyToken);
        if (order.Status != OrderStatus.Confirmed) throw new InvalidOrderStateException("Only a confirmed order can be completed.");
        operation.OrderId = order.Id; order.CompletionOperationId = operation.OperationId;
        await workflow.TransitionAsync(order, OrderStatus.Completed, request.Reason ?? "Order completed.", "Administrator", cancellationToken);
        order.CompletedAtUtc = Now; operation.Status = OrderOperationStatus.Succeeded; operation.ResultStatusCode = 200; operation.CompletedAtUtc = Now; operation.ConcurrencyToken = Guid.NewGuid(); order.ConcurrencyToken = Guid.NewGuid();
        await SaveAsync(cancellationToken);
        logger.LogInformation("Completed order {OrderId} with operation {OperationId}", order.Id, operation.OperationId);
        return new(OrderMapper.ToDetails(order), 200);
    }

    private async Task<OrderOperation> GetOrCreateAsync(string id, OrderOperationType type, string hash, CancellationToken cancellationToken)
    {
        var existing = await operations.GetByOperationIdAsync(id, true, cancellationToken);
        if (existing is not null)
        {
            if (existing.OperationType != type || existing.RequestHash != hash) throw new ConflictException($"OperationId '{id}' was already used with different data.");
            return existing;
        }
        var operation = new OrderOperation { Id = Guid.NewGuid(), OperationId = id, OperationType = type, RequestHash = hash, Status = OrderOperationStatus.InProgress, CreatedAtUtc = Now, ConcurrencyToken = Guid.NewGuid() };
        await operations.AddAsync(operation, cancellationToken);
        try { await operations.SaveChangesAsync(cancellationToken); return operation; }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear(); existing = await operations.GetByOperationIdAsync(id, true, cancellationToken) ?? throw new ConflictException($"OperationId '{id}' was created concurrently but could not be loaded.");
            if (existing.OperationType != type || existing.RequestHash != hash) throw new ConflictException($"OperationId '{id}' was already used with different data.");
            return existing;
        }
    }
    private async Task<Order> RequireAsync(Guid id, CancellationToken cancellationToken) => await orders.GetDetailsByIdAsync(id, true, cancellationToken) ?? throw new NotFoundException($"Order '{id}' was not found.");
    private static void EnsureConcurrency(Order order, Guid supplied) { if (order.ConcurrencyToken != supplied) throw new ConcurrencyConflictException("The supplied concurrencyToken is stale."); }
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("The order changed concurrently."); }
    }
    private DateTimeOffset Now => timeProvider.GetUtcNow();
}
