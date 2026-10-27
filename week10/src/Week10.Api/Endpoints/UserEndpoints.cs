using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Week10.Api.Contracts;
using Week10.Api.Data;
using Week10.Api.Services;

namespace Week10.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").RequireAuthorization().WithTags("Users");
        group.MapGet("", List).RequireAuthorization("AdminOnly");
        group.MapGet("/{id:int}", Get);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
    }

    private static async Task<IResult> List(AppDbContext db, int page = 1, int pageSize = 10,
        string? search = null, CancellationToken ct = default)
    {
        if (!ApiHelpers.IsValidPage(page, pageSize)) return ApiHelpers.PageError();
        if (search?.Length > 100) return ApiHelpers.Bad("search", "Max length is 100.");
        var q = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            q = q.Where(x => x.Username.Contains(needle) || x.Email.Contains(needle));
        }
        var total = await q.CountAsync(ct);
        var users = await q.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new UserDto(x.Id, x.Username, x.Email, x.Role)).ToListAsync(ct);
        return Results.Ok(ApiHelpers.Paged(users, page, pageSize, total));
    }

    private static async Task<IResult> Get(int id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (ApiHelpers.CurrentId(principal) != id && !ApiHelpers.IsAdmin(principal)) return Results.Forbid();
        var user = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new UserDto(x.Id, x.Username, x.Email, x.Role)).FirstOrDefaultAsync(ct);
        return user is null ? Results.NotFound() : Results.Ok(user);
    }

    private static async Task<IResult> Update(int id, UpdateUserRequest request,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (ApiHelpers.CurrentId(principal) != id && !ApiHelpers.IsAdmin(principal)) return Results.Forbid();
        if (ApiHelpers.Validate(request) is { } error) return error;
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return Results.NotFound();
        var email = request.Email.Trim();
        var normalized = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.Id != id && x.NormalizedEmail == normalized, ct))
            return Results.Conflict(new { code = "email_taken" });
        user.Email = email;
        user.NormalizedEmail = normalized;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { code = "email_taken" }); }
        return Results.Ok(ApiHelpers.UserDto(user));
    }

    private static async Task<IResult> Delete(int id, ClaimsPrincipal principal,
        AppDbContext db, CancellationToken ct)
    {
        if (ApiHelpers.CurrentId(principal) != id && !ApiHelpers.IsAdmin(principal)) return Results.Forbid();
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return Results.NotFound();
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
