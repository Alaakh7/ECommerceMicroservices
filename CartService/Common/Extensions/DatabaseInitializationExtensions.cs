using CartService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CartService.Common.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeCartDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!options.ApplyMigrationsOnStartup) return;
        var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
        Exception? last = null;
        for (var attempt = 1; attempt <= Math.Max(1, options.MigrationRetryCount); attempt++)
        {
            try
            {
                logger.LogInformation("Applying CartService migrations (attempt {Attempt}/{MaximumAttempts})", attempt, options.MigrationRetryCount);
                await db.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < options.MigrationRetryCount)
            {
                last = exception;
                logger.LogWarning(exception, "CartService migration attempt {Attempt} failed", attempt);
                await Task.Delay(TimeSpan.FromSeconds(options.MigrationRetryDelaySeconds), cancellationToken);
            }
            catch (Exception exception) { last = exception; break; }
        }
        throw new InvalidOperationException("CartService database migrations failed after all retry attempts.", last);
    }
}
