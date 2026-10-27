using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Week10.Api.Contracts;
using Week10.Api.Data;
using Week10.Api.Domain;
using Week10.Api.Services;

namespace Week10.Api.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tasks").RequireAuthorization().WithTags("Tasks");
        group.MapGet("", List);
        group.MapGet("/{id:int}", Get);
        group.MapPost("", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
    }

    private static IQueryable<TaskItem> Visible(AppDbContext db, ClaimsPrincipal principal) =>
        ApiHelpers.IsAdmin(principal) ? db.Tasks : db.Tasks.Where(x => x.OwnerId == ApiHelpers.CurrentId(principal));

    private static async Task<IResult> List(AppDbContext db, ClaimsPrincipal principal,
        int page = 1, int pageSize = 10, string? status = null, int? ownerId = null, CancellationToken ct = default)
    {
        if (!ApiHelpers.IsValidPage(page, pageSize)) return ApiHelpers.PageError();
        var q = Visible(db, principal).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<WorkStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
                return ApiHelpers.Bad("status", "Expected Todo, InProgress or Done.");
            q = q.Where(x => x.Status == parsed);
        }
        if (ownerId.HasValue) q = q.Where(x => x.OwnerId == ownerId.Value);
        var count = await q.CountAsync(ct);
        var records = await q.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(x => x.TaskTags).ThenInclude(x => x.Tag).AsSplitQuery().ToListAsync(ct);
        return Results.Ok(ApiHelpers.Paged(records.Select(ApiHelpers.TaskDto).ToArray(), page, pageSize, count));
    }

    private static async Task<IResult> Get(int id, AppDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var task = await Visible(db, principal).AsNoTracking()
            .Include(x => x.TaskTags).ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return task is null ? Results.NotFound() : Results.Ok(ApiHelpers.TaskDto(task));
    }

    private static IResult? ValidateTask(TaskWriteRequest request)
    {
        if (ApiHelpers.Validate(request) is { } error) return error;
        if (request.TagIds is null) return ApiHelpers.Bad("tagIds", "Tag IDs cannot be null.");
        if (!Enum.TryParse<WorkStatus>(request.Status, true, out var value) || !Enum.IsDefined(value))
            return ApiHelpers.Bad("status", "Expected Todo, InProgress or Done.");
        return null;
    }

    private static async Task<IResult> Create(TaskWriteRequest request, AppDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (ValidateTask(request) is { } error) return error;
        var creatorId = ApiHelpers.CurrentId(principal)!.Value;
        var ownerId = request.OwnerId ?? creatorId;
        if (!ApiHelpers.IsAdmin(principal) && ownerId != creatorId) return Results.Forbid();
        if (!await db.Users.AnyAsync(x => x.Id == ownerId, ct)) return ApiHelpers.Bad("ownerId", "Unknown owner.");
        if (await ApiHelpers.CheckTags(db, request.TagIds, ct) is { } tagError) return tagError;
        var task = new TaskItem
        {
            Title = request.Title.Trim(), Description = request.Description?.Trim(),
            Status = Enum.Parse<WorkStatus>(request.Status, true), DueAtUtc = request.DueAtUtc,
            OwnerId = ownerId,
            TaskTags = request.TagIds.Select(x => new TaskTag { TagId = x }).ToList()
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        var result = await db.Tasks.AsNoTracking().Include(x => x.TaskTags).ThenInclude(x => x.Tag)
            .SingleAsync(x => x.Id == task.Id, ct);
        return Results.Created($"/tasks/{task.Id}", ApiHelpers.TaskDto(result));
    }

    private static async Task<IResult> Update(int id, TaskWriteRequest request, AppDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (ValidateTask(request) is { } error) return error;
        var task = await Visible(db, principal).Include(x => x.TaskTags)
            .ThenInclude(x => x.Tag).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Results.NotFound();
        if (request.OwnerId.HasValue && request.OwnerId != task.OwnerId)
            return ApiHelpers.Bad("ownerId", "Owner cannot be changed by task update.");
        if (await ApiHelpers.CheckTags(db, request.TagIds, ct) is { } tagError) return tagError;
        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Status = Enum.Parse<WorkStatus>(request.Status, true);
        task.DueAtUtc = request.DueAtUtc;
        var desired = request.TagIds.ToHashSet();
        foreach (var prior in task.TaskTags.Where(x => !desired.Contains(x.TagId)).ToList())
            db.TaskTags.Remove(prior);
        var current = task.TaskTags.Select(x => x.TagId).ToHashSet();
        foreach (var tagId in desired.Where(x => !current.Contains(x)))
            task.TaskTags.Add(new TaskTag { TagId = tagId });
        await db.SaveChangesAsync(ct);
        var response = await db.Tasks.AsNoTracking().Include(x => x.TaskTags).ThenInclude(x => x.Tag)
            .SingleAsync(x => x.Id == id, ct);
        return Results.Ok(ApiHelpers.TaskDto(response));
    }

    private static async Task<IResult> Delete(int id, AppDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var task = await Visible(db, principal).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Results.NotFound();
        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
