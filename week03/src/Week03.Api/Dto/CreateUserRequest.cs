using System.ComponentModel.DataAnnotations;

namespace Week03.Api.Dto;

// Celowo wyłącznie pola, które klient może podać. Id i CreatedAtUtc należą do domeny.
public sealed class CreateUserRequest
{
    [Required(ErrorMessage = "Login jest wymagany.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Login musi mieć 3–30 znaków.")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Login może zawierać litery łacińskie, cyfry, kropkę, _ i -.")]
    public string? Login { get; init; }

    [Required(ErrorMessage = "Imię jest wymagane.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Imię musi mieć 2–80 znaków.")]
    public string? Name { get; init; }

    [Required(ErrorMessage = "E-mail jest wymagany.")]
    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    [StringLength(120, ErrorMessage = "E-mail może mieć maksymalnie 120 znaków.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Wiek jest wymagany.")]
    [Range(18, 120, ErrorMessage = "Wiek musi mieścić się w przedziale 18–120.")]
    public int? Age { get; init; }
}
