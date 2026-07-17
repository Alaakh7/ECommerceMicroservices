using ProductService.Domain.Enums;

namespace ProductService.Domain.Entities;

public sealed class InventoryTransaction
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public required Product Product { get; set; }
    public required string OperationId { get; set; }
    public InventoryOperationType OperationType { get; set; }
    public int Quantity { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
