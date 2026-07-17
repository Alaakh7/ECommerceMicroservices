using CartService.Application.DTOs.CartItems;
using CartService.Application.DTOs.ExternalServices;
using CartService.Application.Interfaces;
using CartService.Domain.Entities;

namespace CartService.Application.Services;

public sealed record CartExternalValidationResult(
    CustomerEligibilityExternalResponse Customer,
    IReadOnlyDictionary<Guid, ProductExternalResponse?> Products,
    IReadOnlyDictionary<Guid, BatchProductAvailabilityExternalItemResponse> Availability,
    IReadOnlyList<CartValidationItemResponse> Items,
    IReadOnlyList<CartPriceChangeResponse> PriceChanges,
    IReadOnlyList<CartAvailabilityIssueResponse> AvailabilityIssues,
    decimal CurrentSubtotal,
    bool CustomerEligible,
    bool IsValid);

public sealed class CartValidationCoordinator(ICustomerServiceClient customers, IProductServiceClient products)
{
    public async Task<CartExternalValidationResult> ValidateAsync(Cart cart, bool requireDefaultShippingAddress, CancellationToken cancellationToken)
    {
        var customerTask = customers.GetEligibilityAsync(cart.CustomerId, cancellationToken);
        if (cart.Items.Count == 0)
        {
            var emptyCustomer = await customerTask;
            var eligible = emptyCustomer.Exists && emptyCustomer.CanPlaceOrder && (!requireDefaultShippingAddress || emptyCustomer.HasDefaultShippingAddress);
            return new(emptyCustomer, new Dictionary<Guid, ProductExternalResponse?>(), new Dictionary<Guid, BatchProductAvailabilityExternalItemResponse>(),
                [], [], [], 0m, eligible, false);
        }

        var batchRequest = new BatchProductAvailabilityExternalRequest
        {
            Items = cart.Items.Select(x => new BatchProductAvailabilityExternalItemRequest { ProductId = x.ProductId, Quantity = x.Quantity }).ToList()
        };
        var availabilityTask = products.CheckBatchAvailabilityAsync(batchRequest, cancellationToken);
        var productTasks = cart.Items.ToDictionary(x => x.ProductId, x => GetProductOrNullAsync(x.ProductId, cancellationToken));
        await Task.WhenAll(productTasks.Values.Append<Task>(customerTask).Append(availabilityTask));

        var customer = await customerTask;
        var availability = (await availabilityTask).Items.ToDictionary(x => x.ProductId);
        var productMap = productTasks.ToDictionary(x => x.Key, x => x.Value.Result);
        var itemResults = new List<CartValidationItemResponse>();
        var priceChanges = new List<CartPriceChangeResponse>();
        var issues = new List<CartAvailabilityIssueResponse>();
        decimal currentSubtotal = 0;

        foreach (var item in cart.Items)
        {
            productMap.TryGetValue(item.ProductId, out var product);
            availability.TryGetValue(item.ProductId, out var stock);
            var exists = product is not null && (stock?.Exists ?? true);
            var active = exists && product!.IsActive && (stock?.IsActive ?? true);
            var availableQuantity = stock?.AvailableQuantity ?? product?.StockQuantity ?? 0;
            var isAvailable = exists && active && availableQuantity >= item.Quantity && (stock?.IsAvailable ?? true);
            var changed = product is not null && product.Price != item.UnitPrice;
            if (changed) priceChanges.Add(new(item.ProductId, item.UnitPrice, product!.Price));
            if (!exists) issues.Add(new(item.ProductId, "product_not_found", "The product no longer exists.", item.Quantity, 0));
            else if (!active) issues.Add(new(item.ProductId, "product_inactive", "The product is inactive.", item.Quantity, availableQuantity));
            else if (!isAvailable) issues.Add(new(item.ProductId, "insufficient_stock", "The requested quantity is unavailable.", item.Quantity, availableQuantity));
            if (product is not null) currentSubtotal += decimal.Round(product.Price * item.Quantity, 2, MidpointRounding.AwayFromZero);
            itemResults.Add(new(item.ProductId, exists, active, isAvailable, item.Quantity, availableQuantity, item.UnitPrice,
                product?.Price, changed));
        }

        var customerEligible = customer.Exists && customer.CanPlaceOrder && (!requireDefaultShippingAddress || customer.HasDefaultShippingAddress);
        return new(customer, productMap, availability, itemResults, priceChanges, issues,
            decimal.Round(currentSubtotal, 2, MidpointRounding.AwayFromZero), customerEligible,
            customerEligible && issues.Count == 0 && cart.Items.Count > 0);
    }

    private async Task<ProductExternalResponse?> GetProductOrNullAsync(Guid productId, CancellationToken cancellationToken)
    {
        try { return await products.GetProductByIdAsync(productId, cancellationToken); }
        catch (Common.Exceptions.ExternalResourceNotFoundException) { return null; }
    }
}
