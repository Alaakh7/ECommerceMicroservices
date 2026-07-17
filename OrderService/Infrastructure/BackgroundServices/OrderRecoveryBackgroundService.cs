using Microsoft.Extensions.Options;
using OrderService.Application.Interfaces;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.BackgroundServices;

public sealed class OrderRecoveryBackgroundService(IServiceScopeFactory scopeFactory, IOptions<OrderRecoveryOptions> options, ILogger<OrderRecoveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) { logger.LogInformation("Order recovery is disabled"); return; }
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.CheckIntervalSeconds));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<IOrderRecoveryService>().RecoverBatchAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Order recovery processed {Count} orders", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Order recovery cycle failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
