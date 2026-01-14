using System.Security.Claims;

namespace IhsanDev.Shared.Infrastructure.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? principal.FindFirst("sub")?.Value; // JWT standard claim
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value 
            ?? principal.FindFirst("email")?.Value; // JWT standard claim
    }

    public static string? GetRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Role)?.Value;
    }

    public static IEnumerable<string> GetRoles(this ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
    }

    public static bool HasRole(this ClaimsPrincipal principal, string role)
    {
        return principal.GetRoles().Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSuperAdmin(this ClaimsPrincipal principal)
    {
        return principal.HasRole("SuperAdmin");
    }

    public static int? GetUserIdAsInt(this ClaimsPrincipal principal)
    {
        var userId = principal.GetUserId();
        return int.TryParse(userId, out var id) ? id : null;
    }
}