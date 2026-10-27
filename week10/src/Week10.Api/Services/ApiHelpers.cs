using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Week10.Api.Contracts;
using Week10.Api.Data;
using Week10.Api.Domain;

namespace Week10.Api.Services;

public static class ApiHelpers
{
    public static IResult? Validate<T>(T request) where T : class
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        if (results.Count == 0) return null;
        var errors = new Dictionary<string, List<string>>();
        foreach (var error in results)
        foreach (var rawName in error.MemberNames.DefaultIfEmpty("body"))
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(rawName);
            if (!errors.TryGetValue(name, out var msgs)) errors[name] = msgs = [];
            msgs.Add(error.ErrorMessage ?? "Invalid value.");
        }
        return Results.ValidationProblem(errors.ToDictionary(k => k.Key, k => k.Value.ToArray()));
    }

    public static IResult Bad(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    public static IResult PageError() => Bad("pagination", "Page must be 1..10000 and pageSize 1..50.");
    public static bool IsValidPage(int page, int pageSize) => page is >= 1 and <= 10000 && pageSize is >= 1 and <= 50;

    public static int? CurrentId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    public static bool IsAdmin(ClaimsPrincipal principal) => principal.IsInRole("Admin");

    public static UserDto UserDto(User user) => new(user.Id, user.Username, user.Email, user.Role);
    public static TaskDto TaskDto(TaskItem task) => new(task.Id, task.Title, task.Description,
        task.Status.ToString(), task.DueAtUtc, task.OwnerId,
        task.TaskTags.Select(join => new TagDto(join.Tag.Id, join.Tag.Name)).OrderBy(x => x.Id).ToArray());

    public static PagedDto<T> Paged<T>(IReadOnlyList<T> records, int page, int pageSize, int total) =>
        new(records, page, pageSize, total, total == 0 ? 0 : (total + pageSize - 1) / pageSize);

    public static async Task<IResult?> CheckTags(AppDbContext db, int[] ids, CancellationToken ct)
    {
        if (ids.Any(x => x < 1) || ids.Distinct().Count() != ids.Length)
            return Bad("tagIds", "Tag IDs must be unique positive integers.");
        if (ids.Length > 20) return Bad("tagIds", "Maximum 20 tags.");
        var count = await db.Tags.CountAsync(x => ids.Contains(x.Id), ct);
        return count == ids.Length ? null : Bad("tagIds", "Unknown tag ID.");
    }
}
