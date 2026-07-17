using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OrderService.Application.DTOs.ExternalServices;
using OrderService.Application.DTOs.Orders;
using OrderService.Common.Exceptions;
using OrderService.Domain.Enums;

namespace OrderService.Application.Services;

public sealed class OrderNumberGenerator(TimeProvider timeProvider)
{
    public string Generate() => $"ORD-{timeProvider.GetUtcNow():yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
}

public sealed record OrderTotals(decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal ShippingAmount, decimal TotalAmount, int TotalQuantity, int DistinctItemCount);
public sealed class OrderTotalsCalculator
{
    public OrderTotals Calculate(IEnumerable<CartCheckoutItemExternalResponse> items, decimal discount = 0, decimal tax = 0, decimal shipping = 0)
    {
        var list = items.ToList();
        if (list.Count == 0) throw new ValidationException("An order must contain at least one item.");
        if (list.Any(x => x.Quantity <= 0 || x.UnitPrice <= 0)) throw new ValidationException("Item price and quantity must be positive.");
        if (list.GroupBy(x => x.ProductId).Any(x => x.Count() > 1)) throw new ValidationException("Duplicate product IDs are not allowed.");
        if (discount < 0 || tax < 0 || shipping < 0) throw new ValidationException("Order amounts cannot be negative.");
        var subtotal = list.Sum(x => decimal.Round(x.UnitPrice * x.Quantity, 2));
        return new(subtotal, discount, tax, shipping, subtotal - discount + tax + shipping, list.Sum(x => x.Quantity), list.Count);
    }
}

public sealed class OrderStatusTransitionValidator
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Allowed = new Dictionary<OrderStatus, OrderStatus[]>
    {
        [OrderStatus.PendingConfirmation] = [OrderStatus.InventoryProcessing, OrderStatus.Failed],
        [OrderStatus.InventoryProcessing] = [OrderStatus.CartCompletionPending, OrderStatus.Failed, OrderStatus.Cancelled],
        [OrderStatus.CartCompletionPending] = [OrderStatus.Confirmed, OrderStatus.Failed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Cancelled, OrderStatus.Completed],
        [OrderStatus.Cancelled] = [], [OrderStatus.Failed] = [], [OrderStatus.Completed] = []
    };
    public bool CanTransition(OrderStatus from, OrderStatus to) => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
    public void EnsureAllowed(OrderStatus from, OrderStatus to)
    {
        if (!CanTransition(from, to)) throw new InvalidOrderStateException($"Order cannot transition from {from} to {to}.");
    }
}

public sealed class RequestHashService
{
    public string ForCreate(CreateOrderRequest request) => Hash(new { request.CartId, OperationId = request.OperationId.Trim(), request.AcceptPriceChanges, request.ShippingAddressId, request.BillingAddressId, request.UseShippingAddressForBilling });
    public string ForCancel(Guid orderId, CancelOrderRequest request) => Hash(new { orderId, OperationId = request.OperationId.Trim(), Reason = request.Reason.Trim() });
    public string ForComplete(Guid orderId, CompleteOrderRequest request) => Hash(new { orderId, OperationId = request.OperationId.Trim(), Reason = request.Reason?.Trim() });
    public string ForRetry(Guid orderId, RetryOrderRequest request) => Hash(new { orderId, OperationId = request.OperationId.Trim() });
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)))));
}
