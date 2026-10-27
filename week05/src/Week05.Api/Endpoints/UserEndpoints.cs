using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Week05.Api.Contracts;
using Week05.Api.Data;
using Week05.Api.Domain;
using Week05.Api.Validation;

namespace Week05.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Users");
        users.MapPost("", Create).WithName("CreateUser");
        users.MapGet("", GetAll).WithName("GetUsers");
        users.MapGet("/{id:int}", GetById).WithName("GetUserById");
        users.MapPut("/{id:int}", Replace).WithName("ReplaceUser");
        users.MapPatch("/{id:int}", Patch).WithName("PatchUser");
        users.MapDelete("/{id:int}", Delete).WithName("DeleteUser");
        return users;
    }

    private static async Task<IResult> Create(
        CreateUserRequest? request, UsersDbContext db, CancellationToken ct)
    {
        if (request is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["JSON object is required."] });

        var errors = UserValidator.Validate(request.Name, request.Email, request.Age);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = new User(request.Name!, request.Email!, request.Age!.Value);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return DuplicateEmail();
        }

        return Results.CreatedAtRoute(
            routeName: "GetUserById",
            routeValues: new { id = user.Id },
            value: UserResponse.FromDomain(user));
    }

    private static async Task<IResult> GetAll(
        UsersDbContext db,
        CancellationToken ct,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? minAge = null,
        [FromQuery] int? maxAge = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (page is < 1 or > 100_000)
            errors["page"] = ["Page must be between 1 and 100000."];
        if (pageSize is < 1 or > 100)
            errors["pageSize"] = ["PageSize must be between 1 and 100."];
        if (search is { Length: > 100 })
            errors["search"] = ["Search must not exceed 100 characters."];
        if (minAge is < 18 or > 120)
            errors["minAge"] = ["MinAge must be between 18 and 120."];
        if (maxAge is < 18 or > 120)
            errors["maxAge"] = ["MaxAge must be between 18 and 120."];
        if (minAge.HasValue && maxAge.HasValue && minAge > maxAge)
            errors["ageRange"] = ["MinAge cannot exceed MaxAge."];

        var orderField = string.IsNullOrWhiteSpace(sortBy) ? "id" : sortBy.Trim().ToLowerInvariant();
        var direction = string.IsNullOrWhiteSpace(sortDir) ? "asc" : sortDir.Trim().ToLowerInvariant();
        if (orderField is not ("id" or "name" or "email" or "age" or "createdat"))
            errors["sortBy"] = ["SortBy must be id, name, email, age or createdAt."];
        if (direction is not ("asc" or "desc"))
            errors["sortDir"] = ["SortDir must be asc or desc."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        IQueryable<User> query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim().ToLowerInvariant();
            query = query.Where(user =>
                user.Name.ToLower().Contains(needle) ||
                user.Email.ToLower().Contains(needle));
        }
        if (minAge.HasValue) query = query.Where(user => user.Age >= minAge.Value);
        if (maxAge.HasValue) query = query.Where(user => user.Age <= maxAge.Value);

        var count = await query.CountAsync(ct);
        query = (orderField, direction) switch
        {
            ("name", "asc") => query.OrderBy(u => u.Name).ThenBy(u => u.Id),
            ("name", "desc") => query.OrderByDescending(u => u.Name).ThenBy(u => u.Id),
            ("email", "asc") => query.OrderBy(u => u.Email).ThenBy(u => u.Id),
            ("email", "desc") => query.OrderByDescending(u => u.Email).ThenBy(u => u.Id),
            ("age", "asc") => query.OrderBy(u => u.Age).ThenBy(u => u.Id),
            ("age", "desc") => query.OrderByDescending(u => u.Age).ThenBy(u => u.Id),
            ("createdat", "asc") => query.OrderBy(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            ("createdat", "desc") => query.OrderByDescending(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            ("id", "desc") => query.OrderByDescending(u => u.Id),
            _ => query.OrderBy(u => u.Id)
        };

        var results = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(user => new UserResponse(user.Id, user.Name, user.Email,
                user.Age, user.CreatedAtUtc, user.UpdatedAtUtc))
            .ToListAsync(ct);
        var totalPages = count == 0 ? 0 : (int)Math.Ceiling((double)count / pageSize);
        return Results.Ok(new PagedResponse<UserResponse>(
            results, page, pageSize, count, totalPages,
            orderField == "createdat" ? "createdAt" : orderField, direction));
    }

    private static async Task<IResult> GetById(int id, UsersDbContext db, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        var result = await db.Users.AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserResponse(user.Id, user.Name, user.Email,
                user.Age, user.CreatedAtUtc, user.UpdatedAtUtc))
            .SingleOrDefaultAsync(ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> Replace(
        int id, UpdateUserRequest? request, UsersDbContext db, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        if (request is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["JSON object is required."] });
        var errors = UserValidator.Validate(request.Name, request.Email, request.Age);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();
        user.UpdateProfile(request.Name!, request.Email!, request.Age!.Value);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return DuplicateEmail(); }
        return Results.Ok(UserResponse.FromDomain(user));
    }

    private static async Task<IResult> Patch(
        int id, PatchUserRequest? request, UsersDbContext db, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        if (request is null || (request.Name is null && request.Email is null && request.Age is null))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            { ["body"] = ["At least one updatable property must be provided."] });

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();
        var name = request.Name ?? user.Name;
        var email = request.Email ?? user.Email;
        var age = request.Age ?? user.Age;
        var errors = UserValidator.Validate(name, email, age);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        user.UpdateProfile(name, email, age);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return DuplicateEmail(); }
        return Results.Ok(UserResponse.FromDomain(user));
    }

    private static async Task<IResult> Delete(int id, UsersDbContext db, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static IResult InvalidId() => Results.ValidationProblem(new Dictionary<string, string[]>
    { ["id"] = ["Id must be positive."] });

    private static IResult DuplicateEmail() =>
        Results.Conflict(new ApiError("duplicate_email", "A user with this email already exists."));

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 };
}
