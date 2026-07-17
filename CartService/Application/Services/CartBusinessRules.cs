using System.Data.Common;
using CartService.Common.Exceptions;
using CartService.Domain.Entities;
using CartService.Domain.Enums;
using CartService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartService.Application.Services;

internal static class CartBusinessRules
{
    public static void RequireActive(Cart cart)
    {
        if (cart.Status == CartStatus.CheckoutPending) throw new CartLockedException("The cart is locked for checkout.");
        if (cart.Status != CartStatus.Active) throw new CartNotActiveException($"Cart '{cart.Id}' is not active.");
    }

    public static void EnsureConcurrency(Guid actual, Guid supplied)
    {
        if (supplied == Guid.Empty || actual != supplied)
            throw new ConcurrencyConflictException("The supplied cart concurrency token is stale or missing.");
    }

    public static void EnsureItemConcurrency(Guid actual, Guid supplied)
    {
        if (supplied == Guid.Empty || actual != supplied)
            throw new ConcurrencyConflictException("The supplied cart-item concurrency token is stale or missing.");
    }

    public static void Recalculate(Cart cart)
    {
        foreach (var item in cart.Items) item.LineTotal = decimal.Round(item.UnitPrice * item.Quantity, 2, MidpointRounding.AwayFromZero);
        cart.Subtotal = decimal.Round(cart.Items.Sum(x => x.LineTotal), 2, MidpointRounding.AwayFromZero);
        cart.TotalQuantity = cart.Items.Sum(x => x.Quantity);
        cart.DistinctItemCount = cart.Items.Count;
    }

    public static void Touch(Cart cart, DateTimeOffset now, CartRulesOptions rules, bool refreshExpiration = true)
    {
        cart.UpdatedAtUtc = now;
        cart.ConcurrencyToken = Guid.NewGuid();
        if (refreshExpiration && rules.RefreshCartExpirationOnModification && cart.Status == CartStatus.Active)
            cart.ExpiresAtUtc = now.AddDays(rules.CartExpirationDays);
    }

    public static void ClearCheckout(Cart cart)
    {
        cart.CheckoutToken = null;
        cart.CheckoutOperationId = null;
        cart.CheckoutExpiresAtUtc = null;
    }

    public static string NormalizeCurrency(string? currency, CartRulesOptions rules)
    {
        var value = string.IsNullOrWhiteSpace(currency) ? rules.DefaultCurrency : currency.Trim().ToUpperInvariant();
        if (value.Length != 3 || value.Any(x => x is < 'A' or > 'Z'))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["currency"] = ["currency must be three uppercase letters."] });
        if (!string.Equals(value, rules.DefaultCurrency, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["currency"] = [$"Only {rules.DefaultCurrency} is currently supported."] });
        return value;
    }

    public static async Task<T> InTransactionAsync<T>(CartDbContext db, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }
        catch (Exception exception) when (
            string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal) &&
            exception is DbException or InvalidOperationException)
        {
            throw new ConcurrencyConflictException("The cart is being changed by another request. Retry with the latest concurrency token.");
        }
    }

    public static async Task InTransactionAsync(CartDbContext db, Func<Task> operation, CancellationToken cancellationToken) =>
        await InTransactionAsync(db, async () => { await operation(); return true; }, cancellationToken);
}
