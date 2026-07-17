using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CartService.Application.Interfaces;
using CartService.Application.Services;
using CartService.Common.ErrorHandling;
using CartService.Common.Extensions;
using CartService.Common.Middleware;
using CartService.Infrastructure.BackgroundServices;
using CartService.Infrastructure.Data;
using CartService.Infrastructure.ExternalServices;
using CartService.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://httpstatuses.com/400", Title = "Validation failed", Status = 400,
            Detail = "One or more validation errors occurred.", Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        problem.Extensions["correlationId"] = context.HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        return new BadRequestObjectResult(problem);
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Cart Service API", Version = "v1",
        Description = "Shopping-cart lifecycle, product snapshots, optimistic concurrency, expiration, and idempotent checkout preparation/completion."
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

var connectionString = builder.Configuration.GetConnectionString("CartDatabase") ?? string.Empty;
builder.Services.AddDbContext<CartDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName)
    .Validate(x => x.MigrationRetryCount > 0, "MigrationRetryCount must be greater than zero.")
    .Validate(x => x.MigrationRetryDelaySeconds >= 0, "MigrationRetryDelaySeconds cannot be negative.").ValidateOnStart();
builder.Services.AddOptions<CartRulesOptions>().BindConfiguration(CartRulesOptions.SectionName)
    .Validate(x => x.DefaultCurrency.Length == 3, "DefaultCurrency must have three letters.")
    .Validate(x => x.MaximumDistinctItems is > 0 and <= 100, "MaximumDistinctItems must be between 1 and 100.")
    .Validate(x => x.MaximumQuantityPerItem > 0, "MaximumQuantityPerItem must be positive.")
    .Validate(x => x.CartExpirationDays > 0 && x.CheckoutLockMinutes > 0, "Cart expiration and checkout lock must be positive.").ValidateOnStart();
builder.Services.AddOptions<CartExpirationOptions>().BindConfiguration(CartExpirationOptions.SectionName)
    .Validate(x => x.CheckIntervalMinutes > 0 && x.BatchSize is > 0 and <= 1000, "Cart expiration settings are invalid.").ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<ICartItemRepository, CartItemRepository>();
builder.Services.AddScoped<ICartService, CartApplicationService>();
builder.Services.AddScoped<ICartItemService, CartItemApplicationService>();
builder.Services.AddScoped<ICartCheckoutService, CartCheckoutApplicationService>();
builder.Services.AddScoped<CartValidationCoordinator>();
builder.Services.AddScoped<ICartExpirationProcessor, CartExpirationProcessor>();
builder.Services.AddHostedService<CartExpirationBackgroundService>();

AddExternalClient<IProductServiceClient, ProductServiceClient>("ProductService");
AddExternalClient<ICustomerServiceClient, CustomerServiceClient>("CustomerService");

void AddExternalClient<TClient, TImplementation>(string name)
    where TClient : class where TImplementation : class, TClient
{
    var section = builder.Configuration.GetSection($"Services:{name}");
    var baseUrl = section.GetValue<string>("BaseUrl") ?? string.Empty;
    var timeout = section.GetValue("TimeoutSeconds", 10);
    builder.Services.AddHttpClient<TClient, TImplementation>(name, client =>
        {
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var address)) client.BaseAddress = address;
            client.Timeout = TimeSpan.FromSeconds(timeout);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddHttpMessageHandler<CorrelationIdHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.Retry.DisableForUnsafeHttpMethods();
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        });
}

var checkDependenciesInReadiness = builder.Configuration.GetValue("HealthChecks:CheckDependenciesInReadiness", false);
var dependencyTags = checkDependenciesInReadiness ? new[] { "dependencies", "ready" } : new[] { "dependencies" };
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CartDbContext>("cart-database", tags: ["ready"])
    .AddTypeActivatedCheck<ExternalServiceHealthCheck>("product-service", failureStatus: null, tags: dependencyTags, args: ["ProductService"])
    .AddTypeActivatedCheck<ExternalServiceHealthCheck>("customer-service", failureStatus: null, tags: dependencyTags, args: ["CustomerService"]);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("ConfiguredOrigins", policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    else policy.SetIsOriginAllowed(_ => false);
}));
var rateLimitEnabled = builder.Configuration.GetValue("RateLimiting:Enabled", true);
var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
var queueLimit = builder.Configuration.GetValue("RateLimiting:QueueLimit", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    if (rateLimitEnabled)
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = queueLimit, QueueProcessingOrder = QueueProcessingOrder.OldestFirst, AutoReplenishment = true }));
});

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseCors("ConfiguredOrigins");
app.UseRateLimiter();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Cart Service v1"));
}
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = HealthCheckResponseWriter.WriteAsync }).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready"), ResponseWriter = HealthCheckResponseWriter.WriteAsync }).DisableRateLimiting();
app.MapHealthChecks("/health/dependencies", new HealthCheckOptions { Predicate = check => check.Tags.Contains("dependencies"), ResponseWriter = HealthCheckResponseWriter.WriteAsync }).DisableRateLimiting();
await app.InitializeCartDatabaseAsync();
app.Run();

public partial class Program;
