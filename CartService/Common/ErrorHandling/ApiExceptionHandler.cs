using CartService.Common.Exceptions;
using CartService.Common.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CartService.Common.ErrorHandling;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (400, "Validation failed"),
            NotFoundException => (404, "Resource not found"),
            InvalidExternalResponseException => (502, "Invalid external service response"),
            ExternalServiceUnavailableException => (503, "External service unavailable"),
            ExternalServiceTimeoutException => (504, "External service timeout"),
            ConcurrencyConflictException => (409, "Concurrency conflict"),
            CartLockedException => (409, "Cart locked"),
            CartEmptyException => (409, "Cart empty"),
            PriceChangedException => (409, "Product price changed"),
            ConflictException => (409, "Conflict"),
            _ => (500, "An unexpected error occurred")
        };

        if (status == 500) logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);
        else logger.LogWarning(exception, "Request failed with status {StatusCode} for {Path}", status, context.Request.Path);

        var details = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = status == 500 && environment.IsProduction() ? "An unexpected server error occurred." : exception.Message,
            Instance = context.Request.Path
        };
        details.Extensions["traceId"] = context.TraceIdentifier;
        details.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        if (exception is ValidationException validation) details.Extensions["errors"] = validation.Errors;
        if (exception is InsufficientStockException stock)
        {
            details.Extensions["productId"] = stock.ProductId;
            details.Extensions["requestedQuantity"] = stock.RequestedQuantity;
            details.Extensions["availableQuantity"] = stock.AvailableQuantity;
        }
        if (exception is PriceChangedException changed) details.Extensions["priceChanges"] = changed.PriceChanges;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(details, cancellationToken);
        return true;
    }
}
