using System.Text.RegularExpressions;

namespace Week09.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[Header].ToString();
        var id = supplied.Length is >= 1 and <= 64 &&
                 Regex.IsMatch(supplied, "^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)
            ? supplied
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            logger.LogInformation("Request {Method} {Path}", context.Request.Method, context.Request.Path);
            await next(context);
            logger.LogInformation("Response {StatusCode} for {Path}",
                context.Response.StatusCode, context.Request.Path);
        }
    }
}
