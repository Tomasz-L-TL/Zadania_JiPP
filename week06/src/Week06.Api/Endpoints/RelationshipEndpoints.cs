using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Week06.Api.Contracts;
using Week06.Api.Data;
using Week06.Api.Queries;

namespace Week06.Api.Endpoints;

public static class RelationshipEndpoints
{
    public static IEndpointRouteBuilder MapRelationshipEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/users", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Users.AsNoTracking().OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name }).ToListAsync(ct)));

        api.MapGet("/tags", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Tags.AsNoTracking().OrderBy(x => x.Id)
                .Select(x => new TagResponse(x.Id, x.Name)).ToListAsync(ct)));

        // E2: Include + 404 + DTO.
        api.MapGet("/users/{id:int}/tasks", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (id <= 0) return Results.BadRequest();
            var user = await UserQueries.WithTasks(db, id).FirstOrDefaultAsync(ct);
            return user is null ? Results.NotFound() : Results.Ok(new UserTasksResponse(
                user.Id, user.Name,
                user.Tasks.OrderBy(t => t.Id)
                    .Select(t => new TaskResponse(t.Id, t.Title, t.IsCompleted)).ToArray()));
        });

        // E3: ThenInclude and DTO nested three levels.
        api.MapGet("/users/{id:int}/details", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (id <= 0) return Results.BadRequest();
            var user = await UserQueries.WithComments(db, id).FirstOrDefaultAsync(ct);
            return user is null ? Results.NotFound() : Results.Ok(new UserDetailsResponse(
                user.Id, user.Name,
                user.Tasks.OrderBy(t => t.Id)
                    .Select(t => new TaskDetailsResponse(t.Id, t.Title, t.IsCompleted,
                        t.Comments.OrderBy(c => c.Id)
                            .Select(c => new CommentResponse(c.Id, c.Text)).ToArray())).ToArray()));
        });

        // E5: full, read-only graph (two nested collections).
        api.MapGet("/users/{id:int}/graph", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (id <= 0) return Results.BadRequest();
            var user = await UserQueries.FullGraph(db, id).FirstOrDefaultAsync(ct);
            return user is null ? Results.NotFound() : Results.Ok(new UserGraphResponse(
                user.Id, user.Name,
                user.Tasks.OrderBy(t => t.Id).Select(t => new TaskGraphResponse(
                    t.Id, t.Title, t.IsCompleted,
                    t.Comments.OrderBy(c => c.Id).Select(c => new CommentResponse(c.Id, c.Text)).ToArray(),
                    t.Tags.OrderBy(tag => tag.Id).Select(tag => new TagResponse(tag.Id, tag.Name)).ToArray()
                )).ToArray()));
        });

        // E5: projection pushes aggregation to SQL and avoids loading entities and full comments.
        api.MapGet("/users/{id:int}/summary", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (id <= 0) return Results.BadRequest();
            var summary = await UserQueries.Summary(db, id).FirstOrDefaultAsync(ct);
            return summary is null ? Results.NotFound() : Results.Ok(summary);
        });

        // E4: a repeat assignment is idempotent. DB-level unique composite PK prevents duplicates.
        api.MapPost("/tasks/{taskId:int}/tags/{tagId:int}", async (
            int taskId, int tagId, AppDbContext db, CancellationToken ct) =>
        {
            if (taskId <= 0 || tagId <= 0) return Results.BadRequest();
            var task = await db.Tasks.Include(t => t.Tags)
                .FirstOrDefaultAsync(t => t.Id == taskId, ct);
            if (task is null) return Results.NotFound();
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId, ct);
            if (tag is null) return Results.NotFound();
            if (task.Tags.Any(t => t.Id == tagId)) return Results.NoContent();

            task.Tags.Add(tag);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                // When concurrent requests race, the database PK is authoritative.
                var attached = await db.Database.SqlQueryRaw<int>(
                    "SELECT 1 AS Value FROM TaskTags WHERE TaskItemId={0} AND TagId={1}", taskId, tagId)
                    .AnyAsync(ct);
                if (!attached) throw;
            }
            return Results.NoContent();
        });

        return app;
    }
}
