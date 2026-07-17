namespace ProductService.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public ICollection<Product> Products { get; set; } = [];
}
