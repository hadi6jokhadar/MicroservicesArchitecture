using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace IhsanDev.Shared.Kernel.Interfaces.Tenant;

/// <summary>
/// Provides access to the current tenant context for the request
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant information, or null if not in multi-tenant mode or no tenant resolved
    /// </summary>
    TenantInfo? CurrentTenant { get; }

    /// <summary>
    /// Gets the tenant ID from the current request, or null if not available
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Indicates if the application is running in multi-tenant mode
    /// </summary>
    bool IsMultiTenantMode { get; }

    /// <summary>
    /// Indicates if a tenant has been successfully resolved for the current request
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// Sets the current tenant for the request (used by middleware)
    /// </summary>
    void SetTenant(TenantInfo? tenantInfo);
}
