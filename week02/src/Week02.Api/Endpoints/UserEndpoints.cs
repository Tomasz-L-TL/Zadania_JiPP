using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Week02.Api.Contracts;
using Week02.Api.Domain;
using Week02.Api.Repositories;

namespace Week02.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/users").WithTags("Users");
        users.MapGet("", ListUsers).WithName("ListUsers");
        users.MapGet("/{id:int}", GetUserById).WithName("GetUserById");
        return users;
    }

    private static Results<Ok<UserDto>, BadRequest<ApiError>, NotFound<ApiError>> GetUserById(
        int id, IUserDirectory directory)
    {
        if (id <= 0)
        {
            return TypedResults.BadRequest(new ApiError("invalid_id", "Identyfikator musi byc wiekszy od zera."));
        }

        User? user = directory.GetById(id);
        if (user is null)
        {
            return TypedResults.NotFound(new ApiError("user_not_found", $"Uzytkownik o ID {id} nie istnieje."));
        }

        return TypedResults.Ok(ToDto(user));
    }

    private static Results<Ok<PagedResponse<UserDto>>, BadRequest<ApiError>> ListUsers(
        IUserDirectory directory,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page is < 1 or > 10_000)
        {
            return TypedResults.BadRequest(new ApiError("invalid_page", "Parametr page musi byc w zakresie 1–10000."));
        }

        if (pageSize is < 1 or > 50)
        {
            return TypedResults.BadRequest(new ApiError("invalid_page_size", "Parametr pageSize musi byc w zakresie 1–50."));
        }

        if (search is { Length: > 100 })
        {
            return TypedResults.BadRequest(new ApiError("invalid_search", "Parametr search moze miec maksymalnie 100 znakow."));
        }

        string query = search?.Trim() ?? string.Empty;
        IEnumerable<User> filtered = directory.GetAll();

        if (query.Length > 0)
        {
            filtered = filtered.Where(user =>
                user.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // Najpierw filtracja, potem policzenie wynikow, nastepnie stronicowanie.
        User[] matches = filtered.OrderBy(user => user.Id).ToArray();
        UserDto[] items = matches
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToArray();

        int totalPages = matches.Length == 0 ? 0 : (matches.Length + pageSize - 1) / pageSize;
        return TypedResults.Ok(new PagedResponse<UserDto>(
            items, page, pageSize, matches.Length, totalPages));
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Name, user.Email);
}
