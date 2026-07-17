using System.Data;
using CartService.Application.DTOs.Carts;
using CartService.Application.Interfaces;
using CartService.Application.Models;
using CartService.Common.Exceptions;
using CartService.Domain.Entities;
using CartService.Domain.Enums;
using CartService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CartService.Application.Services;

public sealed class CartApplicationService(
    CartDbContext db,
    ICartRepository carts,
    ICustomerServiceClient customers,
    IOptions<CartRulesOptions> rulesOptions,
    TimeProvider timeProvider,
    ILogger<CartApplicationService> logger) : ICartService
{
    private readonly CartRulesOptions rules = rulesOptions.Value;

    public async Task<CreateCartResponse> CreateOrGetAsync(CreateCartRequest request, CancellationToken cancellationToken)
    {
        var eligibility = await customers.GetEligibilityAsync(request.CustomerId, cancellationToken);
        if (!eligibility.Exists) throw new ExternalResourceNotFoundException("Customer", request.CustomerId);
        if (!eligibility.CanCreateCart) throw new CustomerNotEligibleException("The customer cannot create a cart.");
        var currency = CartBusinessRules.NormalizeCurrency(request.Currency, rules);
        var now = timeProvider.GetUtcNow();

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var open = await carts.GetOpenByCustomerIdAsync(request.CustomerId, true, cancellationToken);
                if (open is not null)
                {
                    if (open.Status == CartStatus.CheckoutPending && open.CheckoutExpiresAtUtc > now)
                        throw new CartLockedException("The customer has a cart with checkout in progress.");

                    if (open.Status == CartStatus.CheckoutPending)
                    {
                        CartBusinessRules.ClearCheckout(open);
                        open.Status = open.ExpiresAtUtc <= now ? CartStatus.Expired : CartStatus.Active;
                        CartBusinessRules.Touch(open, now, rules, false);
                        await SaveAsync(cancellationToken);
                    }

                    if (open.Status == CartStatus.Active && open.ExpiresAtUtc <= now)
                    {
                        open.Status = CartStatus.Expired;
                        CartBusinessRules.Touch(open, now, rules, false);
                        await SaveAsync(cancellationToken);
                    }
                    else if (open.Status == CartStatus.Active)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        logger.LogInformation("Returned existing cart {CartId} for customer {CustomerId}", open.Id, request.CustomerId);
                        return new CreateCartResponse(CartMapper.ToResponse(open), false);
                    }
                }

                var cart = new Cart
                {
                    Id = Guid.NewGuid(), CustomerId = request.CustomerId, Status = CartStatus.Active, Currency = currency,
                    ExpiresAtUtc = now.AddDays(rules.CartExpirationDays), CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid()
                };
                await carts.AddAsync(cart, cancellationToken);
                await SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Created cart {CartId} for customer {CustomerId}", cart.Id, request.CustomerId);
                return new CreateCartResponse(CartMapper.ToResponse(cart), true);
            });
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new DuplicateActiveCartException("The customer already has an active or checkout-pending cart.");
        }
    }

    public async Task<CartResponse> GetByIdAsync(Guid cartId, CancellationToken cancellationToken)
    {
        var cart = await carts.GetByIdAsync(cartId, true, false, cancellationToken) ?? throw new NotFoundException($"Cart '{cartId}' was not found.");
        return CartMapper.ToResponse(cart);
    }

    public async Task<CartResponse> GetActiveByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = await carts.QueryTracked().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.CustomerId == customerId && x.Status == CartStatus.Active, cancellationToken)
            ?? throw new NotFoundException($"No active cart was found for customer '{customerId}'.");
        var now = timeProvider.GetUtcNow();
        if (cart.ExpiresAtUtc <= now)
        {
            cart.Status = CartStatus.Expired;
            CartBusinessRules.Touch(cart, now, rules, false);
            await SaveAsync(cancellationToken);
            throw new NotFoundException($"No active cart was found for customer '{customerId}'.");
        }
        return CartMapper.ToResponse(cart);
    }

    public Task<PagedResponse<CartSummaryResponse>> GetCustomerHistoryAsync(Guid customerId, CartQueryParameters query, CancellationToken cancellationToken) =>
        carts.GetCustomerHistoryAsync(customerId, query, cancellationToken);

    public async Task<CartResponse> AbandonAsync(Guid cartId, AbandonCartRequest request, CancellationToken cancellationToken)
    {
        var cart = await carts.GetByIdAsync(cartId, true, true, cancellationToken) ?? throw new NotFoundException($"Cart '{cartId}' was not found.");
        CartBusinessRules.RequireActive(cart);
        CartBusinessRules.EnsureConcurrency(cart.ConcurrencyToken, request.ConcurrencyToken);
        var now = timeProvider.GetUtcNow();
        await CartBusinessRules.InTransactionAsync(db, async () =>
        {
            cart.Status = CartStatus.Abandoned;
            cart.AbandonedAtUtc = now;
            CartBusinessRules.Touch(cart, now, rules, false);
            await SaveAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Abandoned cart {CartId}; reason supplied: {ReasonSupplied}", cartId, !string.IsNullOrWhiteSpace(request.Reason));
        return CartMapper.ToResponse(cart);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await carts.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("The cart was changed by another request. Refresh it and retry."); }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
