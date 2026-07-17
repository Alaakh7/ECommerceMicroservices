using CartService.Application.DTOs.CartItems;
using CartService.Application.DTOs.Carts;
using CartService.Application.DTOs.Checkout;
using CartService.Domain.Entities;

namespace CartService.Application.Models;

public static class CartMapper
{
    public static CartResponse ToResponse(Cart cart) => new(
        cart.Id, cart.CustomerId, cart.Status, cart.Currency, cart.Subtotal, cart.TotalQuantity, cart.DistinctItemCount,
        cart.ExpiresAtUtc, cart.CheckoutToken, cart.CheckoutOperationId, cart.CheckoutExpiresAtUtc, cart.CompletedOrderId,
        cart.CheckedOutAtUtc, cart.AbandonedAtUtc, cart.CreatedAtUtc, cart.UpdatedAtUtc, cart.ConcurrencyToken,
        cart.Items.OrderBy(x => x.CreatedAtUtc).Select(ToItemResponse).ToList());

    public static CartItemResponse ToItemResponse(CartItem item) => new(
        item.Id, item.ProductId, item.Sku, item.ProductName, item.ImageUrl, item.UnitPrice, item.Quantity, item.LineTotal,
        item.ProductConcurrencyToken, item.ProductUpdatedAtUtc, item.CreatedAtUtc, item.UpdatedAtUtc, item.ConcurrencyToken);

    public static IReadOnlyList<CheckoutCartItemSnapshot> ToSnapshot(Cart cart) => cart.Items.OrderBy(x => x.CreatedAtUtc)
        .Select(x => new CheckoutCartItemSnapshot(x.ProductId, x.Sku, x.ProductName, x.UnitPrice, x.Quantity, x.LineTotal)).ToList();
}
