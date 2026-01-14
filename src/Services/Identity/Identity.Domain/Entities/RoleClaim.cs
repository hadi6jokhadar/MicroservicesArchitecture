using IhsanDev.Shared.Kernel.Entities;

namespace Identity.Domain.Entities;

/// <summary>
/// Junction table for many-to-many relationship between Roles and Claims
/// </summary>
public class RoleClaim : BaseEntity
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    
    public int ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;
}
