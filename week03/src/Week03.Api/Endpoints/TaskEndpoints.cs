using Week03.Api.Contracts;
using Week03.Api.Domain;
using Week03.Api.Dto;
using Week03.Api.Repositories;
using Week03.Api.Validation;

namespace Week03.Api.Endpoints;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Tasks");

        group.MapPost("", (CreateTaskRequest input, ITaskRepository tasks) =>
        {
            var candidate = new TaskItem(
                Id: 0,
                Title: input.Title!.Trim(),
                Description: input.Description?.Trim(),
                Priority: input.Priority!.Value,
                CreatedAtUtc: DateTimeOffset.UtcNow);

            var saved = tasks.Add(candidate);
            return Results.Created($"/api/tasks/{saved.Id}", TaskResponse.FromDomain(saved));
        }).AddEndpointFilter<ValidationFilter<CreateTaskRequest>>();

        group.MapGet("", (ITaskRepository tasks) =>
            Results.Ok(tasks.GetAll().Select(TaskResponse.FromDomain).ToArray()));

        group.MapGet("/{id:int}", (int id, ITaskRepository tasks) =>
        {
            var task = tasks.GetById(id);
            return task is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Nie znaleziono zadania.")
                : Results.Ok(TaskResponse.FromDomain(task));
        });

        return group;
    }
}
