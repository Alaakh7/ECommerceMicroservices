using CustomerService.Common.Exceptions;
using CustomerService.Common.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Common.ErrorHandling;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            Common.Exceptions.ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            DuplicateEmailException => (StatusCodes.Status409Conflict, "Duplicate email"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
            InvalidCustomerStateException => (StatusCodes.Status409Conflict, "Invalid customer state"),
            AddressOwnershipException => (StatusCodes.Status409Conflict, "Address ownership conflict"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };
        if (status == StatusCodes.Status500InternalServerError) logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);
        else logger.LogWarning("Request failed with status {StatusCode} for {Path}: {FailureType}", status, context.Request.Path, exception.GetType().Name);

        var details = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}", Title = title, Status = status,
            Detail = status == 500 && environment.IsProduction() ? "An unexpected server error occurred." : exception.Message,
            Instance = context.Request.Path
        };
        details.Extensions["traceId"] = context.TraceIdentifier;
        details.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        if (exception is Common.Exceptions.ValidationException validation) details.Extensions["errors"] = validation.Errors;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(details, cancellationToken);
        return true;
    }
}
