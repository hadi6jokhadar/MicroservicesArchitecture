using IhsanDev.Shared.Kernel.Entities;

namespace Identity.Domain.Entities;

public class Role : BaseEntity
{
    public required string Name { get; set; }
    
    public required string NormalizedName { get; set; }
    
    public string? Description { get; set; }
    
    /// <summary>
    /// System roles (SuperAdmin, Admin, User) cannot be deleted
    /// </summary>
    public bool IsSystemRole { get; set; } = false;
    
    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RoleClaim> RoleClaims { get; set; } = [];
}
