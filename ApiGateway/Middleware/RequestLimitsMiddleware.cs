using ApiGateway.Configuration;
using Microsoft.Extensions.Options;

namespace ApiGateway.Middleware;

public sealed class RequestLimitsMiddleware(RequestDelegate next, IOptions<RequestLimitsOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.Count > options.Value.MaximumHeaderCount)
        {
            await GatewayProblemDetails.WriteAsync(context, StatusCodes.Status431RequestHeaderFieldsTooLarge, "Request headers too large", "The request contains too many headers.");
            return;
        }

        if (context.Request.ContentLength > options.Value.MaximumBodySizeBytes)
        {
            await GatewayProblemDetails.WriteAsync(context, StatusCodes.Status413PayloadTooLarge, "Payload too large", "The request body exceeds the configured gateway limit.");
            return;
        }

        await next(context);
    }
}
