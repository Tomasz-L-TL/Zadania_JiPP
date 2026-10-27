using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Week10.Api.Contracts;
using Week10.Api.Domain;

namespace Week10.Api.Security;

public sealed class JwtService(IConfiguration config)
{
    public TokenResponse Issue(User user)
    {
        var issuer = config["Jwt:Issuer"]!;
        var audience = config["Jwt:Audience"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!));
        var duration = TimeSpan.FromMinutes(30);
        var token = new JwtSecurityToken(issuer: issuer, audience: audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.Add(duration),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", (int)duration.TotalSeconds);
    }
}
