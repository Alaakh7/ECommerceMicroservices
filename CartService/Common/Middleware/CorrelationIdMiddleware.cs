using Microsoft.Extensions.Primitives;

namespace CartService.Common.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var value = context.Request.Headers.TryGetValue(HeaderName, out StringValues supplied) && !StringValues.IsNullOrEmpty(supplied)
            ? supplied.ToString()
            : Guid.NewGuid().ToString("N");
        context.Items[HeaderName] = value;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = value;
            return Task.CompletedTask;
        });
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = value }))
        {
            await next(context);
        }
    }
}
