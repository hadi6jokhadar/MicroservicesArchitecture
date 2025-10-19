using IhsanDev.Shared.Kernel.Entities;

namespace Tenant.Domain.Entities;

/// <summary>
/// Represents tenant-specific configuration and settings
/// Each user can have exactly one tenant configuration
/// </summary>
public class TenantSettings : BaseEntity
{
    /// <summary>
    /// Unique identifier for the tenant (e.g., "tenant-001", "company-abc")
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Display name of the tenant
    /// </summary>
    public required string TenantName { get; set; }

    /// <summary>
    /// Foreign key to the Identity user who owns this tenant
    /// Each user can have exactly one tenant
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Start date when tenant subscription becomes active
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Expiration date for tenant subscription
    /// </summary>
    public DateTime ExpireDate { get; set; }

    /// <summary>
    /// JSON-serialized tenant-specific settings
    /// Includes: JWT settings, Database connection, CORS, and other service-level configuration
    /// Stored as string with value converter for get/set operations
    /// </summary>
    public required string Data { get; set; }

    /// <summary>
    /// Indicates if the tenant is currently active
    /// Inactive tenants cannot authenticate
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Check if tenant subscription has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpireDate;

    /// <summary>
    /// Check if tenant is valid (active and not expired)
    /// </summary>
    public bool IsValid => IsActive && !IsExpired;
}
