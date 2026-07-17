using OrderService.Application.DTOs.Orders;
using OrderService.Application.Models;

namespace OrderService.Application.Interfaces;

public interface IOrderService
{
    Task<PagedResponse<OrderSummaryResponse>> GetAsync(OrderQueryParameters query, CancellationToken cancellationToken);
    Task<OrderDetailsResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<OrderDetailsResponse> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken);
    Task<PagedResponse<OrderSummaryResponse>> GetCustomerOrdersAsync(Guid customerId, CustomerOrderQueryParameters query, CancellationToken cancellationToken);
    Task<OrderStatusResponse> GetStatusAsync(Guid id, CancellationToken cancellationToken);
}

public interface IOrderWorkflowService
{
    Task<OperationResult<CreateOrderResponse>> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);
    Task<OperationResult<OrderDetailsResponse>> RetryAsync(Guid orderId, RetryOrderRequest request, CancellationToken cancellationToken);
    Task RecoverAsync(Guid orderId, string changedBy, CancellationToken cancellationToken);
}

public interface IOrderCancellationService
{
    Task<OperationResult<OrderDetailsResponse>> CancelAsync(Guid orderId, CancelOrderRequest request, CancellationToken cancellationToken);
    Task<OperationResult<OrderDetailsResponse>> CompleteAsync(Guid orderId, CompleteOrderRequest request, CancellationToken cancellationToken);
}

public interface IOrderRecoveryService { Task<int> RecoverBatchAsync(CancellationToken cancellationToken); }
