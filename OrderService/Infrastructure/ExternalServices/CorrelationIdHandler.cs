using OrderService.Common.Middleware;

namespace OrderService.Infrastructure.ExternalServices;

public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var id = accessor.HttpContext?.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        if (!string.IsNullOrWhiteSpace(id)) request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);
        return base.SendAsync(request, cancellationToken);
    }
}
