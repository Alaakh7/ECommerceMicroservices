using CartService.Domain.Enums;

namespace CartService.Domain.Entities;

public sealed class Cart
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public CartStatus Status { get; set; }
    public required string Currency { get; set; }
    public decimal Subtotal { get; set; }
    public int TotalQuantity { get; set; }
    public int DistinctItemCount { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid? CheckoutToken { get; set; }
    public string? CheckoutOperationId { get; set; }
    public DateTimeOffset? CheckoutExpiresAtUtc { get; set; }
    public Guid? CompletedOrderId { get; set; }
    public DateTimeOffset? CheckedOutAtUtc { get; set; }
    public DateTimeOffset? AbandonedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public ICollection<CartItem> Items { get; set; } = [];
}
