using CartService.Application.Interfaces;
using CartService.Domain.Entities;
using CartService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartService.Infrastructure.Repositories;

public sealed class CartItemRepository(CartDbContext db) : ICartItemRepository
{
    public Task<CartItem?> GetByProductIdAsync(Guid cartId, Guid productId, CancellationToken cancellationToken) =>
        db.CartItems.SingleOrDefaultAsync(x => x.CartId == cartId && x.ProductId == productId, cancellationToken);

    public Task AddAsync(CartItem item, CancellationToken cancellationToken) => db.CartItems.AddAsync(item, cancellationToken).AsTask();
    public void Remove(CartItem item) => db.CartItems.Remove(item);
    public void RemoveRange(IEnumerable<CartItem> items) => db.CartItems.RemoveRange(items);
}
