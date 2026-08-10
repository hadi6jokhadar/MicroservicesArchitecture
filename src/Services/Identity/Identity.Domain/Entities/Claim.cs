using IhsanDev.Shared.Kernel.Entities;

namespace Identity.Domain.Entities;

public class Claim : BaseEntity
{
    public required string Name { get; set; }
    
    public required string NormalizedName { get; set; }
    
    public string? Description { get; set; }
    
    /// <summary>
    /// Claim type (e.g., "Permission", "Feature")
    /// </summary>
    public required string ClaimType { get; set; }
    
    /// <summary>
    /// Claim value (e.g., "users.delete", "files.upload")
    /// </summary>
    public required string ClaimValue { get; set; }
    
    /// <summary>
    /// If true, only SuperAdmin can assign this claim to users
    /// </summary>
    public bool IsSuperAdminOnly { get; set; } = false;

    /// <summary>
    /// If true, this claim was created by <see cref="Identity.Infrastructure.Seeding.SystemPermissionCatalog"/>
    /// and cannot be deleted or renamed via the admin API/UI — its ClaimValue is a literal contract
    /// referenced by another service's authorization policy code.
    /// </summary>
    public bool IsSystemClaim { get; set; } = false;

    // Navigation properties
    public ICollection<RoleClaim> RoleClaims { get; set; } = [];
}
