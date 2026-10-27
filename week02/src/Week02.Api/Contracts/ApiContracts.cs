namespace Week02.Api.Contracts;

public sealed record ApiError(string Code, string Message);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record UserDto(int Id, string Name, string Email);

public sealed record TaskDto(int Id, string Title, bool Completed, int AssigneeId);

// Kontrakt v1 jest zachowany — cena to liczba, flaga nazywa się "available".
public sealed record ProductV1Dto(int Id, string Name, decimal Price, bool Available);

// Wersja v2 rozszerza reprezentację bez modyfikowania DTO w v1.
public sealed record MoneyV2Dto(decimal Amount, string Currency);
public sealed record ProductV2Dto(
    int Id, string Name, MoneyV2Dto Price, string Category, bool InStock);
