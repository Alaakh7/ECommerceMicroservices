using CartService.Application.Interfaces;
using CartService.Application.Services;
using CartService.Domain.Entities;
using CartService.Domain.Enums;
using CartService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CartService.Infrastructure.BackgroundServices;

public sealed class CartExpirationProcessor(
    CartDbContext db,
    IOptions<CartExpirationOptions> expirationOptions,
    IOptions<CartRulesOptions> rulesOptions,
    TimeProvider timeProvider,
    ILogger<CartExpirationProcessor> logger) : ICartExpirationProcessor
{
    private readonly CartExpirationOptions expiration = expirationOptions.Value;
    private readonly CartRulesOptions rules = rulesOptions.Value;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        List<Cart> carts;
        if (string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            // SQLite cannot translate DateTimeOffset ordering/comparison. Keep the compatibility path bounded for relational tests.
            carts = (await db.Carts.Where(x => x.Status == CartStatus.Active || x.Status == CartStatus.CheckoutPending)
                    .Take(expiration.BatchSize * 2).ToListAsync(cancellationToken))
                .Where(x => (x.Status == CartStatus.Active && x.ExpiresAtUtc <= now) ||
                            (x.Status == CartStatus.CheckoutPending && x.CheckoutExpiresAtUtc <= now))
                .OrderBy(x => x.ExpiresAtUtc).Take(expiration.BatchSize).ToList();
        }
        else
        {
            carts = await db.Carts.Where(x =>
                    (x.Status == CartStatus.Active && x.ExpiresAtUtc <= now) ||
                    (x.Status == CartStatus.CheckoutPending && x.CheckoutExpiresAtUtc <= now))
                .OrderBy(x => x.ExpiresAtUtc).Take(expiration.BatchSize).ToListAsync(cancellationToken);
        }
        if (carts.Count == 0) return 0;
        await CartBusinessRules.InTransactionAsync(db, async () =>
        {
            foreach (var cart in carts)
            {
                if (cart.Status == CartStatus.Active) cart.Status = CartStatus.Expired;
                else
                {
                    CartBusinessRules.ClearCheckout(cart);
                    cart.Status = cart.ExpiresAtUtc <= now ? CartStatus.Expired : CartStatus.Active;
                }
                CartBusinessRules.Touch(cart, now, rules, false);
            }
            await db.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Processed {CartCount} expired or checkout-locked carts", carts.Count);
        return carts.Count;
    }
}
