using System.ComponentModel.DataAnnotations;
using OrderService.Application.Validation;
using OrderService.Domain.Enums;

namespace OrderService.Application.DTOs.Orders;

public sealed class CreateOrderRequest
{
    [NotEmptyGuid(ErrorMessage = "cartId is required.")] public Guid CartId { get; init; }
    [Required, StringLength(150)] public string OperationId { get; init; } = string.Empty;
    public bool AcceptPriceChanges { get; init; }
    public Guid? ShippingAddressId { get; init; }
    public Guid? BillingAddressId { get; init; }
    public bool UseShippingAddressForBilling { get; init; }
}

public sealed class CancelOrderRequest
{
    [Required, StringLength(150)] public string OperationId { get; init; } = string.Empty;
    [Required, StringLength(500)] public string Reason { get; init; } = string.Empty;
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed class CompleteOrderRequest
{
    [Required, StringLength(150)] public string OperationId { get; init; } = string.Empty;
    [StringLength(500)] public string? Reason { get; init; }
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed class RetryOrderRequest
{
    [Required, StringLength(150)] public string OperationId { get; init; } = string.Empty;
}

public sealed class OrderQueryParameters : IValidatableObject
{
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? CartId { get; init; }
    [EnumDataType(typeof(OrderStatus))] public OrderStatus? Status { get; init; }
    public DateTimeOffset? CreatedFromUtc { get; init; }
    public DateTimeOffset? CreatedToUtc { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal? MinTotal { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal? MaxTotal { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public string SortDirection { get; init; } = "desc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status.HasValue && !Enum.IsDefined(Status.Value)) yield return new("Invalid status.", [nameof(Status)]);
        if (CreatedFromUtc > CreatedToUtc) yield return new("createdFromUtc cannot be greater than createdToUtc.", [nameof(CreatedFromUtc), nameof(CreatedToUtc)]);
        if (MinTotal > MaxTotal) yield return new("minTotal cannot be greater than maxTotal.", [nameof(MinTotal), nameof(MaxTotal)]);
        if (!new[] { "orderNumber", "status", "total", "createdAt", "updatedAt", "confirmedAt" }.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
            yield return new("Invalid sortBy.", [nameof(SortBy)]);
        if (!new[] { "asc", "desc" }.Contains(SortDirection, StringComparer.OrdinalIgnoreCase)) yield return new("sortDirection must be asc or desc.", [nameof(SortDirection)]);
    }
}

public sealed class CustomerOrderQueryParameters : IValidatableObject
{
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [EnumDataType(typeof(OrderStatus))] public OrderStatus? Status { get; init; }
    public DateTimeOffset? CreatedFromUtc { get; init; }
    public DateTimeOffset? CreatedToUtc { get; init; }
    public string SortDirection { get; init; } = "desc";
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status.HasValue && !Enum.IsDefined(Status.Value)) yield return new("Invalid status.", [nameof(Status)]);
        if (CreatedFromUtc > CreatedToUtc) yield return new("createdFromUtc cannot be greater than createdToUtc.", [nameof(CreatedFromUtc), nameof(CreatedToUtc)]);
        if (!new[] { "asc", "desc" }.Contains(SortDirection, StringComparer.OrdinalIgnoreCase)) yield return new("sortDirection must be asc or desc.", [nameof(SortDirection)]);
    }
}
