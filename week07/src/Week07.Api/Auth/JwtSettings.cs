using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Week07.Api.Auth;

public sealed class JwtSettings
{
    public string Issuer { get; }
    public string Audience { get; }
    public int ExpiryMinutes { get; }
    public SymmetricSecurityKey SigningKey { get; }
    public IReadOnlyList<SecurityKey> ValidationKeys { get; }

    private JwtSettings(string issuer, string audience, int expiryMinutes,
        SymmetricSecurityKey signingKey, IReadOnlyList<SecurityKey> validationKeys)
    {
        Issuer = issuer;
        Audience = audience;
        ExpiryMinutes = expiryMinutes;
        SigningKey = signingKey;
        ValidationKeys = validationKeys;
    }

    public static JwtSettings Load(IConfiguration config)
    {
        var issuer = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Jwt:Issuer i Jwt:Audience muszą być skonfigurowane.");

        if (!int.TryParse(config["Jwt:ExpiryMinutes"], out var minutes) || minutes is < 5 or > 60)
            throw new InvalidOperationException("Jwt:ExpiryMinutes musi być liczbą z zakresu 5–60.");

        var primary = DecodeKey(config["Jwt:SigningKey"], "Jwt:SigningKey");
        var previousStrings = config.GetSection("Jwt:PreviousSigningKeys").Get<string[]>() ?? [];
        if (previousStrings.Length > 3)
            throw new InvalidOperationException("Obsługiwane są maksymalnie trzy poprzednie klucze.");

        var keys = new List<SymmetricSecurityKey> { primary };
        for (var i = 0; i < previousStrings.Length; i++)
            keys.Add(DecodeKey(previousStrings[i], $"Jwt:PreviousSigningKeys:{i}"));
        if (keys.Select(key => key.KeyId).Distinct(StringComparer.Ordinal).Count() != keys.Count)
            throw new InvalidOperationException("Klucze JWT muszą być różne.");

        return new JwtSettings(issuer, audience, minutes, primary, keys.Cast<SecurityKey>().ToArray());
    }

    private static SymmetricSecurityKey DecodeKey(string? base64, string setting)
    {
        // No secret contents in diagnostics (including when invalid).
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException($"Brak {setting}. Podaj losowy klucz Base64 poza repozytorium.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{setting} musi być poprawnym Base64.");
        }
        if (bytes.Length < 32)
            throw new InvalidOperationException($"{setting} musi zawierać co najmniej 32 losowe bajty.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(bytes).AsSpan(0, 12)).ToLowerInvariant();
        return new SymmetricSecurityKey(bytes) { KeyId = fingerprint };
    }
}
