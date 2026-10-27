using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.HttpResults;
using Week02.Api.Contracts;
using Week02.Api.Domain;
using Week02.Api.Repositories;

namespace Week02.Api.Endpoints;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this WebApplication app)
    {
        var tasks = app.MapGroup("/tasks").WithTags("Tasks");
        tasks.MapGet("", ListTasks).WithName("ListTasks");
        tasks.MapGet("/{id:int}", GetTaskById).WithName("GetTaskById");
        return tasks;
    }

    private static Ok<IReadOnlyList<TaskDto>> ListTasks(ITaskCatalog catalog)
    {
        IReadOnlyList<TaskDto> tasks = catalog.GetAll().Select(ToDto).ToArray();
        return TypedResults.Ok(tasks);
    }

    private static Results<Ok<TaskDto>, BadRequest<ApiError>, NotFound<ApiError>> GetTaskById(
        int id, ITaskCatalog catalog)
    {
        if (id <= 0)
        {
            return TypedResults.BadRequest(new ApiError("invalid_id", "Identyfikator musi byc wiekszy od zera."));
        }

        WorkTask? task = catalog.GetById(id);
        if (task is null)
        {
            return TypedResults.NotFound(new ApiError("task_not_found", $"Zadanie o ID {id} nie istnieje."));
        }

        return TypedResults.Ok(ToDto(task));
    }

    private static TaskDto ToDto(WorkTask task) =>
        new(task.Id, task.Title, task.Completed, task.AssigneeId);
}
