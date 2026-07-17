using CartService.Application.DTOs.CartItems;
using CartService.Application.DTOs.Carts;
using CartService.Application.Interfaces;
using CartService.Application.Models;
using CartService.Common.Exceptions;
using CartService.Domain.Entities;
using CartService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CartService.Application.Services;

public sealed class CartItemApplicationService(
    CartDbContext db,
    ICartRepository carts,
    ICartItemRepository items,
    IProductServiceClient products,
    CartValidationCoordinator validator,
    IOptions<CartRulesOptions> rulesOptions,
    TimeProvider timeProvider,
    ILogger<CartItemApplicationService> logger) : ICartItemService
{
    private readonly CartRulesOptions rules = rulesOptions.Value;

    public async Task<CartResponse> AddAsync(Guid cartId, AddCartItemRequest request, CancellationToken cancellationToken)
    {
        ValidateQuantity(request.Quantity);
        var cart = await GetTrackedAsync(cartId, cancellationToken);
        CartBusinessRules.RequireActive(cart);
        if (request.ExpectedCartConcurrencyToken.HasValue)
            CartBusinessRules.EnsureConcurrency(cart.ConcurrencyToken, request.ExpectedCartConcurrencyToken.Value);

        var existing = cart.Items.SingleOrDefault(x => x.ProductId == request.ProductId);
        var quantity = checked((existing?.Quantity ?? 0) + request.Quantity);
        ValidateQuantity(quantity);
        if (existing is null && cart.Items.Count >= rules.MaximumDistinctItems)
            throw Validation("productId", $"A cart cannot contain more than {rules.MaximumDistinctItems} distinct products.");

        var productTask = products.GetProductByIdAsync(request.ProductId, cancellationToken);
        var availabilityTask = products.CheckAvailabilityAsync(request.ProductId, quantity, cancellationToken);
        await Task.WhenAll(productTask, availabilityTask);
        var product = await productTask;
        var availability = await availabilityTask;
        EnsureProductAvailable(product.IsActive && availability.IsActive, request.ProductId, quantity, availability.AvailableQuantity, availability.IsAvailable);
        var now = timeProvider.GetUtcNow();

        try
        {
            await CartBusinessRules.InTransactionAsync(db, async () =>
            {
                if (existing is null)
                {
                    existing = new CartItem
                    {
                        Id = Guid.NewGuid(), CartId = cart.Id, Cart = cart, ProductId = product.Id, Sku = product.Sku,
                        ProductName = product.Name, ImageUrl = product.ImageUrl, UnitPrice = product.Price, Quantity = quantity,
                        ProductConcurrencyToken = product.ConcurrencyToken, ProductUpdatedAtUtc = product.UpdatedAtUtc,
                        CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid()
                    };
                    cart.Items.Add(existing);
                    await items.AddAsync(existing, cancellationToken);
                }
                else
                {
                    existing.Quantity = quantity;
                    existing.Sku = product.Sku;
                    existing.ProductName = product.Name;
                    existing.ImageUrl = product.ImageUrl;
                    existing.UnitPrice = product.Price;
                    existing.ProductConcurrencyToken = product.ConcurrencyToken;
                    existing.ProductUpdatedAtUtc = product.UpdatedAtUtc;
                    existing.UpdatedAtUtc = now;
                    existing.ConcurrencyToken = Guid.NewGuid();
                }
                CartBusinessRules.Recalculate(cart);
                CartBusinessRules.Touch(cart, now, rules);
                await SaveAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConcurrencyConflictException("The same product was added by another request. Refresh the cart and retry.");
        }
        logger.LogInformation("Added product {ProductId} to cart {CartId}; resulting quantity {Quantity}", request.ProductId, cartId, quantity);
        return CartMapper.ToResponse(cart);
    }

    public async Task<CartResponse> UpdateQuantityAsync(Guid cartId, Guid productId, UpdateCartItemQuantityRequest request, CancellationToken cancellationToken)
    {
        ValidateQuantity(request.Quantity);
        var cart = await GetTrackedAsync(cartId, cancellationToken);
        CartBusinessRules.RequireActive(cart);
        CartBusinessRules.EnsureConcurrency(cart.ConcurrencyToken, request.CartConcurrencyToken);
        var item = cart.Items.SingleOrDefault(x => x.ProductId == productId) ?? throw new NotFoundException($"Product '{productId}' is not in cart '{cartId}'.");
        CartBusinessRules.EnsureItemConcurrency(item.ConcurrencyToken, request.ItemConcurrencyToken);
        var availability = await products.CheckAvailabilityAsync(productId, request.Quantity, cancellationToken);
        EnsureProductAvailable(availability.IsActive, productId, request.Quantity, availability.AvailableQuantity, availability.IsAvailable);
        var now = timeProvider.GetUtcNow();
        await CartBusinessRules.InTransactionAsync(db, async () =>
        {
            item.Quantity = request.Quantity;
            item.UpdatedAtUtc = now;
            item.ConcurrencyToken = Guid.NewGuid();
            CartBusinessRules.Recalculate(cart);
            CartBusinessRules.Touch(cart, now, rules);
            await SaveAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Updated product {ProductId} quantity in cart {CartId} to {Quantity}", productId, cartId, request.Quantity);
        return CartMapper.ToResponse(cart);
    }

    public async Task RemoveAsync(Guid cartId, Guid productId, Guid concurrencyToken, CancellationToken cancellationToken)
    {
        var cart = await GetTrackedAsync(cartId, cancellationToken);
        CartBusinessRules.RequireActive(cart);
        CartBusinessRules.EnsureConcurrency(cart.ConcurrencyToken, concurrencyToken);
        var item = cart.Items.SingleOrDefault(x => x.ProductId == productId) ?? throw new NotFoundException($"Product '{productId}' is not in cart '{cartId}'.");
        var now = timeProvider.GetUtcNow();
        await CartBusinessRules.InTransactionAsync(db, async () =>
        {
            items.Remove(item);
            cart.Items.Remove(item);
            CartBusinessRules.Recalculate(cart);
            CartBusinessRules.Touch(cart, now, rules);
            await SaveAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Removed product {ProductId} from cart {CartId}", productId, cartId);
    }

    public async Task ClearAsync(Guid cartId, Guid concurrencyToken, CancellationToken cancellationToken)
    {
        var cart = await GetTrackedAsync(cartId, cancellationToken);
        CartBusinessRules.RequireActive(cart);
        CartBusinessRules.EnsureConcurrency(cart.ConcurrencyToken, concurrencyToken);
        var now = timeProvider.GetUtcNow();
        await CartBusinessRules.InTransactionAsync(db, async () =>
        {
            items.RemoveRange(cart.Items);
            cart.Items.Clear();
            CartBusinessRules.Recalculate(cart);
            CartBusinessRules.Touch(cart, now, rules);
            await SaveAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Cleared cart {CartId}", cartId);
    }

    public async Task<RefreshCartResponse> RefreshAsync(Guid cartId, RefreshCartRequest request, CancellationToken cancellationToken)
    {
        var cart = await GetTrackedAsync(cartId, cancellationToken);
        CartBusinessRules.RequireActive(cart);
        CartBusinessRules.EnsureConcurrency(cart.ConcurrencyToken, request.ConcurrencyToken);
        var validation = await validator.ValidateAsync(cart, false, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var changed = false;
        foreach (var item in cart.Items)
        {
            if (!validation.Products.TryGetValue(item.ProductId, out var product) || product is null) continue;
            var metadataChanged = item.Sku != product.Sku || item.ProductName != product.Name || item.ImageUrl != product.ImageUrl ||
                                  item.ProductConcurrencyToken != product.ConcurrencyToken || item.ProductUpdatedAtUtc != product.UpdatedAtUtc;
            var priceChanged = request.UpdatePrices && item.UnitPrice != product.Price;
            if (!metadataChanged && !priceChanged) continue;
            item.Sku = product.Sku;
            item.ProductName = product.Name;
            item.ImageUrl = product.ImageUrl;
            item.ProductConcurrencyToken = product.ConcurrencyToken;
            item.ProductUpdatedAtUtc = product.UpdatedAtUtc;
            if (request.UpdatePrices) item.UnitPrice = product.Price;
            item.UpdatedAtUtc = now;
            item.ConcurrencyToken = Guid.NewGuid();
            changed = true;
        }
        if (changed)
        {
            await CartBusinessRules.InTransactionAsync(db, async () =>
            {
                CartBusinessRules.Recalculate(cart);
                CartBusinessRules.Touch(cart, now, rules);
                await SaveAsync(cancellationToken);
            }, cancellationToken);
        }
        logger.LogInformation("Refreshed cart {CartId}; updated {WasUpdated}; price changes {PriceChangeCount}", cartId, changed, validation.PriceChanges.Count);
        return new(CartMapper.ToResponse(cart), validation.PriceChanges, validation.AvailabilityIssues,
            validation.Items.Where(x => !x.Exists).Select(x => x.ProductId).ToList(), changed);
    }

    public async Task<ValidateCartResponse> ValidateAsync(Guid cartId, ValidateCartRequest request, CancellationToken cancellationToken)
    {
        var cart = await carts.GetByIdAsync(cartId, true, false, cancellationToken) ?? throw new NotFoundException($"Cart '{cartId}' was not found.");
        var result = await validator.ValidateAsync(cart, request.RequireDefaultShippingAddress, cancellationToken);
        return new(result.IsValid, result.CustomerEligible, result.Items, result.PriceChanges, result.AvailabilityIssues,
            result.CurrentSubtotal, cart.Subtotal, timeProvider.GetUtcNow());
    }

    private async Task<Cart> GetTrackedAsync(Guid cartId, CancellationToken cancellationToken) =>
        await carts.GetByIdAsync(cartId, true, true, cancellationToken) ?? throw new NotFoundException($"Cart '{cartId}' was not found.");

    private void ValidateQuantity(int quantity)
    {
        if (quantity is < 1 || quantity > rules.MaximumQuantityPerItem)
            throw Validation("quantity", $"quantity must be between 1 and {rules.MaximumQuantityPerItem}.");
    }

    private static void EnsureProductAvailable(bool isActive, Guid productId, int requested, int available, bool isAvailable)
    {
        if (!isActive) throw new ProductUnavailableException($"Product '{productId}' is inactive.");
        if (!isAvailable || available < requested) throw new InsufficientStockException(productId, requested, available);
    }

    private static ValidationException Validation(string field, string message) =>
        new("Validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await carts.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("The cart was changed by another request. Refresh it and retry."); }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
