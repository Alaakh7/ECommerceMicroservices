using OrderService.Application.DTOs.Orders;
using OrderService.Application.Models;
using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<Order?> GetDetailsByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken);
    Task<Order?> GetByCreateOperationIdAsync(string operationId, CancellationToken cancellationToken);
    Task<Order?> GetByCartIdAsync(Guid cartId, CancellationToken cancellationToken);
    Task<PagedResponse<OrderSummaryResponse>> GetPagedAsync(OrderQueryParameters query, CancellationToken cancellationToken);
    Task<PagedResponse<OrderSummaryResponse>> GetCustomerOrdersAsync(Guid customerId, CustomerOrderQueryParameters query, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetRecoverableOrderIdsAsync(DateTimeOffset now, int maximumRetryCount, int batchSize, CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IOrderOperationRepository
{
    Task<OrderOperation?> GetByOperationIdAsync(string operationId, bool tracking, CancellationToken cancellationToken);
    Task AddAsync(OrderOperation operation, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IOrderStatusHistoryRepository
{
    Task AddAsync(OrderStatusHistory history, CancellationToken cancellationToken);
}
