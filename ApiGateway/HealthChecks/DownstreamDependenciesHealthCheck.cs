using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiGateway.HealthChecks;

public sealed class DownstreamDependenciesHealthCheck(DependencyHealthService health) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var services = await health.CheckAsync(cancellationToken);
        return services.All(service => service.Status == "Healthy")
            ? HealthCheckResult.Healthy("All downstream services are ready.")
            : HealthCheckResult.Degraded("One or more downstream services are unavailable.");
    }
}
