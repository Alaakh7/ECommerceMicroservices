using Microsoft.EntityFrameworkCore;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repositories;

public sealed class OrderRepository(OrderDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken) => Track(db.Orders, tracking).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Order?> GetDetailsByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken) => Track(db.Orders, tracking).Include(x => x.Items).Include(x => x.Addresses).Include(x => x.StatusHistory).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken) => db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Addresses).Include(x => x.StatusHistory).FirstOrDefaultAsync(x => x.OrderNumber.ToLower() == orderNumber.ToLower(), cancellationToken);
    public Task<Order?> GetByCreateOperationIdAsync(string operationId, CancellationToken cancellationToken) => db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.CreateOperationId == operationId, cancellationToken);
    public Task<Order?> GetByCartIdAsync(Guid cartId, CancellationToken cancellationToken) => db.Orders.AsNoTracking().Where(x => x.Status != OrderStatus.Failed && x.Status != OrderStatus.Cancelled).FirstOrDefaultAsync(x => x.CartId == cartId, cancellationToken);
    public Task AddAsync(Order order, CancellationToken cancellationToken) => db.Orders.AddAsync(order, cancellationToken).AsTask();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<PagedResponse<OrderSummaryResponse>> GetPagedAsync(OrderQueryParameters q, CancellationToken cancellationToken)
    {
        var source = db.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var search = q.Search.Trim().ToLower();
            var isGuid = Guid.TryParse(search, out var guid);
            source = source.Where(x => x.OrderNumber.ToLower().Contains(search) || (isGuid && (x.CustomerId == guid || x.CartId == guid)) || x.Items.Any(i => i.ProductName.ToLower().Contains(search) || i.Sku.ToLower().Contains(search)));
        }
        if (q.CustomerId.HasValue) source = source.Where(x => x.CustomerId == q.CustomerId);
        if (q.CartId.HasValue) source = source.Where(x => x.CartId == q.CartId);
        if (q.Status.HasValue) source = source.Where(x => x.Status == q.Status);
        if (q.CreatedFromUtc.HasValue) source = source.Where(x => x.CreatedAtUtc >= q.CreatedFromUtc);
        if (q.CreatedToUtc.HasValue) source = source.Where(x => x.CreatedAtUtc <= q.CreatedToUtc);
        if (q.MinTotal.HasValue) source = source.Where(x => x.TotalAmount >= q.MinTotal);
        if (q.MaxTotal.HasValue) source = source.Where(x => x.TotalAmount <= q.MaxTotal);
        source = IsSqlite && new[] { "createdAt", "updatedAt", "confirmedAt" }.Contains(q.SortBy, StringComparer.OrdinalIgnoreCase)
            ? (string.Equals(q.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? source.OrderBy(x => x.OrderNumber) : source.OrderByDescending(x => x.OrderNumber))
            : Sort(source, q.SortBy, q.SortDirection);
        return await PageAsync(source, q.PageNumber, q.PageSize, cancellationToken);
    }

    public async Task<PagedResponse<OrderSummaryResponse>> GetCustomerOrdersAsync(Guid customerId, CustomerOrderQueryParameters q, CancellationToken cancellationToken)
    {
        var source = db.Orders.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (q.Status.HasValue) source = source.Where(x => x.Status == q.Status);
        if (q.CreatedFromUtc.HasValue) source = source.Where(x => x.CreatedAtUtc >= q.CreatedFromUtc);
        if (q.CreatedToUtc.HasValue) source = source.Where(x => x.CreatedAtUtc <= q.CreatedToUtc);
        source = IsSqlite
            ? (string.Equals(q.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? source.OrderBy(x => x.OrderNumber) : source.OrderByDescending(x => x.OrderNumber))
            : (string.Equals(q.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? source.OrderBy(x => x.CreatedAtUtc) : source.OrderByDescending(x => x.CreatedAtUtc));
        return await PageAsync(source, q.PageNumber, q.PageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetRecoverableOrderIdsAsync(DateTimeOffset now, int maximumRetryCount, int batchSize, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking().Where(x => x.RetryCount < maximumRetryCount && (!x.NextRetryAtUtc.HasValue || x.NextRetryAtUtc <= now) &&
            (x.Status == OrderStatus.PendingConfirmation || x.Status == OrderStatus.InventoryProcessing || x.Status == OrderStatus.CartCompletionPending || x.Items.Any(i => i.InventoryStatus == OrderItemInventoryStatus.RestorePending)))
            .OrderBy(x => x.NextRetryAtUtc).ThenBy(x => x.CreatedAtUtc).Select(x => x.Id).Take(batchSize).ToListAsync(cancellationToken);

    private static IQueryable<Order> Track(IQueryable<Order> query, bool tracking) => tracking ? query : query.AsNoTracking();
    private bool IsSqlite => db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
    private static IQueryable<Order> Sort(IQueryable<Order> source, string sortBy, string direction)
    {
        var asc = string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy.ToLowerInvariant() switch
        {
            "ordernumber" => asc ? source.OrderBy(x => x.OrderNumber) : source.OrderByDescending(x => x.OrderNumber),
            "status" => asc ? source.OrderBy(x => x.Status) : source.OrderByDescending(x => x.Status),
            "total" => asc ? source.OrderBy(x => x.TotalAmount) : source.OrderByDescending(x => x.TotalAmount),
            "updatedat" => asc ? source.OrderBy(x => x.UpdatedAtUtc) : source.OrderByDescending(x => x.UpdatedAtUtc),
            "confirmedat" => asc ? source.OrderBy(x => x.ConfirmedAtUtc) : source.OrderByDescending(x => x.ConfirmedAtUtc),
            _ => asc ? source.OrderBy(x => x.CreatedAtUtc) : source.OrderByDescending(x => x.CreatedAtUtc)
        };
    }
    private static async Task<PagedResponse<OrderSummaryResponse>> PageAsync(IQueryable<Order> source, int page, int size, CancellationToken cancellationToken)
    {
        var total = await source.LongCountAsync(cancellationToken);
        var items = await source.Skip((page - 1) * size).Take(size).Select(x => new OrderSummaryResponse(x.Id, x.OrderNumber, x.CustomerId, x.CartId, x.Currency, x.Status, x.TotalAmount, x.TotalQuantity, x.DistinctItemCount, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConfirmedAtUtc, x.ConcurrencyToken)).ToListAsync(cancellationToken);
        return PagedResponse<OrderSummaryResponse>.Create(items, page, size, total);
    }
}

public sealed class OrderOperationRepository(OrderDbContext db) : IOrderOperationRepository
{
    public Task<OrderOperation?> GetByOperationIdAsync(string operationId, bool tracking, CancellationToken cancellationToken) => (tracking ? db.OrderOperations : db.OrderOperations.AsNoTracking()).FirstOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);
    public Task AddAsync(OrderOperation operation, CancellationToken cancellationToken) => db.OrderOperations.AddAsync(operation, cancellationToken).AsTask();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

public sealed class OrderStatusHistoryRepository(OrderDbContext db) : IOrderStatusHistoryRepository
{
    public Task AddAsync(OrderStatusHistory history, CancellationToken cancellationToken) => db.OrderStatusHistories.AddAsync(history, cancellationToken).AsTask();
}
