using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Week09.Api.Contracts;
using Week09.Api.Data;
using Week09.Api.Domain;
using Week09.Api.Security;

namespace Week09.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/auth").WithTags("Auth");
        auth.MapPost("/register", Register);
        auth.MapPost("/login", Login);
        auth.MapGet("/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> Register(RegisterRequest request, Week09DbContext db,
        IPasswordHasher<AppUser> hasher, CancellationToken ct)
    {
        var errors = RequestValidation.Check(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var email = request.Email!.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email, ct))
            return Results.Problem(statusCode: 409, title: "Adres e-mail jest zajęty.");

        var user = new AppUser { Email = email };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Wyścig dwóch równoczesnych rejestracji nie powinien ujawniać błędów DB.
            if (await db.Users.AsNoTracking().AnyAsync(entry => entry.Email == email, ct))
                return Results.Problem(statusCode: 409, title: "Adres e-mail jest zajęty.");
            throw;
        }
        return Results.Created("/auth/me", new UserResponse(user.Id, user.Email));
    }

    private static async Task<IResult> Login(LoginRequest request, Week09DbContext db,
        IPasswordHasher<AppUser> hasher, JwtTokenService tokens, CancellationToken ct)
    {
        var errors = RequestValidation.Check(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var email = request.Email!.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Email == email, ct);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!)
            == PasswordVerificationResult.Failed)
        {
            return Results.Problem(statusCode: 401, title: "Nieprawidłowe dane logowania.");
        }
        return Results.Ok(tokens.Issue(user));
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, Week09DbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Results.Unauthorized();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new ProfileResponse(user.Id, user.Email, user.Role));
    }
}
