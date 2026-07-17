using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OrderService.Application.Interfaces;
using OrderService.Application.Services;
using OrderService.Common.ErrorHandling;
using OrderService.Common.Extensions;
using OrderService.Common.Middleware;
using OrderService.Infrastructure.BackgroundServices;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.ExternalServices;
using OrderService.Infrastructure.Repositories;
using Polly;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var problem = new ValidationProblemDetails(context.ModelState) { Type = "https://httpstatuses.com/400", Title = "Validation failed", Status = 400, Detail = "One or more validation errors occurred.", Instance = context.HttpContext.Request.Path };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier; problem.Extensions["correlationId"] = context.HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
    return new BadRequestObjectResult(problem);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Order Service API", Version = "v1", Description = "Order snapshots, idempotent saga orchestration, inventory compensation, optimistic concurrency, and recovery. A 202 response means processing is durable and scheduled for retry." });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"); if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

var connectionString = builder.Configuration.GetConnectionString("OrderDatabase") ?? string.Empty;
builder.Services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName).Validate(x => x.MigrationRetryCount > 0 && x.MigrationRetryDelaySeconds >= 0, "Database migration settings are invalid.").ValidateOnStart();
builder.Services.AddOptions<OrderRulesOptions>().BindConfiguration(OrderRulesOptions.SectionName).Validate(x => x.DefaultCurrency.Length == 3 && x.MaximumItemsPerOrder is > 0 and <= 100 && x.MaximumQuantityPerItem > 0, "Order rules are invalid.").ValidateOnStart();
builder.Services.AddOptions<OrderRecoveryOptions>().BindConfiguration(OrderRecoveryOptions.SectionName).Validate(x => x.CheckIntervalSeconds > 0 && x.BatchSize is > 0 and <= 1000 && x.MaximumRetryCount > 0 && x.InitialRetryDelaySeconds > 0 && x.MaximumRetryDelayMinutes > 0, "Order recovery settings are invalid.").ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddHttpContextAccessor(); builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>(); builder.Services.AddScoped<IOrderOperationRepository, OrderOperationRepository>(); builder.Services.AddScoped<IOrderStatusHistoryRepository, OrderStatusHistoryRepository>();
builder.Services.AddScoped<IOrderService, OrderApplicationService>(); builder.Services.AddScoped<OrderTotalsCalculator>(); builder.Services.AddScoped<OrderNumberGenerator>(); builder.Services.AddScoped<OrderStatusTransitionValidator>(); builder.Services.AddScoped<RequestHashService>();
builder.Services.AddScoped<OrderWorkflowService>(); builder.Services.AddScoped<IOrderWorkflowService>(sp => sp.GetRequiredService<OrderWorkflowService>());
builder.Services.AddScoped<IOrderCancellationService, OrderCancellationService>(); builder.Services.AddScoped<IOrderRecoveryService, OrderRecoveryService>(); builder.Services.AddHostedService<OrderRecoveryBackgroundService>();

AddExternalClient<ICartServiceClient, CartServiceClient>("CartService", 15); AddExternalClient<ICustomerServiceClient, CustomerServiceClient>("CustomerService", 10); AddExternalClient<IProductServiceClient, ProductServiceClient>("ProductService", 10);
void AddExternalClient<TClient, TImplementation>(string name, int defaultTimeout) where TClient : class where TImplementation : class, TClient
{
    var section = builder.Configuration.GetSection($"Services:{name}"); var baseUrl = section.GetValue<string>("BaseUrl") ?? string.Empty; var timeout = section.GetValue("TimeoutSeconds", defaultTimeout);
    builder.Services.AddHttpClient<TClient, TImplementation>(name, client => { if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var address)) client.BaseAddress = address; client.Timeout = TimeSpan.FromSeconds(timeout); client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); })
        .AddHttpMessageHandler<CorrelationIdHandler>().AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3; options.Retry.Delay = TimeSpan.FromMilliseconds(200); options.Retry.BackoffType = DelayBackoffType.Exponential; options.Retry.UseJitter = true;
            options.CircuitBreaker.FailureRatio = 0.5; options.CircuitBreaker.MinimumThroughput = 5; options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30); options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        });
}

var checkDependenciesInReadiness = builder.Configuration.GetValue("HealthChecks:CheckDependenciesInReadiness", false); var dependencyTags = checkDependenciesInReadiness ? new[] { "dependencies", "ready" } : new[] { "dependencies" };
builder.Services.AddHealthChecks().AddDbContextCheck<OrderDbContext>("order-database", tags: ["ready"])
    .AddTypeActivatedCheck<ExternalServiceHealthCheck>("product-service", failureStatus: null, tags: dependencyTags, args: ["ProductService"])
    .AddTypeActivatedCheck<ExternalServiceHealthCheck>("customer-service", failureStatus: null, tags: dependencyTags, args: ["CustomerService"])
    .AddTypeActivatedCheck<ExternalServiceHealthCheck>("cart-service", failureStatus: null, tags: dependencyTags, args: ["CartService"]);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("ConfiguredOrigins", policy => { if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod(); else policy.SetIsOriginAllowed(_ => false); }));
var rateLimitEnabled = builder.Configuration.GetValue("RateLimiting:Enabled", true); var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100); var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60); var queueLimit = builder.Configuration.GetValue("RateLimiting:QueueLimit", 10);
builder.Services.AddRateLimiter(options => { options.RejectionStatusCode = 429; if (rateLimitEnabled) options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = queueLimit, QueueProcessingOrder = QueueProcessingOrder.OldestFirst, AutoReplenishment = true })); });

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>(); app.UseExceptionHandler(); app.UseCors("ConfiguredOrigins"); app.UseRateLimiter();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service v1")); }
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = HealthCheckResponseWriter.WriteAsync }).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready"), ResponseWriter = HealthCheckResponseWriter.WriteAsync }).DisableRateLimiting();
app.MapHealthChecks("/health/dependencies", new HealthCheckOptions { Predicate = check => check.Tags.Contains("dependencies"), ResponseWriter = HealthCheckResponseWriter.WriteAsync }).DisableRateLimiting();
await app.InitializeOrderDatabaseAsync(); app.Run();
public partial class Program;
