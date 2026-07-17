using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CartService.Infrastructure.ExternalServices;

public sealed class ExternalServiceHealthCheck(IHttpClientFactory factory, string clientName) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await factory.CreateClient(clientName).GetAsync("health/live", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{clientName} returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"{clientName} health request failed.", exception);
        }
    }
}
