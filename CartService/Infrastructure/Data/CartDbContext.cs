using CartService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CartService.Infrastructure.Data;

public sealed class CartDbContext(DbContextOptions<CartDbContext> options) : DbContext(options)
{
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartDbContext).Assembly);
        if (!string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal)) return;

        // SQLite stores DateTimeOffset as text so relational integration tests can compare and order UTC timestamps.
        var cart = modelBuilder.Entity<Cart>();
        cart.Property(x => x.ExpiresAtUtc).HasConversion<string>();
        cart.Property(x => x.CheckoutExpiresAtUtc).HasConversion<string>();
        cart.Property(x => x.CheckedOutAtUtc).HasConversion<string>();
        cart.Property(x => x.AbandonedAtUtc).HasConversion<string>();
        cart.Property(x => x.CreatedAtUtc).HasConversion<string>();
        cart.Property(x => x.UpdatedAtUtc).HasConversion<string>();
        var item = modelBuilder.Entity<CartItem>();
        item.Property(x => x.ProductUpdatedAtUtc).HasConversion<string>();
        item.Property(x => x.CreatedAtUtc).HasConversion<string>();
        item.Property(x => x.UpdatedAtUtc).HasConversion<string>();
    }
}
