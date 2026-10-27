namespace Week03.Api.Validation;

// Ćwiczenie 5: wspólny filtr dla POST /api/users i POST /api/tasks.
public sealed class ValidationFilter<TRequest>(IRequestValidator validator) : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var input = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (input is null)
        {
            // Nie maskujemy błędów programistycznych konfiguracji filtra.
            throw new InvalidOperationException($"Brak parametru typu {typeof(TRequest).Name} w endpointzie.");
        }

        var errors = validator.Validate(input);
        if (errors.Count != 0)
        {
            return Results.ValidationProblem(errors, title: "Niepoprawne dane wejściowe.");
        }

        return await next(context);
    }
}
