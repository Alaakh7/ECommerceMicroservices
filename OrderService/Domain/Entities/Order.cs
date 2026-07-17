using OrderService.Domain.Enums;

namespace OrderService.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid CartId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
    public int DistinctItemCount { get; set; }
    public string CreateOperationId { get; set; } = string.Empty;
    public Guid? CartCheckoutToken { get; set; }
    public string? CartCheckoutOperationId { get; set; }
    public string? CancellationOperationId { get; set; }
    public string? CompletionOperationId { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAtUtc { get; set; }
    public DateTimeOffset? LastRetryAtUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderAddressSnapshot> Addresses { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<OrderOperation> Operations { get; set; } = [];
}

public sealed class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public string InventoryDecreaseOperationId { get; set; } = string.Empty;
    public string InventoryRestoreOperationId { get; set; } = string.Empty;
    public OrderItemInventoryStatus InventoryStatus { get; set; }
    public DateTimeOffset? StockDecreasedAtUtc { get; set; }
    public DateTimeOffset? StockRestoredAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

public sealed class OrderAddressSnapshot
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public OrderAddressType AddressType { get; set; }
    public Guid? SourceAddressId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public OrderStatus? PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public string? Reason { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class OrderOperation
{
    public Guid Id { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public OrderOperationType OperationType { get; set; }
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public OrderOperationStatus Status { get; set; }
    public int? ResultStatusCode { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
