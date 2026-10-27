using Week08.Api.Middleware;

namespace Week08.Api.Endpoints;

public sealed record DemoInput(string? Note);

public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var demo = app.MapGroup("/api/demo").WithTags("Observability Demo");

        demo.MapGet("/trace", (HttpContext context) => Results.Ok(new
        {
            status = "ok",
            correlationId = CorrelationIdMiddleware.Get(context)
        }));

        // Return a generic acknowledgement. Do not log or echo the input.
        demo.MapPost("/submit", (DemoInput input) => Results.Ok(new
        {
            accepted = true
        }));

        // The synthetic failing endpoint is never mapped in Production.
        if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            demo.MapGet("/fail", () => ThrowDemoException());
        }

        return app;
    }

    private static IResult ThrowDemoException() =>
        throw new InvalidOperationException("SENSITIVE_EXCEPTION_DO_NOT_LOG_5412");
}
