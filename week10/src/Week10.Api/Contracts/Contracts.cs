using System.ComponentModel.DataAnnotations;

namespace Week10.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$")]
    public string Username { get; set; } = "";
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = "";
    [Required, MinLength(12), MaxLength(128)]
    public string Password { get; set; } = "";
}

public sealed class LoginRequest
{
    [Required] public string Username { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public sealed class UpdateUserRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
}

public sealed class TaskWriteRequest
{
    [Required, StringLength(120, MinimumLength = 1)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    [Required] public string Status { get; set; } = "Todo";
    public DateTime? DueAtUtc { get; set; }
    public int[] TagIds { get; set; } = [];
    public int? OwnerId { get; set; }
}

public sealed class CreateTagRequest
{
    [Required, StringLength(40, MinimumLength = 1)] public string Name { get; set; } = "";
}

public sealed record UserDto(int Id, string Username, string Email, string Role);
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds);
public sealed record TagDto(int Id, string Name);
public sealed record TaskDto(int Id, string Title, string? Description,
    string Status, DateTime? DueAtUtc, int OwnerId, IReadOnlyList<TagDto> Tags);
public sealed record PagedDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record StatusReportDto(string Status, int Count);
public sealed record UserReportDto(int UserId, string Username, int Count);
public sealed record TagReportDto(int TagId, string Name, int Count);
public sealed record OverdueDto(int TaskId, string Title, int OwnerId, DateTime DueAtUtc);
