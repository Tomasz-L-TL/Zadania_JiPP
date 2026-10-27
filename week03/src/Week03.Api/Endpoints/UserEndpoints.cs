using Week03.Api.Contracts;
using Week03.Api.Dto;
using Week03.Api.Repositories;
using Week03.Api.Services;
using Week03.Api.Validation;

namespace Week03.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapPost("", (CreateUserRequest input, UserRegistrationService registration) =>
        {
            var result = registration.Register(input);
            if (result.LoginAlreadyExists)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Login jest już zajęty.",
                    detail: "Użytkownik z takim loginem już istnieje.",
                    extensions: new Dictionary<string, object?> { ["code"] = "duplicate_login" });
            }

            var response = UserResponse.FromDomain(result.User!);
            return Results.Created($"/api/users/{response.Id}", response);
        }).AddEndpointFilter<ValidationFilter<CreateUserRequest>>();

        group.MapGet("", (IUserRepository users) =>
            Results.Ok(users.GetAll().Select(UserResponse.FromDomain).ToArray()));

        group.MapGet("/{id:int}", (int id, IUserRepository users) =>
        {
            var user = users.GetById(id);
            return user is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Nie znaleziono użytkownika.")
                : Results.Ok(UserResponse.FromDomain(user));
        });

        return group;
    }
}
