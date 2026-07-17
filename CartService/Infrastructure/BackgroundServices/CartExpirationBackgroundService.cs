using CartService.Application.Interfaces;
using CartService.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace CartService.Infrastructure.BackgroundServices;

public sealed class CartExpirationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<CartExpirationOptions> options,
    ILogger<CartExpirationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.CheckIntervalMinutes));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ICartExpirationProcessor>();
                while (await processor.ProcessBatchAsync(stoppingToken) >= options.Value.BatchSize) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Cart expiration cycle failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
