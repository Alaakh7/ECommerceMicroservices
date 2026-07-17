using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProductService.Common.Exceptions;
using ProductService.Common.Middleware;

namespace ProductService.Common.ErrorHandling;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError) logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);
        else logger.LogWarning(exception, "Request failed with status {StatusCode} for {Path}", status, httpContext.Request.Path);

        var details = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}", Title = title, Status = status,
            Detail = status == 500 && environment.IsProduction() ? "An unexpected server error occurred." : exception.Message,
            Instance = httpContext.Request.Path
        };
        details.Extensions["traceId"] = httpContext.TraceIdentifier;
        details.Extensions["correlationId"] = httpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        if (exception is ValidationException validation) details.Extensions["errors"] = validation.Errors;
        if (exception is InsufficientStockException stock)
        {
            details.Extensions["productId"] = stock.ProductId;
            details.Extensions["requestedQuantity"] = stock.RequestedQuantity;
            details.Extensions["availableQuantity"] = stock.AvailableQuantity;
        }
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);
        return true;
    }
}
