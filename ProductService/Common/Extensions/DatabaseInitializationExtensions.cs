using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProductService.Domain.Entities;
using ProductService.Domain.Enums;
using ProductService.Infrastructure.Data;

namespace ProductService.Common.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeProductDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");

        if (options.ApplyMigrationsOnStartup)
        {
            Exception? last = null;
            for (var attempt = 1; attempt <= Math.Max(1, options.MigrationRetryCount); attempt++)
            {
                try
                {
                    logger.LogInformation("Applying database migrations (attempt {Attempt}/{MaximumAttempts})", attempt, options.MigrationRetryCount);
                    await db.Database.MigrateAsync(cancellationToken);
                    last = null;
                    break;
                }
                catch (Exception ex) when (attempt < options.MigrationRetryCount)
                {
                    last = ex;
                    logger.LogWarning(ex, "Database migration attempt {Attempt} failed", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(options.MigrationRetryDelaySeconds), cancellationToken);
                }
            }
            if (last is not null) throw new InvalidOperationException("Database migrations failed after all retry attempts.", last);
        }

        if (options.SeedData && !app.Environment.IsProduction()) await SeedAsync(db, cancellationToken);
    }

    private static async Task SeedAsync(ProductDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var seeds = new[]
        {
            new { Name = "Electronics", Slug = "electronics" },
            new { Name = "Computers", Slug = "computers" },
            new { Name = "Home Appliances", Slug = "home-appliances" }
        };
        foreach (var seed in seeds)
        {
            if (!await db.Categories.AnyAsync(x => x.Slug == seed.Slug, cancellationToken))
                db.Categories.Add(new Category { Id = Guid.NewGuid(), Name = seed.Name, Slug = seed.Slug, IsActive = true, CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid() });
        }
        await db.SaveChangesAsync(cancellationToken);

        var computersId = await db.Categories.Where(x => x.Slug == "computers").Select(x => x.Id).SingleAsync(cancellationToken);
        if (!await db.Products.AnyAsync(x => x.Sku == "LAPTOP-DELL-001", cancellationToken))
        {
            var product = new Product { Id = Guid.NewGuid(), Sku = "LAPTOP-DELL-001", Name = "Dell Latitude Laptop", Description = "Business laptop", Price = 950m, StockQuantity = 15, ReorderLevel = 3, CategoryId = computersId, Category = null!, IsActive = true, CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid() };
            db.Products.Add(product);
            db.InventoryTransactions.Add(new InventoryTransaction { Id = Guid.NewGuid(), ProductId = product.Id, Product = product, OperationId = $"seed-initial:{product.Id:N}", OperationType = InventoryOperationType.Increase, Quantity = 15, StockBefore = 0, StockAfter = 15, Reason = "Seed initial stock", CreatedAtUtc = now });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
