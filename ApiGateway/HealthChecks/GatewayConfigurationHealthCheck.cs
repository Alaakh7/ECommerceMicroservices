using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.HealthChecks;

public sealed class GatewayConfigurationHealthCheck(IProxyConfigProvider configProvider) : IHealthCheck
{
    private static readonly string[] RequiredClusters = ["product-cluster", "customer-cluster", "cart-cluster", "order-cluster"];

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var config = configProvider.GetConfig();
        var missing = RequiredClusters.Where(id => config.Clusters.All(cluster => cluster.ClusterId != id || cluster.Destinations is null || cluster.Destinations.Count == 0)).ToArray();
        if (config.Routes.Count == 0 || missing.Length > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Gateway routing configuration is incomplete. Missing clusters: {string.Join(", ", missing)}"));
        }
        return Task.FromResult(HealthCheckResult.Healthy("Gateway routing configuration is loaded."));
    }
}
