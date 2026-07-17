using Microsoft.Extensions.Options;
using OrderService.Application.Interfaces;
using OrderService.Common.Exceptions;
using OrderService.Infrastructure.Data;

namespace OrderService.Application.Services;

public sealed class OrderRecoveryService(IOrderRepository orders, IOrderWorkflowService workflow, IOptions<OrderRecoveryOptions> options, TimeProvider timeProvider, ILogger<OrderRecoveryService> logger) : IOrderRecoveryService
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> ActiveOrders = new();
    public async Task<int> RecoverBatchAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var ids = await orders.GetRecoverableOrderIdsAsync(timeProvider.GetUtcNow(), settings.MaximumRetryCount, settings.BatchSize, cancellationToken);
        var processed = 0;
        foreach (var id in ids)
        {
            if (!ActiveOrders.TryAdd(id, 0)) continue;
            try
            {
                await workflow.RecoverAsync(id, "RecoveryWorker", cancellationToken); processed++;
            }
            catch (ConcurrencyConflictException) { logger.LogInformation("Recovery skipped order {OrderId} because another worker changed it", id); }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { logger.LogWarning(exception, "Recovery attempt failed for order {OrderId}", id); }
            finally { ActiveOrders.TryRemove(id, out _); }
        }
        return processed;
    }
}
