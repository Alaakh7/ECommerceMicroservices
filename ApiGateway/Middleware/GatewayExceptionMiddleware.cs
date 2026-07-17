namespace ApiGateway.Middleware;

public sealed class GatewayExceptionMiddleware(RequestDelegate next, ILogger<GatewayExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            if (!context.Response.HasStarted) await GatewayProblemDetails.WriteAsync(context, exception.StatusCode, "Payload too large", "The request body exceeds the configured gateway limit.");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Gateway request was cancelled by the client.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected gateway error.");
            if (!context.Response.HasStarted)
            {
                await GatewayProblemDetails.WriteAsync(context, StatusCodes.Status500InternalServerError, "Gateway error", "An unexpected error occurred in the API gateway.");
            }
        }
    }
}
