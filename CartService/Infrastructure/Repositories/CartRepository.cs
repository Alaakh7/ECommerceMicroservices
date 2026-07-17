using CartService.Application.DTOs.Carts;
using CartService.Application.Interfaces;
using CartService.Application.Models;
using CartService.Domain.Entities;
using CartService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartService.Infrastructure.Repositories;

public sealed class CartRepository(CartDbContext db) : ICartRepository
{
    public IQueryable<Cart> Query() => db.Carts.AsNoTracking();
    public IQueryable<Cart> QueryTracked() => db.Carts;

    public Task<Cart?> GetByIdAsync(Guid id, bool withItems, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Cart> query = tracking ? db.Carts : db.Carts.AsNoTracking();
        if (withItems) query = query.Include(x => x.Items);
        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Cart?> GetOpenByCustomerIdAsync(Guid customerId, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Cart> query = tracking ? db.Carts : db.Carts.AsNoTracking();
        return query.Include(x => x.Items).SingleOrDefaultAsync(
            x => x.CustomerId == customerId && (x.Status == Domain.Enums.CartStatus.Active || x.Status == Domain.Enums.CartStatus.CheckoutPending), cancellationToken);
    }

    public async Task<PagedResponse<CartSummaryResponse>> GetCustomerHistoryAsync(Guid customerId, CartQueryParameters query, CancellationToken cancellationToken)
    {
        var source = db.Carts.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (query.Status.HasValue) source = source.Where(x => x.Status == query.Status);
        if (query.CreatedFromUtc.HasValue) source = source.Where(x => x.CreatedAtUtc >= query.CreatedFromUtc);
        if (query.CreatedToUtc.HasValue) source = source.Where(x => x.CreatedAtUtc <= query.CreatedToUtc);
        source = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? source.OrderBy(x => x.CreatedAtUtc)
            : source.OrderByDescending(x => x.CreatedAtUtc);
        var total = await source.LongCountAsync(cancellationToken);
        var items = await source.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CartSummaryResponse(x.Id, x.CustomerId, x.Status, x.Currency, x.Subtotal, x.TotalQuantity,
                x.DistinctItemCount, x.ExpiresAtUtc, x.CompletedOrderId, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyToken))
            .ToListAsync(cancellationToken);
        return PagedResponse<CartSummaryResponse>.Create(items, query.PageNumber, query.PageSize, total);
    }

    public Task AddAsync(Cart cart, CancellationToken cancellationToken) => db.Carts.AddAsync(cart, cancellationToken).AsTask();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
