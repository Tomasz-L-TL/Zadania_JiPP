namespace Week03.Api.Domain;

public sealed record User(
    int Id,
    string Login,
    string Name,
    string Email,
    int Age,
    DateTimeOffset CreatedAtUtc);

public sealed record TaskItem(
    int Id,
    string Title,
    string? Description,
    int Priority,
    DateTimeOffset CreatedAtUtc);
