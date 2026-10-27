namespace Week07.Api.Users;

// Internal domain entity. Never return or serialize directly in any API response.
public sealed record StoredUser(int Id, string Email, string PasswordHash, string Role, DateTimeOffset CreatedAtUtc);

public sealed record PublicUser(int Id, string Email, string Role)
{
    public static PublicUser From(StoredUser user) => new(user.Id, user.Email, user.Role);
}
