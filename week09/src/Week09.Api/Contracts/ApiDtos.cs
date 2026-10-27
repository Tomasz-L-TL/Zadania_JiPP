using System.ComponentModel.DataAnnotations;
using Week09.Api.Domain;

namespace Week09.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, EmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [Required, MinLength(8), MaxLength(128)]
    public string? Password { get; set; }
}

public sealed class LoginRequest
{
    [Required]
    public string? Email { get; set; }

    [Required]
    public string? Password { get; set; }
}

public sealed class NoteRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Body { get; set; }
}

public sealed record UserResponse(Guid Id, string Email);
public sealed record ProfileResponse(Guid Id, string Email, string Role);
public sealed record TokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc);
public sealed record NoteResponse(Guid Id, string Title, string Body, DateTimeOffset CreatedAtUtc)
{
    public static NoteResponse FromNote(Note note) =>
        new(note.Id, note.Title, note.Body, note.CreatedAtUtc);
}
public sealed record StatsResponse(int Users, int Notes);
