using System.Globalization;
using Week07.Api.Auth;
using Week07.Api.Users;

namespace Week07.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization();
        users.MapGet("/me", Me);
        users.MapGet("", List).RequireAuthorization(Policies.AdminOnly);
        users.MapGet("/{id:int}", GetById).RequireAuthorization(Policies.AdminOnly);
        users.MapDelete("/{id:int}", Delete).RequireAuthorization(Policies.AdminOnly);
    }

    private static IResult Me(HttpContext context, IUserStore store)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (!int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            return Results.Unauthorized();
        var current = store.FindById(id);
        return current is null ? Results.Unauthorized() : Results.Ok(PublicUser.From(current));
    }

    private static IResult List(IUserStore store) =>
        Results.Ok(store.GetAll().Select(PublicUser.From).ToArray());

    private static IResult GetById(int id, IUserStore store)
    {
        var user = store.FindById(id);
        return user is null ? Results.NotFound() : Results.Ok(PublicUser.From(user));
    }

    private static IResult Delete(int id, HttpContext context, IUserStore store)
    {
        if (int.TryParse(context.User.FindFirst("sub")?.Value, out var selfId) && selfId == id)
            return Results.Problem(statusCode: 409, title: "Nie można usunąć własnego konta administratora.");
        return store.Delete(id) ? Results.NoContent() : Results.NotFound();
    }
}
