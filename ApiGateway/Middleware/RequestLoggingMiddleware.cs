using System.Diagnostics;
using Yarp.ReverseProxy.Model;

namespace ApiGateway.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var proxy = context.Features.Get<IReverseProxyFeature>();
            var values = new object?[]
            {
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsed,
                context.Items[CorrelationIdMiddleware.HeaderName], Activity.Current?.TraceId.ToString(),
                context.Connection.RemoteIpAddress?.ToString(), proxy?.Route?.Config.RouteId,
                proxy?.Cluster?.Config.ClusterId, proxy?.ProxiedDestination?.DestinationId
            };
            const string template = "Gateway {Method} {Path} responded {StatusCode} in {ElapsedMs:F1} ms. CorrelationId={CorrelationId} TraceId={TraceId} ClientIp={ClientIp} RouteId={RouteId} ClusterId={ClusterId} Destination={DestinationName}";
            if (context.Response.StatusCode >= 500) logger.LogError(template, values);
            else if (context.Response.StatusCode >= 400) logger.LogWarning(template, values);
            else if (!context.Request.Path.StartsWithSegments("/health")) logger.LogInformation(template, values);
        }
    }
}
