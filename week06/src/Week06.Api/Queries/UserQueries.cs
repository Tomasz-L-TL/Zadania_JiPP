using Microsoft.EntityFrameworkCore;
using Week06.Api.Contracts;
using Week06.Api.Data;
using Week06.Api.Domain;

namespace Week06.Api.Queries;

public static class UserQueries
{
    // Exercise 2: Include. Read-only response uses AsNoTracking.
    public static IQueryable<User> WithTasks(AppDbContext db, int id) =>
        db.Users.AsNoTracking()
            .Include(user => user.Tasks)
            .AsSplitQuery()
            .Where(user => user.Id == id);

    // Exercise 3: 3-level graph: User -> Tasks -> Comments.
    public static IQueryable<User> WithComments(AppDbContext db, int id) =>
        db.Users.AsNoTracking()
            .Include(user => user.Tasks)
                .ThenInclude(task => task.Comments)
            .AsSplitQuery()
            .Where(user => user.Id == id);

    // Exercise 5: full graph loads two collection branches; splitting avoids cartesian explosion.
    public static IQueryable<User> FullGraph(AppDbContext db, int id) =>
        db.Users.AsNoTracking()
            .Include(user => user.Tasks)
                .ThenInclude(task => task.Comments)
            .Include(user => user.Tasks)
                .ThenInclude(task => task.Tags)
            .AsSplitQuery()
            .Where(user => user.Id == id);

    // Exercise 5: lightweight server-side projection; no Include and no full entities.
    public static IQueryable<UserSummaryResponse> Summary(AppDbContext db, int id) =>
        db.Users.AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserSummaryResponse(
                user.Id, user.Name,
                user.Tasks.Count,
                user.Tasks.Count(task => task.IsCompleted),
                user.Tasks.OrderBy(task => task.Id)
                    .Select(task => new TaskLiteResponse(
                        task.Id, task.Title, task.Comments.Count, task.Tags.Count))
                    .ToList()));
}
