using OrderService.Application.DTOs.Orders;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services;

internal static class OrderMapper
{
    public static CreateOrderResponse ToCreate(Order x, bool retryScheduled) => new(x.Id, x.OrderNumber, x.Status, retryScheduled, x.ConcurrencyToken);
    public static OrderDetailsResponse ToDetails(Order x) => new(x.Id, x.OrderNumber, x.CustomerId, x.CartId, x.Currency, x.Status, x.Subtotal, x.DiscountAmount, x.TaxAmount, x.ShippingAmount, x.TotalAmount, x.TotalQuantity, x.DistinctItemCount, x.FailureCode, x.FailureMessage, x.RetryCount, x.NextRetryAtUtc, x.ConfirmedAtUtc, x.CancelledAtUtc, x.CompletedAtUtc, x.FailedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyToken,
        x.Items.OrderBy(i => i.ProductId).Select(i => new OrderItemResponse(i.Id, i.ProductId, i.Sku, i.ProductName, i.ImageUrl, i.UnitPrice, i.Quantity, i.LineTotal, i.InventoryStatus, i.StockDecreasedAtUtc, i.StockRestoredAtUtc)).ToList(),
        x.Addresses.OrderBy(a => a.AddressType).Select(a => new OrderAddressResponse(a.Id, a.AddressType, a.SourceAddressId, a.RecipientName, a.AddressLine1, a.AddressLine2, a.City, a.StateOrProvince, a.PostalCode, a.CountryCode, a.PhoneNumber)).ToList(),
        x.StatusHistory.OrderBy(h => h.CreatedAtUtc).Select(h => new OrderStatusHistoryResponse(h.PreviousStatus, h.NewStatus, h.Reason, h.ChangedBy, h.CreatedAtUtc, h.CorrelationId)).ToList());
    public static OrderStatusResponse ToStatus(Order x) => new(x.Id, x.OrderNumber, x.Status, x.FailureCode, x.RetryCount, x.NextRetryAtUtc, x.UpdatedAtUtc, x.ConcurrencyToken);
}
