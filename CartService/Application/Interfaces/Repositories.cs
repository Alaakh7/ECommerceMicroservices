using CartService.Application.DTOs.Carts;
using CartService.Application.Models;
using CartService.Domain.Entities;

namespace CartService.Application.Interfaces;

public interface ICartRepository
{
    IQueryable<Cart> Query();
    IQueryable<Cart> QueryTracked();
    Task<Cart?> GetByIdAsync(Guid id, bool withItems, bool tracking, CancellationToken cancellationToken);
    Task<Cart?> GetOpenByCustomerIdAsync(Guid customerId, bool tracking, CancellationToken cancellationToken);
    Task<PagedResponse<CartSummaryResponse>> GetCustomerHistoryAsync(Guid customerId, CartQueryParameters query, CancellationToken cancellationToken);
    Task AddAsync(Cart cart, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ICartItemRepository
{
    Task<CartItem?> GetByProductIdAsync(Guid cartId, Guid productId, CancellationToken cancellationToken);
    Task AddAsync(CartItem item, CancellationToken cancellationToken);
    void Remove(CartItem item);
    void RemoveRange(IEnumerable<CartItem> items);
}
