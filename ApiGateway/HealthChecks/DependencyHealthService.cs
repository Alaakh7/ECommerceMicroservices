using System.Diagnostics;
using ApiGateway.Configuration;
using ApiGateway.Models;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.HealthChecks;

public sealed class DependencyHealthService(
    IHttpClientFactory httpClientFactory,
    IProxyConfigProvider configProvider,
    IOptions<DependencyHealthOptions> options,
    TimeProvider timeProvider,
    ILogger<DependencyHealthService> logger)
{
    private static readonly (string ClusterId, string ServiceName)[] Services =
    [
        ("product-cluster", "ProductService"),
        ("customer-cluster", "CustomerService"),
        ("cart-cluster", "CartService"),
        ("order-cluster", "OrderService")
    ];

    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private DateTimeOffset cacheExpiresUtc;
    private IReadOnlyList<GatewayDependencyStatusResponse>? cached;

    public async Task<IReadOnlyList<GatewayDependencyStatusResponse>> CheckAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (cached is not null && now < cacheExpiresUtc) return cached;
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();
            if (cached is not null && now < cacheExpiresUtc) return cached;
            cached = await Task.WhenAll(Services.Select(service => CheckServiceAsync(service, cancellationToken)));
            cacheExpiresUtc = now.AddSeconds(Math.Max(1, options.Value.CacheDurationSeconds));
            return cached;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private async Task<GatewayDependencyStatusResponse> CheckServiceAsync((string ClusterId, string ServiceName) service, CancellationToken cancellationToken)
    {
        var cluster = configProvider.GetConfig().Clusters.FirstOrDefault(item => item.ClusterId == service.ClusterId);
        var address = cluster?.Destinations?.Values.FirstOrDefault()?.Address;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var baseUri)) return new(service.ServiceName, "Unhealthy", 0);

        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            var healthUri = new Uri(baseUri, "health/ready");
            using var request = new HttpRequestMessage(HttpMethod.Get, healthUri);
            using var response = await httpClientFactory.CreateClient("dependency-health").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return new(service.ServiceName, response.IsSuccessStatusCode ? "Healthy" : "Unhealthy", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(service.ServiceName, "Timeout", stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException exception)
        {
            logger.LogDebug(exception, "Dependency health check failed for {ServiceName}.", service.ServiceName);
            return new(service.ServiceName, "Unhealthy", stopwatch.ElapsedMilliseconds);
        }
    }
}
