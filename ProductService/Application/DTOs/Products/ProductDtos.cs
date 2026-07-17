using System.ComponentModel.DataAnnotations;

namespace ProductService.Application.DTOs.Products;

public sealed class CreateProductRequest
{
    [Required, StringLength(64)] public string Sku { get; init; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    [StringLength(2000)] public string? Description { get; init; }
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal Price { get; init; }
    [Range(0, int.MaxValue)] public int StockQuantity { get; init; }
    [Range(0, int.MaxValue)] public int ReorderLevel { get; init; }
    public Guid CategoryId { get; init; }
    [StringLength(2048), Url] public string? ImageUrl { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateProductRequest
{
    [Required, StringLength(64)] public string Sku { get; init; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    [StringLength(2000)] public string? Description { get; init; }
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal Price { get; init; }
    [Range(0, int.MaxValue)] public int ReorderLevel { get; init; }
    public Guid CategoryId { get; init; }
    [StringLength(2048), Url] public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
    public Guid ConcurrencyToken { get; init; }
}

public sealed class UpdateProductStatusRequest
{
    public bool IsActive { get; init; }
    public Guid ConcurrencyToken { get; init; }
}

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    int ReorderLevel,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    string? ImageUrl,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    Guid ConcurrencyToken);

public sealed record ProductSummaryResponse(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity,
    int ReorderLevel,
    Guid CategoryId,
    string CategoryName,
    string? ImageUrl,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed class ProductQueryParameters : IValidatableObject
{
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategorySlug { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal? MinPrice { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal? MaxPrice { get; init; }
    public bool? InStock { get; init; }
    public bool? IsActive { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public string SortDirection { get; init; } = "desc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var sortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "price", "stock", "createdAt", "updatedAt", "sku" };
        if (!sortFields.Contains(SortBy)) yield return new("sortBy must be one of: name, price, stock, createdAt, updatedAt, sku.", [nameof(SortBy)]);
        if (!string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) && !string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            yield return new("sortDirection must be asc or desc.", [nameof(SortDirection)]);
        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
            yield return new("minPrice cannot be greater than maxPrice.", [nameof(MinPrice), nameof(MaxPrice)]);
    }
}
