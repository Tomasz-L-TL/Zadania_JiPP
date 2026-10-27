using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Week08.Api.Diagnostics;

public static class HealthResponseWriter
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var publicResult = new
        {
            status = report.Status.ToString(),
            dependencies = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString()
                }).ToArray()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(publicResult));
    }
}
