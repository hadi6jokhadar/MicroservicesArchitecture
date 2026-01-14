using IhsanDev.Shared.Kernel.Entities;

namespace Identity.Domain.Entities;

/// <summary>
/// Junction table for many-to-many relationship between Users and Roles
/// </summary>
public class UserRole : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
