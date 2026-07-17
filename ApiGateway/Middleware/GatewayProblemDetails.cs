using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Middleware;

public static class GatewayProblemDetails
{
    public static async Task WriteAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
    }
}
