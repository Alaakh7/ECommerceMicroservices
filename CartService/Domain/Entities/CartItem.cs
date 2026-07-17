namespace CartService.Domain.Entities;

public sealed class CartItem
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public required Cart Cart { get; set; }
    public Guid ProductId { get; set; }
    public required string Sku { get; set; }
    public required string ProductName { get; set; }
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ProductConcurrencyToken { get; set; }
    public DateTimeOffset? ProductUpdatedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
