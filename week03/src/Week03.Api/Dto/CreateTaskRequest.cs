using System.ComponentModel.DataAnnotations;

namespace Week03.Api.Dto;

public sealed class CreateTaskRequest
{
    [Required(ErrorMessage = "Tytuł zadania jest wymagany.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "Tytuł musi mieć 3–120 znaków.")]
    public string? Title { get; init; }

    [StringLength(500, ErrorMessage = "Opis może mieć maksymalnie 500 znaków.")]
    public string? Description { get; init; }

    [Required(ErrorMessage = "Priorytet jest wymagany.")]
    [Range(1, 5, ErrorMessage = "Priorytet musi mieścić się w przedziale 1–5.")]
    public int? Priority { get; init; }
}
