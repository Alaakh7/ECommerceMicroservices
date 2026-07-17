using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CartService.Common.Extensions;

public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            checks = report.Entries.Select(x => new { name = x.Key, status = x.Value.Status.ToString(), duration = x.Value.Duration, description = x.Value.Description })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
