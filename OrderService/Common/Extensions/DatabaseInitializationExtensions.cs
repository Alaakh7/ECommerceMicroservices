using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderService.Infrastructure.Data;

namespace OrderService.Common.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeOrderDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!options.ApplyMigrationsOnStartup) return;
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
        Exception? last = null;
        for (var attempt = 1; attempt <= options.MigrationRetryCount; attempt++)
        {
            try { logger.LogInformation("Applying OrderService migrations (attempt {Attempt}/{MaximumAttempts})", attempt, options.MigrationRetryCount); await db.Database.MigrateAsync(cancellationToken); return; }
            catch (Exception exception) when (attempt < options.MigrationRetryCount) { last = exception; logger.LogWarning(exception, "OrderService migration attempt {Attempt} failed", attempt); await Task.Delay(TimeSpan.FromSeconds(options.MigrationRetryDelaySeconds), cancellationToken); }
            catch (Exception exception) { last = exception; break; }
        }
        throw new InvalidOperationException("OrderService database migrations failed after all retry attempts.", last);
    }
}
