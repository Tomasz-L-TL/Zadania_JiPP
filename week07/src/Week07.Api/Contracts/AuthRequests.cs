using System.ComponentModel.DataAnnotations;

namespace Week07.Api.Contracts;

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "E-mail jest wymagany.")]
    [EmailAddress(ErrorMessage = "Niepoprawny e-mail.")]
    [MaxLength(254, ErrorMessage = "E-mail jest zbyt długi.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Hasło jest wymagane.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Hasło musi mieć 12–128 znaków.")]
    public string? Password { get; init; }
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "E-mail jest wymagany.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Hasło jest wymagane.")]
    public string? Password { get; init; }
}
