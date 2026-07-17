using System.Diagnostics;
using ApiGateway.Middleware;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiGateway.HealthChecks;

public static class GatewayHealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestampUtc = DateTimeOffset.UtcNow,
            durationMs = (long)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString(), durationMs = (long)entry.Value.Duration.TotalMilliseconds }),
            correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString(),
            traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
        };
        return context.Response.WriteAsJsonAsync(response, cancellationToken: context.RequestAborted);
    }
}
