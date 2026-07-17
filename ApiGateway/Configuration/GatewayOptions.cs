using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Configuration;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    [Required]
    public string Name { get; init; } = "ECommerce API Gateway";
    public bool ExposeRouteDetails { get; init; }
    public bool UseHttpsRedirection { get; init; }
}

public sealed class GatewayCorsOptions
{
    public const string SectionName = "Cors";
    public string[] AllowedOrigins { get; init; } = [];
    public string[] AllowedMethods { get; init; } = ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"];
    public string[] AllowedHeaders { get; init; } = ["Content-Type", "Authorization", "X-Correlation-ID", "If-Match"];
    public string[] ExposedHeaders { get; init; } = ["Location", "X-Correlation-ID", "Retry-After"];
    public bool AllowCredentials { get; init; }
    [Range(0, 1440)] public int PreflightMaxAgeMinutes { get; init; } = 10;
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public bool Enabled { get; init; } = true;
    public RateLimitPolicyOptions ReadPolicy { get; init; } = new(120, 60, 10);
    public RateLimitPolicyOptions WritePolicy { get; init; } = new(60, 60, 5);
    public RateLimitPolicyOptions CriticalPolicy { get; init; } = new(20, 60, 0);
}

public sealed record RateLimitPolicyOptions(int PermitLimit, int WindowSeconds, int QueueLimit);

public sealed class GatewayRequestTimeoutOptions
{
    public const string SectionName = "RequestTimeouts";
    public int DefaultSeconds { get; init; } = 30;
    public int ReadSeconds { get; init; } = 15;
    public int WriteSeconds { get; init; } = 30;
    public int OrderCreationSeconds { get; init; } = 90;
}

public sealed class RequestLimitsOptions
{
    public const string SectionName = "RequestLimits";
    public long MaximumBodySizeBytes { get; init; } = 1_048_576;
    public int MaximumHeaderCount { get; init; } = 100;
    public int MaximumHeadersTotalSizeBytes { get; init; } = 32_768;
    public int MaximumRequestLineSize { get; init; } = 8_192;
}

public sealed class DependencyHealthOptions
{
    public const string SectionName = "DependencyHealth";
    public bool Enabled { get; init; } = true;
    public bool CheckDependenciesInReadiness { get; init; }
    public int TimeoutSeconds { get; init; } = 3;
    public int CacheDurationSeconds { get; init; } = 5;
}

public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";
    public string ContentTypeOptions { get; init; } = "nosniff";
    public string FrameOptions { get; init; } = "DENY";
    public string ReferrerPolicy { get; init; } = "no-referrer";
    public string PermissionsPolicy { get; init; } = "geolocation=(), microphone=(), camera=()";
}

public sealed class GatewayForwardedHeadersOptions
{
    public const string SectionName = "ForwardedHeaders";
    public int ForwardLimit { get; init; } = 1;
    public string[] KnownProxies { get; init; } = [];
    public string[] KnownNetworks { get; init; } = [];
}

public sealed class DocumentationOptions
{
    public const string SectionName = "Documentation";
    public bool Enabled { get; init; } = true;
    public bool ExposeDownstreamDocuments { get; init; } = true;
}

public sealed class GatewayAuthenticationOptions
{
    public const string SectionName = "Authentication";
    public bool Enabled { get; init; }
    public string Authority { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public bool RequireHttpsMetadata { get; init; } = true;
}
