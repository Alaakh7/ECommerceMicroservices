using ApiGateway.Configuration;
using Microsoft.Extensions.Options;

namespace ApiGateway.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = options.Value.ContentTypeOptions;
            headers["X-Frame-Options"] = options.Value.FrameOptions;
            headers["Referrer-Policy"] = options.Value.ReferrerPolicy;
            headers["Permissions-Policy"] = options.Value.PermissionsPolicy;
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            return Task.CompletedTask;
        });
        return next(context);
    }
}
