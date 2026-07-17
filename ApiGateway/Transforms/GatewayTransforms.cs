using ApiGateway.Middleware;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ApiGateway.Transforms;

public static class GatewayTransforms
{
    private static readonly (string Internal, string External)[] LocationPrefixes =
    [
        ("/api/v1/products", "/api/products"),
        ("/api/v1/categories", "/api/categories"),
        ("/api/v1/customers", "/api/customers"),
        ("/api/v1/carts", "/api/carts"),
        ("/api/v1/orders", "/api/orders")
    ];

    public static void AddGatewayTransforms(this TransformBuilderContext builder)
    {
        builder.AddRequestTransform(context =>
        {
            context.ProxyRequest.Headers.Remove("X-Gateway-Request");
            context.ProxyRequest.Headers.TryAddWithoutValidation("X-Gateway-Request", "true");
            if (context.HttpContext.Items[CorrelationIdMiddleware.HeaderName] is string id)
            {
                context.ProxyRequest.Headers.Remove(CorrelationIdMiddleware.HeaderName);
                context.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);
            }
            return ValueTask.CompletedTask;
        });

        builder.AddResponseTransform(context =>
        {
            context.HttpContext.Response.Headers.Remove("Server");
            context.HttpContext.Response.Headers.Remove("X-Powered-By");
            var location = context.ProxyResponse?.Headers.Location?.ToString();
            var rewritten = RewriteLocation(location);
            if (rewritten is not null) context.HttpContext.Response.Headers.Location = rewritten;
            return ValueTask.CompletedTask;
        });
    }

    public static string? RewriteLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return location;
        if (Uri.TryCreate(location, UriKind.Absolute, out var absolute)) location = absolute.PathAndQuery + absolute.Fragment;
        foreach (var (internalPrefix, externalPrefix) in LocationPrefixes)
        {
            if (location.StartsWith(internalPrefix, StringComparison.OrdinalIgnoreCase)) return externalPrefix + location[internalPrefix.Length..];
        }
        return location;
    }
}
