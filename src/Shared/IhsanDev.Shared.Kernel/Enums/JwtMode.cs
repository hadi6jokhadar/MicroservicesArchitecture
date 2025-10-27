namespace IhsanDev.Shared.Kernel.Enums;

/// <summary>
/// Defines how JWT validation is performed in multi-tenant scenarios
/// </summary>
public enum JwtMode
{
    /// <summary>
    /// All tenants share the same JWT secret from appsettings.json.
    /// Useful for superadmin access across all tenants.
    /// </summary>
    Shared = 0,
    
    /// <summary>
    /// Each tenant has its own JWT secret stored in Tenant Service.
    /// Provides complete JWT isolation between tenants.
    /// </summary>
    PerTenant = 1
}
