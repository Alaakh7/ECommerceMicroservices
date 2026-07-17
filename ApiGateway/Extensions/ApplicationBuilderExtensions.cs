using System.Reflection;
using ApiGateway.Configuration;
using ApiGateway.HealthChecks;
using ApiGateway.Middleware;
using ApiGateway.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace ApiGateway.Extensions;

public static class ApplicationBuilderExtensions
{
    private static readonly GatewayRouteInfoResponse[] PublicRoutes =
    [
        new("/api/products", ["GET", "POST", "PUT", "PATCH", "DELETE"], "ProductService", "read/write", "read/write"),
        new("/api/categories", ["GET", "POST", "PUT", "PATCH", "DELETE"], "ProductService", "read/write", "read/write"),
        new("/api/customers", ["GET", "POST", "PUT", "PATCH", "DELETE"], "CustomerService", "read/write", "read/write"),
        new("/api/carts", ["GET", "POST", "PUT", "DELETE"], "CartService", "read/write/critical", "read/write"),
        new("/api/orders", ["GET", "POST"], "OrderService", "read/write/critical", "read/write/order-creation")
    ];

    public static WebApplication UseGatewayPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseMiddleware<GatewayExceptionMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseStatusCodePages(async statusContext =>
        {
            var response = statusContext.HttpContext.Response;
            if (response.StatusCode == StatusCodes.Status404NotFound && string.IsNullOrEmpty(response.ContentType))
            {
                await GatewayProblemDetails.WriteAsync(statusContext.HttpContext, response.StatusCode, "Route not found", "No public gateway route matches this request.");
            }
        });
        app.UseMiddleware<RequestLoggingMiddleware>();

        var gateway = app.Services.GetRequiredService<IOptions<GatewayOptions>>().Value;
        if (!app.Environment.IsDevelopment() && gateway.UseHttpsRedirection)
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestLimitsMiddleware>();
        app.UseResponseCompression();
        app.UseCors();
        app.UseRequestTimeouts();
        app.UseRateLimiter();
        return app;
    }

    public static WebApplication MapGatewayEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = GatewayHealthResponseWriter.WriteAsync
        }).DisableRateLimiting().DisableRequestTimeout();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = GatewayHealthResponseWriter.WriteAsync
        }).DisableRateLimiting().DisableRequestTimeout();

        app.MapGet("/health/dependencies", async (HttpContext context, DependencyHealthService health, TimeProvider timeProvider) =>
        {
            var services = await health.CheckAsync(context.RequestAborted);
            var status = services.All(service => service.Status == "Healthy") ? "Healthy" : services.Any(service => service.Status == "Healthy") ? "Degraded" : "Unhealthy";
            var response = new DependencyHealthResponse(status, timeProvider.GetUtcNow(), services, context.Items[CorrelationIdMiddleware.HeaderName]?.ToString() ?? string.Empty);
            return Results.Json(response, statusCode: status == "Unhealthy" ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK);
        }).DisableRateLimiting().DisableRequestTimeout();

        app.MapGet("/api/gateway/info", (HttpContext context, IOptions<GatewayOptions> gateway, TimeProvider timeProvider) =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            return Results.Ok(new GatewayInfoResponse(
                gateway.Value.Name,
                version,
                app.Environment.EnvironmentName,
                timeProvider.GetUtcNow(),
                PublicRoutes.Select(route => route.PublicPath).ToArray(),
                context.Items[CorrelationIdMiddleware.HeaderName]?.ToString() ?? string.Empty,
                gateway.Value.ExposeRouteDetails ? PublicRoutes : null));
        }).DisableRateLimiting().WithRequestTimeout("default");

        app.MapGet("/api/gateway/status", async (DependencyHealthService health, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        {
            var services = await health.CheckAsync(cancellationToken);
            return Results.Ok(new
            {
                status = services.All(service => service.Status == "Healthy") ? "Healthy" : "Degraded",
                services = services.Select(service => new { service.Name, service.Status }),
                timestampUtc = timeProvider.GetUtcNow()
            });
        }).WithRequestTimeout("default");

        var docs = app.Services.GetRequiredService<IOptions<DocumentationOptions>>().Value;
        if (docs.Enabled && (app.Environment.IsDevelopment() || docs.ExposeDownstreamDocuments))
        {
            app.MapGet("/docs", () => Results.Content(DocsHtml, "text/html; charset=utf-8")).DisableRateLimiting().WithRequestTimeout("default");
        }

        if (app.Environment.IsDevelopment() && app.Services.GetRequiredService<IOptions<GatewayOptions>>().Value.ExposeRouteDetails)
        {
            app.MapGet("/api/gateway/routes", () => Results.Ok(PublicRoutes)).DisableRateLimiting();
        }

        return app;
    }

    public static WebApplication MapGatewayProxy(this WebApplication app)
    {
        app.MapReverseProxy(proxyPipeline => proxyPipeline.Use(async (context, next) =>
        {
            await next();
            var error = context.GetForwarderErrorFeature();
            if (error is null || error.Error == ForwarderError.None || context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) return;

            var (status, title, detail) = error.Error switch
            {
                ForwarderError.NoAvailableDestinations => (StatusCodes.Status503ServiceUnavailable, "Downstream unavailable", "No healthy destination is currently available for this service."),
                ForwarderError.RequestTimedOut or ForwarderError.UpgradeActivityTimeout => (StatusCodes.Status504GatewayTimeout, "Gateway timeout", "The downstream service did not respond before the forwarding timeout."),
                _ => (StatusCodes.Status502BadGateway, "Proxy forwarding failure", "The gateway could not obtain a valid response from the downstream service.")
            };
            await GatewayProblemDetails.WriteAsync(context, status, title, detail);
        }));
        return app;
    }

    private const string DocsHtml = """
        <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>ECommerce API Gateway documentation</title>
        <style>body{font-family:system-ui;margin:3rem;max-width:50rem}li{margin:.8rem 0}code{background:#eee;padding:.2rem .4rem}</style></head>
        <body><h1>ECommerce API Gateway</h1><p>Downstream OpenAPI documents are exposed through the gateway in Development. Internal operations may appear in service documents even when they have no public gateway route.</p>
        <ul><li><a href="/docs/products/openapi.json">ProductService OpenAPI</a></li><li><a href="/docs/customers/openapi.json">CustomerService OpenAPI</a></li><li><a href="/docs/carts/openapi.json">CartService OpenAPI</a></li><li><a href="/docs/orders/openapi.json">OrderService OpenAPI</a></li></ul></body></html>
        """;
}
