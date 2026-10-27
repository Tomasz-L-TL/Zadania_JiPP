using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Week07.Api.Users;

namespace Week07.Api.Auth;

public sealed record IssuedToken(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc);

public sealed class TokenService(JwtSettings settings)
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Create(StoredUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddMinutes(settings.ExpiryMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiry.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim("email", user.Email),
                new Claim("role", user.Role),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            ]),
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256)
        };
        return new IssuedToken(_handler.CreateToken(descriptor), "Bearer", expiry);
    }
}
