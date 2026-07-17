using System.ComponentModel.DataAnnotations;
using CartService.Application.DTOs.CartItems;
using CartService.Application.Validation;
using CartService.Domain.Enums;

namespace CartService.Application.DTOs.Carts;

public sealed class CreateCartRequest
{
    [NotEmptyGuid(ErrorMessage = "customerId is required.")] public Guid CustomerId { get; init; }
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "currency must be three uppercase letters.")] public string? Currency { get; init; }
}

public sealed record CreateCartResponse(CartResponse Cart, bool WasCreated);

public sealed record CartResponse(
    Guid Id,
    Guid CustomerId,
    CartStatus Status,
    string Currency,
    decimal Subtotal,
    int TotalQuantity,
    int DistinctItemCount,
    DateTimeOffset ExpiresAtUtc,
    Guid? CheckoutToken,
    string? CheckoutOperationId,
    DateTimeOffset? CheckoutExpiresAtUtc,
    Guid? CompletedOrderId,
    DateTimeOffset? CheckedOutAtUtc,
    DateTimeOffset? AbandonedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    Guid ConcurrencyToken,
    IReadOnlyList<CartItemResponse> Items);

public sealed record CartSummaryResponse(
    Guid Id,
    Guid CustomerId,
    CartStatus Status,
    string Currency,
    decimal Subtotal,
    int TotalQuantity,
    int DistinctItemCount,
    DateTimeOffset ExpiresAtUtc,
    Guid? CompletedOrderId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    Guid ConcurrencyToken);

public sealed class CartQueryParameters : IValidatableObject
{
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [EnumDataType(typeof(CartStatus))] public CartStatus? Status { get; init; }
    public DateTimeOffset? CreatedFromUtc { get; init; }
    public DateTimeOffset? CreatedToUtc { get; init; }
    public string SortDirection { get; init; } = "desc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status.HasValue && !Enum.IsDefined(Status.Value)) yield return new("Invalid status.", [nameof(Status)]);
        if (CreatedFromUtc.HasValue && CreatedToUtc.HasValue && CreatedFromUtc > CreatedToUtc)
            yield return new("createdFromUtc cannot be greater than createdToUtc.", [nameof(CreatedFromUtc), nameof(CreatedToUtc)]);
        if (!string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) && !string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            yield return new("sortDirection must be asc or desc.", [nameof(SortDirection)]);
    }
}

public sealed class AbandonCartRequest
{
    [StringLength(500)] public string? Reason { get; init; }
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}
