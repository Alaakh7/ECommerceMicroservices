using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CustomerService.Application.Interfaces;
using CustomerService.Application.Services;
using CustomerService.Common.ErrorHandling;
using CustomerService.Common.Extensions;
using CustomerService.Common.Middleware;
using CustomerService.Infrastructure.Data;
using CustomerService.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            Type = "https://httpstatuses.com/400", Title = "Validation failed", Status = StatusCodes.Status400BadRequest,
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
        Title = "Customer Service API", Version = "v1",
        Description = "Customer profiles, addresses, lifecycle state, optimistic concurrency, and Cart/Order eligibility. Enums are serialized as names."
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

var connectionString = builder.Configuration.GetConnectionString("CustomerDatabase") ?? string.Empty;
builder.Services.AddDbContext<CustomerDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));
builder.Services.AddHealthChecks().AddDbContextCheck<CustomerDbContext>("customer-database", tags: ["ready"]);
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName)
    .Validate(x => x.MigrationRetryCount > 0, "MigrationRetryCount must be greater than zero.")
    .Validate(x => x.MigrationRetryDelaySeconds >= 0, "MigrationRetryDelaySeconds cannot be negative.").ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
builder.Services.AddScoped<ICustomerService, CustomerApplicationService>();
builder.Services.AddScoped<ICustomerAddressService, CustomerAddressApplicationService>();

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
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    if (rateLimitEnabled)
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = queueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst, AutoReplenishment = true
            }));
    }
});

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseCors("ConfiguredOrigins");
app.UseRateLimiter();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Customer Service v1"));
}
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = HealthCheckResponseWriter.WriteAsync }).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready"), ResponseWriter = HealthCheckResponseWriter.WriteAsync }).DisableRateLimiting();
await app.InitializeCustomerDatabaseAsync();
app.Run();

public partial class Program;
