using System.ComponentModel.DataAnnotations;
using CartService.Application.Validation;

namespace CartService.Application.DTOs.Checkout;

public sealed class PrepareCheckoutRequest
{
    [Required, StringLength(150)] public string OperationId { get; init; } = string.Empty;
    public bool AcceptPriceChanges { get; init; }
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed record CheckoutCartItemSnapshot(Guid ProductId, string Sku, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);
public sealed record CheckoutCartSnapshot(Guid CartId, Guid CustomerId, string Currency, decimal Subtotal, int TotalQuantity, IReadOnlyList<CheckoutCartItemSnapshot> Items);

public sealed record PrepareCheckoutResponse(
    Guid CartId,
    Guid CustomerId,
    Guid CheckoutToken,
    string CheckoutOperationId,
    DateTimeOffset CheckoutExpiresAtUtc,
    string Currency,
    decimal Subtotal,
    int TotalQuantity,
    IReadOnlyList<CheckoutCartItemSnapshot> Items,
    bool WasAlreadyPrepared);

public sealed class CompleteCheckoutRequest
{
    [NotEmptyGuid(ErrorMessage = "checkoutToken is required.")] public Guid CheckoutToken { get; init; }
    [Required, StringLength(150)] public string OperationId { get; init; } = string.Empty;
    [NotEmptyGuid(ErrorMessage = "orderId is required.")] public Guid OrderId { get; init; }
}

public sealed record CompleteCheckoutResponse(Guid CartId, Guid OrderId, DateTimeOffset CheckedOutAtUtc, bool WasAlreadyCompleted);

public sealed class CancelCheckoutRequest
{
    [NotEmptyGuid(ErrorMessage = "checkoutToken is required.")] public Guid CheckoutToken { get; init; }
    [Required, StringLength(150)] public string OperationId { get; init; } = string.Empty;
    [StringLength(500)] public string? Reason { get; init; }
}
