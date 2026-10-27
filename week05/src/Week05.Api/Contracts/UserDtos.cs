using Week05.Api.Domain;

namespace Week05.Api.Contracts;

public sealed record CreateUserRequest(string? Name, string? Email, int? Age);
public sealed record UpdateUserRequest(string? Name, string? Email, int? Age);
public sealed record PatchUserRequest(string? Name, string? Email, int? Age);

public sealed record UserResponse(
    int Id, string Name, string Email, int Age,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc)
{
    public static UserResponse FromDomain(User user) =>
        new(user.Id, user.Name, user.Email, user.Age,
            user.CreatedAtUtc, user.UpdatedAtUtc);
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    string SortBy,
    string SortDir);

public sealed record ApiError(string Code, string Message);
