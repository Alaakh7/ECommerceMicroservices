using System.IO.Compression;
using System.Net;
using System.Threading.RateLimiting;
using ApiGateway.Configuration;
using ApiGateway.HealthChecks;
using ApiGateway.Middleware;
using ApiGateway.Transforms;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

namespace ApiGateway.Extensions;

public static class ServiceCollectionExtensions
{
    public const string CorsPolicyName = "gateway-cors";

    public static IServiceCollection AddGateway(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddProblemDetails();
        services.AddSingleton(TimeProvider.System);

        services.AddOptions<GatewayOptions>().BindConfiguration(GatewayOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<GatewayCorsOptions>().BindConfiguration(GatewayCorsOptions.SectionName).ValidateOnStart();
        services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName).ValidateOnStart();
        services.AddOptions<GatewayRequestTimeoutOptions>().BindConfiguration(GatewayRequestTimeoutOptions.SectionName).ValidateOnStart();
        services.AddOptions<RequestLimitsOptions>().BindConfiguration(RequestLimitsOptions.SectionName).ValidateOnStart();
        services.AddOptions<DependencyHealthOptions>().BindConfiguration(DependencyHealthOptions.SectionName).Validate(options => options.TimeoutSeconds > 0 && options.CacheDurationSeconds > 0, "Dependency health timeout and cache duration must be positive.").ValidateOnStart();
        services.AddOptions<SecurityHeadersOptions>().BindConfiguration(SecurityHeadersOptions.SectionName).ValidateOnStart();
        services.AddOptions<GatewayForwardedHeadersOptions>().BindConfiguration(GatewayForwardedHeadersOptions.SectionName).Validate(options => options.ForwardLimit > 0, "ForwardLimit must be positive.").ValidateOnStart();
        services.AddOptions<DocumentationOptions>().BindConfiguration(DocumentationOptions.SectionName).ValidateOnStart();
        services.AddOptions<GatewayAuthenticationOptions>().BindConfiguration(GatewayAuthenticationOptions.SectionName).ValidateOnStart();

        services.AddSingleton<IValidateOptions<GatewayOptions>, ReverseProxyConfigurationValidator>();
        services.AddSingleton<IValidateOptions<GatewayCorsOptions>, GatewayCorsOptionsValidator>();
        services.AddSingleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();
        services.AddSingleton<IValidateOptions<GatewayRequestTimeoutOptions>, GatewayRequestTimeoutOptionsValidator>();
        services.AddSingleton<IValidateOptions<RequestLimitsOptions>, RequestLimitsOptionsValidator>();

        ConfigureForwardedHeaders(services, configuration);
        ConfigureCors(services, configuration);
        ConfigureRateLimiting(services, configuration);
        ConfigureTimeouts(services, configuration);
        ConfigureCompression(services);

        services.AddHttpClient("dependency-health", client => client.DefaultRequestHeaders.UserAgent.ParseAdd("ECommerce-ApiGateway-Health/1.0"));
        services.AddSingleton<DependencyHealthService>();
        services.AddHealthChecks()
            .AddCheck<GatewayConfigurationHealthCheck>("gateway-configuration", tags: ["ready"]);

        if (configuration.GetValue("DependencyHealth:CheckDependenciesInReadiness", false))
        {
            services.AddHealthChecks().AddCheck<DownstreamDependenciesHealthCheck>("downstream-services", tags: ["ready"]);
        }

        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(builder => builder.AddGatewayTransforms());

        return services;
    }

    private static void ConfigureForwardedHeaders(IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration.GetSection(GatewayForwardedHeadersOptions.SectionName).Get<GatewayForwardedHeadersOptions>() ?? new();
        services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = configured.ForwardLimit;
            foreach (var proxy in configured.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address)) options.KnownProxies.Add(address);
            }
            foreach (var network in configured.KnownNetworks)
            {
                if (IPNetwork.TryParse(network, out var value)) options.KnownIPNetworks.Add(value);
            }
        });
    }

    private static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration.GetSection(GatewayCorsOptions.SectionName).Get<GatewayCorsOptions>() ?? new();
        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (configured.AllowedOrigins.Length > 0) policy.WithOrigins(configured.AllowedOrigins);
            else policy.SetIsOriginAllowed(_ => false);
            policy.WithMethods(configured.AllowedMethods)
                .WithHeaders(configured.AllowedHeaders)
                .WithExposedHeaders(configured.ExposedHeaders)
                .SetPreflightMaxAge(TimeSpan.FromMinutes(configured.PreflightMaxAgeMinutes));
            if (configured.AllowCredentials) policy.AllowCredentials();
            else policy.DisallowCredentials();
        }));
    }

    private static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new();
        services.AddRateLimiter(options =>
        {
            AddPolicy("read", configured.ReadPolicy);
            AddPolicy("write", configured.WritePolicy);
            AddPolicy("critical", configured.CriticalPolicy);

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                await GatewayProblemDetails.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, "Rate limit exceeded", "Too many requests were sent to this gateway route.");
            };

            void AddPolicy(string name, RateLimitPolicyOptions policy)
            {
                options.AddPolicy(name, context =>
                {
                    if (!configured.Enabled) return RateLimitPartition.GetNoLimiter("disabled");
                    var partition = context.User.FindFirst("sub")?.Value ?? context.Request.Headers["X-API-Key"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = policy.PermitLimit,
                        Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                        QueueLimit = policy.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
                });
            }
        });
    }

    private static void ConfigureTimeouts(IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration.GetSection(GatewayRequestTimeoutOptions.SectionName).Get<GatewayRequestTimeoutOptions>() ?? new();
        services.AddRequestTimeouts(options =>
        {
            Add("default", configured.DefaultSeconds);
            Add("read", configured.ReadSeconds);
            Add("write", configured.WriteSeconds);
            Add("order-creation", configured.OrderCreationSeconds);

            void Add(string name, int seconds) => options.AddPolicy(name, new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(seconds),
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
                WriteTimeoutResponse = context => GatewayProblemDetails.WriteAsync(context, StatusCodes.Status504GatewayTimeout, "Gateway timeout", "The downstream operation exceeded the configured gateway timeout.")
            });
        });
    }

    private static void ConfigureCompression(IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
    }
}
