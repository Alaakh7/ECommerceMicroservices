using System.ComponentModel.DataAnnotations;
using CartService.Application.DTOs.Carts;
using CartService.Application.Validation;

namespace CartService.Application.DTOs.CartItems;

public sealed class AddCartItemRequest
{
    [NotEmptyGuid(ErrorMessage = "productId is required.")] public Guid ProductId { get; init; }
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
    public Guid? ExpectedCartConcurrencyToken { get; init; }
}

public sealed class UpdateCartItemQuantityRequest
{
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
    [NotEmptyGuid(ErrorMessage = "cartConcurrencyToken is required.")] public Guid CartConcurrencyToken { get; init; }
    [NotEmptyGuid(ErrorMessage = "itemConcurrencyToken is required.")] public Guid ItemConcurrencyToken { get; init; }
}

public sealed record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string Sku,
    string ProductName,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    Guid? ProductConcurrencyToken,
    DateTimeOffset? ProductUpdatedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    Guid ConcurrencyToken);

public sealed class RemoveCartItemRequest
{
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed class ClearCartRequest
{
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed class RefreshCartRequest
{
    public bool UpdatePrices { get; init; }
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed record CartPriceChangeResponse(Guid ProductId, decimal StoredPrice, decimal CurrentPrice);
public sealed record CartAvailabilityIssueResponse(Guid ProductId, string Code, string Detail, int RequestedQuantity, int AvailableQuantity);
public sealed record RefreshCartResponse(
    CartResponse Cart,
    IReadOnlyList<CartPriceChangeResponse> PriceChanges,
    IReadOnlyList<CartAvailabilityIssueResponse> AvailabilityIssues,
    IReadOnlyList<Guid> RemovedProducts,
    bool WasUpdated);

public sealed class ValidateCartRequest
{
    public bool RequireDefaultShippingAddress { get; init; } = true;
}

public sealed record CartValidationItemResponse(
    Guid ProductId,
    bool Exists,
    bool IsActive,
    bool IsAvailable,
    int RequestedQuantity,
    int AvailableQuantity,
    decimal StoredPrice,
    decimal? CurrentPrice,
    bool PriceChanged);

public sealed record ValidateCartResponse(
    bool IsValid,
    bool CustomerEligible,
    IReadOnlyList<CartValidationItemResponse> Items,
    IReadOnlyList<CartPriceChangeResponse> PriceChanges,
    IReadOnlyList<CartAvailabilityIssueResponse> AvailabilityIssues,
    decimal CurrentSubtotal,
    decimal StoredSubtotal,
    DateTimeOffset ValidatedAtUtc);
