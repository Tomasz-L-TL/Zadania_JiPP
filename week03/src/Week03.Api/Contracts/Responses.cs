using Week03.Api.Domain;

namespace Week03.Api.Contracts;

public sealed record UserResponse(int Id, string Login, string Name, string Email, int Age)
{
    public static UserResponse FromDomain(User user) =>
        new(user.Id, user.Login, user.Name, user.Email, user.Age);
}

public sealed record TaskResponse(int Id, string Title, string? Description, int Priority)
{
    public static TaskResponse FromDomain(TaskItem item) =>
        new(item.Id, item.Title, item.Description, item.Priority);
}
