namespace IhsanDev.Shared.Infrastructure.Services.Identity;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
    /// <summary>The caller's own tenant (from the "tenant_id" JWT claim). Null for a global user (SuperAdmin/Service).</summary>
    string? TenantId { get; }
    IEnumerable<string> Roles { get; }
    bool HasRole(string role);
}