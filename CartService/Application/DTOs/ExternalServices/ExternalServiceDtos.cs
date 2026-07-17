using System.ComponentModel.DataAnnotations;

namespace CartService.Application.DTOs.ExternalServices;

public sealed record CustomerEligibilityExternalResponse(
    Guid CustomerId,
    bool Exists,
    string? Status,
    bool CanCreateCart,
    bool CanPlaceOrder,
    bool HasDefaultShippingAddress,
    bool HasDefaultBillingAddress);

public sealed record ProductExternalResponse(
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

public sealed record ProductAvailabilityExternalResponse(Guid ProductId, int RequestedQuantity, int AvailableQuantity, bool IsAvailable, bool IsActive);

public sealed class BatchProductAvailabilityExternalItemRequest
{
    public Guid ProductId { get; init; }
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
}

public sealed class BatchProductAvailabilityExternalRequest
{
    [Required, MinLength(1), MaxLength(100)] public IReadOnlyList<BatchProductAvailabilityExternalItemRequest> Items { get; init; } = [];
}

public sealed record BatchProductAvailabilityExternalItemResponse(Guid ProductId, int RequestedQuantity, int AvailableQuantity, bool IsAvailable, bool IsActive, bool Exists);
public sealed record BatchProductAvailabilityExternalResponse(IReadOnlyList<BatchProductAvailabilityExternalItemResponse> Items, bool AllAvailable);
