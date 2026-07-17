using Microsoft.Extensions.Options;

namespace ApiGateway.Configuration;

public sealed class GatewayCorsOptionsValidator(IHostEnvironment environment) : IValidateOptions<GatewayCorsOptions>
{
    public ValidateOptionsResult Validate(string? name, GatewayCorsOptions options)
    {
        var errors = new List<string>();
        if (environment.IsProduction() && options.AllowedOrigins.Length == 0) errors.Add("Cors:AllowedOrigins must not be empty in Production.");
        if (options.AllowCredentials && options.AllowedOrigins.Contains("*", StringComparer.Ordinal)) errors.Add("Wildcard CORS origins cannot be combined with credentials.");
        foreach (var origin in options.AllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) errors.Add($"CORS origin '{origin}' is not an absolute HTTP(S) URI.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        static bool Valid(RateLimitPolicyOptions value) => value.PermitLimit > 0 && value.WindowSeconds > 0 && value.QueueLimit >= 0;
        return Valid(options.ReadPolicy) && Valid(options.WritePolicy) && Valid(options.CriticalPolicy)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Rate limit permit/window values must be positive and queue limits cannot be negative.");
    }
}

public sealed class GatewayRequestTimeoutOptionsValidator : IValidateOptions<GatewayRequestTimeoutOptions>
{
    public ValidateOptionsResult Validate(string? name, GatewayRequestTimeoutOptions options) =>
        options.DefaultSeconds > 0 && options.ReadSeconds > 0 && options.WriteSeconds > 0 && options.OrderCreationSeconds > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("All request timeout values must be positive.");
}

public sealed class RequestLimitsOptionsValidator : IValidateOptions<RequestLimitsOptions>
{
    public ValidateOptionsResult Validate(string? name, RequestLimitsOptions options) =>
        options.MaximumBodySizeBytes > 0 && options.MaximumHeaderCount > 0 && options.MaximumHeadersTotalSizeBytes > 0 && options.MaximumRequestLineSize > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Request limits must be positive.");
}

public sealed class ReverseProxyConfigurationValidator(IConfiguration configuration) : IValidateOptions<GatewayOptions>
{
    private static readonly string[] RequiredClusters = ["product-cluster", "customer-cluster", "cart-cluster", "order-cluster"];
    private static readonly string[] RequiredRoutes = ["products-route", "categories-route", "customers-route", "carts-route", "orders-route"];

    public ValidateOptionsResult Validate(string? name, GatewayOptions options)
    {
        var proxy = configuration.GetSection("ReverseProxy");
        var errors = new List<string>();
        if (!proxy.Exists()) return ValidateOptionsResult.Fail("ReverseProxy configuration is missing.");
        var routes = proxy.GetSection("Routes").GetChildren().ToArray();
        var clusters = proxy.GetSection("Clusters").GetChildren().ToArray();
        foreach (var routeId in RequiredRoutes)
        {
            if (routes.All(route => !string.Equals(route.Key, routeId, StringComparison.Ordinal))) errors.Add($"Required route '{routeId}' is missing.");
        }
        foreach (var clusterId in RequiredClusters)
        {
            var cluster = clusters.SingleOrDefault(item => string.Equals(item.Key, clusterId, StringComparison.Ordinal));
            if (cluster is null) { errors.Add($"Required cluster '{clusterId}' is missing."); continue; }
            var destinations = cluster.GetSection("Destinations").GetChildren().ToArray();
            if (destinations.Length == 0) { errors.Add($"Cluster '{clusterId}' has no destination."); continue; }
            foreach (var destination in destinations)
            {
                var address = destination["Address"];
                if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) errors.Add($"Cluster '{clusterId}' contains an invalid HTTP(S) destination URI.");
            }
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
