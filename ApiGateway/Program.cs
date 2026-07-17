using ApiGateway.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("reverseproxy.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"reverseproxy.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = context.Configuration.GetValue<long>("RequestLimits:MaximumBodySizeBytes", 1_048_576);
    options.Limits.MaxRequestHeadersTotalSize = context.Configuration.GetValue<int>("RequestLimits:MaximumHeadersTotalSizeBytes", 32_768);
    options.Limits.MaxRequestLineSize = context.Configuration.GetValue<int>("RequestLimits:MaximumRequestLineSize", 8_192);
});

builder.Services.AddGateway(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseGatewayPipeline();
app.MapGatewayEndpoints();
app.MapGatewayProxy();

app.Run();

public partial class Program;
