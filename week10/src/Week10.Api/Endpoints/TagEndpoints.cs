using Microsoft.EntityFrameworkCore;
using Week10.Api.Contracts;
using Week10.Api.Data;
using Week10.Api.Domain;
using Week10.Api.Services;

namespace Week10.Api.Endpoints;

public static class TagEndpoints
{
    public static void MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tags", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Tags.AsNoTracking().OrderBy(x => x.Id)
                .Select(x => new TagDto(x.Id, x.Name)).ToListAsync(ct))).RequireAuthorization();
        app.MapPost("/tags", async (CreateTagRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (ApiHelpers.Validate(request) is { } error) return error;
            var name = request.Name.Trim();
            if (name.Length == 0) return ApiHelpers.Bad("name", "Tag name is required.");
            if (await db.Tags.AnyAsync(x => x.NormalizedName == name.ToUpperInvariant(), ct))
                return Results.Conflict(new { code = "tag_exists" });
            var tag = new Tag { Name = name, NormalizedName = name.ToUpperInvariant() };
            db.Tags.Add(tag);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Results.Conflict(new { code = "tag_exists" }); }
            return Results.Created($"/tags/{tag.Id}", new TagDto(tag.Id, tag.Name));
        }).RequireAuthorization("AdminOnly");
    }
}
