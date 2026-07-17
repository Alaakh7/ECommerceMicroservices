namespace ApiGateway.Models;

public sealed record GatewayRouteInfoResponse(string PublicPath, string[] Methods, string Service, string RateLimitPolicy, string TimeoutPolicy);

public sealed record GatewayInfoResponse(
    string Name,
    string Version,
    string Environment,
    DateTimeOffset TimestampUtc,
    string[] PublicRoutePrefixes,
    string CorrelationId,
    IReadOnlyList<GatewayRouteInfoResponse>? Routes);

public sealed record GatewayDependencyStatusResponse(string Name, string Status, long DurationMs);

public sealed record DependencyHealthResponse(
    string Status,
    DateTimeOffset TimestampUtc,
    IReadOnlyList<GatewayDependencyStatusResponse> Services,
    string CorrelationId);
