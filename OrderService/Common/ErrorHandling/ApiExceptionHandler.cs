using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderService.Common.Exceptions;
using OrderService.Common.Middleware;

namespace OrderService.Common.ErrorHandling;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (400, "Validation failed"),
            NotFoundException => (404, "Resource not found"),
            InvalidExternalResponseException => (502, "Invalid external response"),
            ExternalServiceUnavailableException => (503, "External service unavailable"),
            ExternalServiceTimeoutException => (504, "External service timeout"),
            ConcurrencyConflictException => (409, "Concurrency conflict"),
            ConflictException => (409, "Conflict"),
            _ => (500, "An unexpected error occurred")
        };
        if (status == 500) logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);
        else logger.LogWarning("Request {Path} failed with {StatusCode}: {ExceptionType}", context.Request.Path, status, exception.GetType().Name);
        var details = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}", Title = title, Status = status,
            Detail = status == 500 && environment.IsProduction() ? "An unexpected server error occurred." : exception.Message,
            Instance = context.Request.Path
        };
        details.Extensions["traceId"] = context.TraceIdentifier;
        details.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        if (exception is ValidationException validation) details.Extensions["errors"] = validation.Errors;
        if (exception is InsufficientStockException stock) details.Extensions["productId"] = stock.ProductId;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(details, cancellationToken);
        return true;
    }
}
