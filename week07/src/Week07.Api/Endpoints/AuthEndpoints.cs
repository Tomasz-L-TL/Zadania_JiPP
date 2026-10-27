using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Week07.Api.Auth;
using Week07.Api.Contracts;

namespace Week07.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication").AllowAnonymous();
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
    }

    private static IResult Register(RegisterRequest request, AuthService auth)
    {
        var errors = Validate(request);
        if (errors.Count != 0) return Results.ValidationProblem(errors);

        var result = auth.Register(request.Email!, request.Password!);
        if (result.Conflict)
            return Results.Problem(statusCode: 409, title: "Użytkownik już istnieje.");
        return Results.Created($"/api/users/{result.User!.Id}", result.User);
    }

    private static IResult Login(LoginRequest request, AuthService auth,
        TokenService tokens, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        // Exactly the same 401 response for unknown user and wrong password.
        var user = request.Email is not null && request.Password is not null
            ? auth.Login(request.Email, request.Password) : null;
        if (user is null)
            return Results.Problem(statusCode: 401, title: "Nieprawidłowe dane logowania.");
        return Results.Ok(tokens.Create(user));
    }

    private static Dictionary<string, string[]> Validate<T>(T request) where T : class
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var result in validationResults)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                var key = member.Length > 0 ? JsonNamingPolicy.CamelCase.ConvertName(member) : string.Empty;
                if (!grouped.TryGetValue(key, out var messages))
                    grouped.Add(key, messages = []);
                messages.Add(result.ErrorMessage ?? "Niepoprawna wartość.");
            }
        }
        return grouped.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }
}
