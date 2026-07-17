using System.ComponentModel.DataAnnotations;
using ProductService.Domain.Enums;

namespace ProductService.Application.DTOs.Inventory;

public sealed class AdjustStockRequest
{
    [Required, StringLength(150)] public string OperationId { get; init; } = string.Empty;
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
    [StringLength(500)] public string? Reason { get; init; }
}

public sealed record StockAdjustmentResponse(Guid ProductId, string OperationId, InventoryOperationType OperationType, int Quantity, int StockBefore, int StockAfter, DateTimeOffset ProcessedAtUtc, bool WasAlreadyProcessed);
public sealed record ProductAvailabilityResponse(Guid ProductId, int RequestedQuantity, int AvailableQuantity, bool IsAvailable, bool IsActive);

public sealed class BatchAvailabilityItemRequest
{
    public Guid ProductId { get; init; }
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
}

public sealed class BatchAvailabilityRequest : IValidatableObject
{
    [Required, MinLength(1), MaxLength(100)] public IReadOnlyList<BatchAvailabilityItemRequest> Items { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Items.GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
            yield return new("Duplicate product IDs are not allowed.", [nameof(Items)]);
    }
}

public sealed record BatchAvailabilityItemResponse(Guid ProductId, int RequestedQuantity, int AvailableQuantity, bool IsAvailable, bool IsActive, bool Exists);
public sealed record BatchAvailabilityResponse(IReadOnlyList<BatchAvailabilityItemResponse> Items, bool AllAvailable);
public sealed record InventoryTransactionResponse(Guid Id, Guid ProductId, string OperationId, InventoryOperationType OperationType, int Quantity, int StockBefore, int StockAfter, string? Reason, DateTimeOffset CreatedAtUtc);

public sealed class InventoryTransactionQueryParameters : IValidatableObject
{
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public InventoryOperationType? OperationType { get; init; }
    public DateTimeOffset? FromDateUtc { get; init; }
    public DateTimeOffset? ToDateUtc { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OperationType.HasValue && !Enum.IsDefined(OperationType.Value)) yield return new("Invalid operationType.", [nameof(OperationType)]);
        if (FromDateUtc.HasValue && ToDateUtc.HasValue && FromDateUtc > ToDateUtc)
            yield return new("fromDateUtc cannot be greater than toDateUtc.", [nameof(FromDateUtc), nameof(ToDateUtc)]);
    }
}
