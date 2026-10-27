using System.Text.RegularExpressions;

namespace Week08.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "Week08.CorrelationId";

    // Restrict client-supplied IDs: no newlines, emails, whitespace or unbounded text.
    private static readonly Regex AllowedId = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task InvokeAsync(HttpContext context)
    {
        var received = context.Request.Headers[HeaderName];
        string id = received.Count == 1 && IsSafe(received.ToString())
            ? received.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = id;
        // Set before the pipeline: 404/500 responses also carry the correlation header.
        context.Response.Headers[HeaderName] = id;

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = id
               }))
        {
            await next(context);
        }
    }

    public static string Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id
            ? id
            : "unavailable";

    public static bool IsSafe(string? candidate) =>
        candidate is { Length: >= 1 and <= 64 } && AllowedId.IsMatch(candidate);
}
