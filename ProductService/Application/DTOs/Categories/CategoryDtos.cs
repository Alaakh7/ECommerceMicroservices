using System.ComponentModel.DataAnnotations;

namespace ProductService.Application.DTOs.Categories;

public sealed class CreateCategoryRequest
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(120), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; init; } = string.Empty;
    [StringLength(1000)] public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateCategoryRequest
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(120), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; init; } = string.Empty;
    [StringLength(1000)] public string? Description { get; init; }
    public bool IsActive { get; init; }
    public Guid ConcurrencyToken { get; init; }
}

public sealed class UpdateCategoryStatusRequest
{
    public bool IsActive { get; init; }
    public Guid ConcurrencyToken { get; init; }
}

public sealed record CategoryResponse(Guid Id, string Name, string Slug, string? Description, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, Guid ConcurrencyToken);
public sealed record CategorySummaryResponse(Guid Id, string Name, string Slug, bool IsActive);

public sealed class CategoryQueryParameters : IValidatableObject
{
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    public string SortBy { get; init; } = "name";
    public string SortDirection { get; init; } = "asc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!new[] { "name", "createdAt", "updatedAt", "slug" }.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
            yield return new("sortBy must be one of: name, slug, createdAt, updatedAt.", [nameof(SortBy)]);
        if (!string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) && !string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            yield return new("sortDirection must be asc or desc.", [nameof(SortDirection)]);
    }
}
