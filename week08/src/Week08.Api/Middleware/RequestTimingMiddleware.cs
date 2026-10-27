using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace Week08.Api.Middleware;

public sealed class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log route TEMPLATE rather than raw URL: paths may contain identifiers/PII.
            // Unknown routes have no template, so do not log their untrusted URL text.
            var safePath = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                           ?? "[unmatched]";
            string? userId = SafeLogIdentity.GetUserId(context.User);
            logger.LogInformation(
                "HTTP request completed Method={Method} Path={Path} StatusCode={StatusCode} ElapsedMs={ElapsedMs} UserId={UserId} CorrelationId={CorrelationId}",
                context.Request.Method,
                safePath,
                context.Response.StatusCode,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                userId,
                CorrelationIdMiddleware.Get(context));
        }
    }
}
