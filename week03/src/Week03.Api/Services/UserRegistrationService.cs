using Week03.Api.Domain;
using Week03.Api.Dto;
using Week03.Api.Repositories;

namespace Week03.Api.Services;

public sealed record RegistrationResult(User? User, bool LoginAlreadyExists);

// Ćwiczenie 3: reguła biznesowa poza endpointem i adnotacjami.
public sealed class UserRegistrationService(IUserRepository users)
{
    public RegistrationResult Register(CreateUserRequest request)
    {
        // Filtr wcześniej waliduje DTO. Dodatkowo rozdzielamy DTO i encję domenową.
        var candidate = new User(
            Id: 0,
            Login: request.Login!.Trim(),
            Name: request.Name!.Trim(),
            Email: request.Email!.Trim(),
            Age: request.Age!.Value,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        return users.TryAdd(candidate, out var saved)
            ? new RegistrationResult(saved, LoginAlreadyExists: false)
            : new RegistrationResult(null, LoginAlreadyExists: true);
    }
}
