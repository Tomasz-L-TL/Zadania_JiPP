using System.Diagnostics;

namespace Week08.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception error) when (!context.Response.HasStarted)
        {
            // Deliberately do NOT log the exception object, Message, headers, body, or query.
            // Exception messages may contain secrets/PII. Use correlation ID for investigation.
            string correlationId = CorrelationIdMiddleware.Get(context);
            logger.LogError(
                "Request failed ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                error.GetType().Name, correlationId);

            context.Response.Clear();
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

            await Results.Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred. Contact support with the correlation ID.",
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: new Dictionary<string, object?>
                {
                    ["correlationId"] = correlationId,
                    ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier
                }).ExecuteAsync(context);
        }
    }
}
