using System.Text.Json.Serialization;

namespace OrderService.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    PendingConfirmation = 1,
    InventoryProcessing = 2,
    CartCompletionPending = 3,
    Confirmed = 4,
    Cancelled = 5,
    Failed = 6,
    Completed = 7
}

[JsonConverter(typeof(JsonStringEnumConverter<OrderItemInventoryStatus>))]
public enum OrderItemInventoryStatus
{
    NotProcessed = 1,
    Decreased = 2,
    RestorePending = 3,
    Restored = 4,
    Failed = 5
}

[JsonConverter(typeof(JsonStringEnumConverter<OrderAddressType>))]
public enum OrderAddressType { Shipping = 1, Billing = 2 }
[JsonConverter(typeof(JsonStringEnumConverter<OrderOperationType>))]
public enum OrderOperationType { Create = 1, Cancel = 2, Complete = 3, Retry = 4 }
[JsonConverter(typeof(JsonStringEnumConverter<OrderOperationStatus>))]
public enum OrderOperationStatus { InProgress = 1, Succeeded = 2, Failed = 3 }
