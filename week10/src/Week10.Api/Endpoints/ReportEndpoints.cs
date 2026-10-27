using Microsoft.EntityFrameworkCore;
using Week10.Api.Contracts;
using Week10.Api.Data;
using Week10.Api.Domain;
using Week10.Api.Services;

namespace Week10.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports").WithTags("Reports").RequireAuthorization("AdminOnly");
        group.MapGet("/tasks/by-status", async (AppDbContext db, CancellationToken ct) =>
        {
            var counts = await db.Tasks.AsNoTracking().GroupBy(x => x.Status)
                .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
            return Results.Ok(counts.Select(x => new StatusReportDto(x.Status.ToString(), x.Count))
                .OrderBy(x => x.Status).ToArray());
        });
        group.MapGet("/tasks/by-user", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Users.AsNoTracking().OrderBy(x => x.Id)
                .Select(x => new UserReportDto(x.Id, x.Username, x.Tasks.Count))
                .ToListAsync(ct)));
        group.MapGet("/tags/top", async (AppDbContext db, int limit = 10, CancellationToken ct = default) =>
        {
            if (limit is < 1 or > 100) return ApiHelpers.Bad("limit", "Expected 1..100.");
            var tags = await db.Tags.AsNoTracking()
                .Select(x => new TagReportDto(x.Id, x.Name, x.TaskTags.Count))
                .OrderByDescending(x => x.Count).ThenBy(x => x.TagId).Take(limit).ToListAsync(ct);
            return Results.Ok(tags);
        });
        group.MapGet("/tasks/overdue", async (AppDbContext db, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var records = await db.Tasks.AsNoTracking()
                .Where(x => x.DueAtUtc != null && x.DueAtUtc < now && x.Status != WorkStatus.Done)
                .OrderBy(x => x.DueAtUtc).ThenBy(x => x.Id)
                .Select(x => new OverdueDto(x.Id, x.Title, x.OwnerId, x.DueAtUtc!.Value)).ToListAsync(ct);
            return Results.Ok(records);
        });
    }
}
