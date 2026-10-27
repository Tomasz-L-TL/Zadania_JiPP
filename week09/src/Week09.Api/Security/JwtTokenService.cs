using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Week09.Api.Domain;
using Week09.Api.Contracts;

namespace Week09.Api.Security;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public TokenResponse Issue(AppUser user)
    {
        var secret = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
        var issuer = configuration["Jwt:Issuer"] ?? "Week09.Api";
        var audience = configuration["Jwt:Audience"] ?? "Week09.Client";
        var expires = DateTimeOffset.UtcNow.AddMinutes(20);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var jwt = new JwtSecurityToken(
            issuer: issuer, audience: audience,
            claims: claims, notBefore: DateTime.UtcNow, expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), "Bearer", expires);
    }
}
