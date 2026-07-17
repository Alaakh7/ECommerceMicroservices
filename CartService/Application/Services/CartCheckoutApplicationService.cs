using CartService.Application.DTOs.Checkout;
using CartService.Application.DTOs.Carts;
using CartService.Application.DTOs.CartItems;
using CartService.Application.Interfaces;
using CartService.Application.Models;
using CartService.Common.Exceptions;
using CartService.Domain.Enums;
using CartService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CartService.Application.Services;

public sealed class CartCheckoutApplicationService(
    CartDbContext db,
    ICartRepository carts,
    CartValidationCoordinator validator,
    IOptions<CartRulesOptions> rulesOptions,
    TimeProvider timeProvider,
    ILogger<CartCheckoutApplicationService> logger) : ICartCheckoutService
{
    private readonly CartRulesOptions rules = rulesOptions.Value;

    public async Task<PrepareCheckoutResponse> PrepareAsync(Guid cartId, PrepareCheckoutRequest request, CancellationToken cancellationToken)
    {
        var operationId = request.OperationId.Trim();
        var cart = await carts.GetByIdAsync(cartId, true, true, cancellationToken) ?? throw new NotFoundException($"Cart '{cartId}' was not found.");
        var now = timeProvider.GetUtcNow();

        if (cart.Status == CartStatus.CheckoutPending && string.Equals(cart.CheckoutOperationId, operationId, StringComparison.Ordinal))
        {
            if (cart.CheckoutExpiresAtUtc > now && cart.CheckoutToken.HasValue)
                return ToPrepareResponse(cart, true);
            CartBusinessRules.ClearCheckout(cart);
            cart.Status = cart.ExpiresAtUtc <= now ? CartStatus.Expired : CartStatus.Active;
            CartBusinessRules.Touch(cart, now, rules, false);
            await SaveAsync(cancellationToken);
        }

        if (await carts.Query().AnyAsync(x => x.Id != cartId && x.CheckoutOperationId == operationId, cancellationToken))
            throw new ConflictException($"Checkout operation '{operationId}' is already associated with another cart.");
        CartBusinessRules.RequireActive(cart);
        CartBusinessRules.EnsureConcurrency(cart.ConcurrencyToken, request.ConcurrencyToken);
        if (cart.Items.Count == 0) throw new CartEmptyException("The cart is empty.");

        var validation = await validator.ValidateAsync(cart, true, cancellationToken);
        if (!validation.Customer.Exists) throw new ExternalResourceNotFoundException("Customer", cart.CustomerId);
        if (!validation.Customer.CanPlaceOrder || !validation.Customer.HasDefaultShippingAddress)
            throw new CustomerNotEligibleException("The customer cannot place an order or has no default shipping address.");
        ThrowForAvailabilityIssue(validation.AvailabilityIssues);
        if (validation.PriceChanges.Count > 0 && !request.AcceptPriceChanges)
            throw new PriceChangedException(validation.PriceChanges);

        await CartBusinessRules.InTransactionAsync(db, async () =>
        {
            if (request.AcceptPriceChanges)
            {
                foreach (var item in cart.Items)
                {
                    if (!validation.Products.TryGetValue(item.ProductId, out var product) || product is null) continue;
                    if (item.UnitPrice != product.Price)
                    {
                        item.UnitPrice = product.Price;
                        item.UpdatedAtUtc = now;
                        item.ConcurrencyToken = Guid.NewGuid();
                    }
                    item.Sku = product.Sku;
                    item.ProductName = product.Name;
                    item.ImageUrl = product.ImageUrl;
                    item.ProductConcurrencyToken = product.ConcurrencyToken;
                    item.ProductUpdatedAtUtc = product.UpdatedAtUtc;
                }
                CartBusinessRules.Recalculate(cart);
            }
            cart.Status = CartStatus.CheckoutPending;
            cart.CheckoutToken = Guid.NewGuid();
            cart.CheckoutOperationId = operationId;
            cart.CheckoutExpiresAtUtc = now.AddMinutes(rules.CheckoutLockMinutes);
            CartBusinessRules.Touch(cart, now, rules, false);
            await SaveAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Prepared cart {CartId} for checkout operation {OperationId}", cartId, operationId);
        return ToPrepareResponse(cart, false);
    }

    public async Task<CompleteCheckoutResponse> CompleteAsync(Guid cartId, CompleteCheckoutRequest request, CancellationToken cancellationToken)
    {
        var operationId = request.OperationId.Trim();
        var cart = await carts.GetByIdAsync(cartId, false, true, cancellationToken) ?? throw new NotFoundException($"Cart '{cartId}' was not found.");
        if (cart.Status == CartStatus.CheckedOut)
        {
            if (cart.CompletedOrderId == request.OrderId && string.Equals(cart.CheckoutOperationId, operationId, StringComparison.Ordinal) && cart.CheckedOutAtUtc.HasValue)
                return new(cart.Id, request.OrderId, cart.CheckedOutAtUtc.Value, true);
            throw new ConflictException("The cart was already checked out with different order details.");
        }
        if (cart.Status != CartStatus.CheckoutPending) throw new CartNotActiveException("The cart is not awaiting checkout completion.");
        ValidateCheckoutIdentity(cart.CheckoutToken, cart.CheckoutOperationId, request.CheckoutToken, operationId);
        var now = timeProvider.GetUtcNow();
        if (cart.CheckoutExpiresAtUtc <= now) throw new CheckoutExpiredException("The checkout lock has expired.");
        await CartBusinessRules.InTransactionAsync(db, async () =>
        {
            cart.Status = CartStatus.CheckedOut;
            cart.CompletedOrderId = request.OrderId;
            cart.CheckedOutAtUtc = now;
            cart.CheckoutToken = null;
            cart.CheckoutExpiresAtUtc = null;
            CartBusinessRules.Touch(cart, now, rules, false);
            await SaveAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Completed checkout for cart {CartId}, order {OrderId}, operation {OperationId}", cartId, request.OrderId, operationId);
        return new(cart.Id, request.OrderId, now, false);
    }

    public async Task<CartResponse> CancelAsync(Guid cartId, CancelCheckoutRequest request, CancellationToken cancellationToken)
    {
        var operationId = request.OperationId.Trim();
        var cart = await carts.GetByIdAsync(cartId, true, true, cancellationToken) ?? throw new NotFoundException($"Cart '{cartId}' was not found.");
        if (cart.Status != CartStatus.CheckoutPending) throw new CartNotActiveException("The cart is not awaiting checkout cancellation.");
        ValidateCheckoutIdentity(cart.CheckoutToken, cart.CheckoutOperationId, request.CheckoutToken, operationId);
        var now = timeProvider.GetUtcNow();
        await CartBusinessRules.InTransactionAsync(db, async () =>
        {
            cart.Status = cart.ExpiresAtUtc <= now ? CartStatus.Expired : CartStatus.Active;
            CartBusinessRules.ClearCheckout(cart);
            if (cart.Status == CartStatus.Active) cart.ExpiresAtUtc = now.AddDays(rules.CartExpirationDays);
            CartBusinessRules.Touch(cart, now, rules, false);
            await SaveAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Cancelled checkout for cart {CartId}, operation {OperationId}; reason supplied: {ReasonSupplied}", cartId, operationId, !string.IsNullOrWhiteSpace(request.Reason));
        return CartMapper.ToResponse(cart);
    }

    private static void ValidateCheckoutIdentity(Guid? actualToken, string? actualOperation, Guid suppliedToken, string suppliedOperation)
    {
        if (!actualToken.HasValue || actualToken.Value != suppliedToken)
            throw new InvalidCheckoutTokenException("The checkout token is invalid.");
        if (!string.Equals(actualOperation, suppliedOperation, StringComparison.Ordinal))
            throw new ConflictException("The checkout operation ID does not match the cart.");
    }

    private static void ThrowForAvailabilityIssue(IReadOnlyList<CartAvailabilityIssueResponse> issues)
    {
        var issue = issues.FirstOrDefault();
        if (issue is null) return;
        if (issue.Code == "product_not_found") throw new ExternalResourceNotFoundException("Product", issue.ProductId);
        if (issue.Code == "product_inactive") throw new ProductUnavailableException($"Product '{issue.ProductId}' is inactive.");
        throw new InsufficientStockException(issue.ProductId, issue.RequestedQuantity, issue.AvailableQuantity);
    }

    private static PrepareCheckoutResponse ToPrepareResponse(CartService.Domain.Entities.Cart cart, bool alreadyPrepared) => new(
        cart.Id, cart.CustomerId, cart.CheckoutToken!.Value, cart.CheckoutOperationId!, cart.CheckoutExpiresAtUtc!.Value,
        cart.Currency, cart.Subtotal, cart.TotalQuantity, CartMapper.ToSnapshot(cart), alreadyPrepared);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await carts.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("The cart was changed by another request. Refresh it and retry."); }
        catch (DbUpdateException) { throw new ConflictException("The checkout operation conflicts with an existing operation."); }
    }
}
