using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Week10.Api.Contracts;
using Week10.Api.Data;
using Week10.Api.Domain;
using Week10.Api.Security;
using Week10.Api.Services;

namespace Week10.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");
        group.MapPost("/register", Register);
        app.MapPost("/users", Register).WithTags("Users"); // CREATE w CRUD użytkowników
        group.MapPost("/login", Login);
    }

    internal static async Task<IResult> Register(RegisterRequest request, AppDbContext db,
        IPasswordHasher<User> hasher, CancellationToken ct)
    {
        if (ApiHelpers.Validate(request) is { } error) return error;
        var name = request.Username.Trim();
        var email = request.Email.Trim();
        var normalizedName = name.ToUpperInvariant();
        var normalizedEmail = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.NormalizedUsername == normalizedName || x.NormalizedEmail == normalizedEmail, ct))
            return Results.Conflict(new { code = "already_exists", message = "Username or email is already registered." });
        var user = new User
        {
            Username = name, NormalizedUsername = normalizedName,
            Email = email, NormalizedEmail = normalizedEmail, Role = "User"
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) // Unique indexes protect competing registration requests.
        {
            return Results.Conflict(new { code = "already_exists", message = "Username or email is already registered." });
        }
        return Results.Created($"/users/{user.Id}", ApiHelpers.UserDto(user));
    }

    private static async Task<IResult> Login(LoginRequest request, AppDbContext db,
        IPasswordHasher<User> hasher, JwtService jwt, CancellationToken ct)
    {
        if (ApiHelpers.Validate(request) is { } error) return error;
        var normalized = request.Username.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedUsername == normalized, ct);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
            return Results.Unauthorized();
        return Results.Ok(jwt.Issue(user));
    }
}
