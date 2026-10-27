using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Week07.Api.Users;

namespace Week07.Api.Auth;

public sealed record AuthRegistrationResult(PublicUser? User, bool Conflict);

public sealed class AuthService(IUserStore users, IPasswordHasher<StoredUser> hasher)
{
    public AuthRegistrationResult Register(string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var placeholder = new StoredUser(0, normalizedEmail, "", "User", DateTimeOffset.UtcNow);
        var hash = hasher.HashPassword(placeholder, password);
        var stored = users.TryCreate(normalizedEmail, hash, "User");
        return stored is null
            ? new AuthRegistrationResult(null, Conflict: true)
            : new AuthRegistrationResult(PublicUser.From(stored), Conflict: false);
    }

    public StoredUser? Login(string email, string password)
    {
        var user = users.FindByEmail(email.Trim().ToLowerInvariant());
        if (user is null) return null;

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded
            ? user : null;
    }

    public void BootstrapAdmin(string email, string password)
    {
        var address = new EmailAddressAttribute();
        if (email.Length > 254 || !address.IsValid(email))
            throw new InvalidOperationException("Niepoprawny BootstrapAdmin:Email.");
        if (password.Length is < 12 or > 128)
            throw new InvalidOperationException("BootstrapAdmin:Password musi mieć 12–128 znaków.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var placeholder = new StoredUser(0, normalizedEmail, "", "Admin", DateTimeOffset.UtcNow);
        var hash = hasher.HashPassword(placeholder, password);
        if (users.TryCreate(normalizedEmail, hash, "Admin") is null)
            throw new InvalidOperationException("Konflikt adresu BootstrapAdmin:Email.");
    }
}
