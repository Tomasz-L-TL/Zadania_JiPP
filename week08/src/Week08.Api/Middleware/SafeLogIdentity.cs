using System.Security.Claims;

namespace Week08.Api.Middleware;

public static class SafeLogIdentity
{
    // Only authenticated opaque GUID user IDs are logged; never names or email addresses.
    public static string? GetUserId(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return null;

        var raw = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) && id != Guid.Empty
            ? id.ToString("D")
            : null;
    }
}
