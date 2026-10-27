using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Week08.Api.Diagnostics;
using Week08.Api.Endpoints;
using Week08.Api.Middleware;
using Week08.Api.Options;

var builder = WebApplication.CreateBuilder(args);

// JSON Console emits structured properties; no third-party logging libraries required.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.Configure<DiagnosticsOptions>(
    builder.Configuration.GetSection(DiagnosticsOptions.SectionName));
builder.Services.AddHealthChecks()
    .AddCheck<WritableStorageHealthCheck>("writable-storage", tags: ["dependency", "storage"]);

var app = builder.Build();

// Ordering matters: correlation wraps logging and error handling.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTimingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .WithTags("Health");
app.MapHealthChecks("/health/dependencies", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.MapDemoEndpoints();
app.Run();

public partial class Program { }
