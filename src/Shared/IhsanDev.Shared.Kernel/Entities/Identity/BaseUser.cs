using IhsanDev.Shared.Kernel.Enums.Identity;

namespace IhsanDev.Shared.Kernel.Entities.Identity;

public abstract class BaseUser : BaseEntity
{    
    public required string FirstName { get; set; }
    
    public required string LastName { get; set; }

    public UserRole Role { get; set; } = UserRole.User;
    
    public string? RefreshToken { get; set; }
    
    public DateTime? RefreshTokenExpiryTime { get; set; }
    
    public string? FirebaseToken { get; set; }
    
    public DateTime? LastLogin { get; set; }
}