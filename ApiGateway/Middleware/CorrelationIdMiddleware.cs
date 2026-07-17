using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace ApiGateway.Middleware;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const int MaximumLength = 100;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreate(context.Request.Headers[HeaderName]);
        context.Items[HeaderName] = correlationId;
        context.Request.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString()
        }))
        {
            await next(context);
        }
    }

    public static string GetOrCreate(StringValues supplied)
    {
        if (supplied.Count == 1)
        {
            var value = supplied[0];
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= MaximumLength && SafeValue().IsMatch(value)) return value;
        }
        return Guid.NewGuid().ToString("N");
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeValue();
}
