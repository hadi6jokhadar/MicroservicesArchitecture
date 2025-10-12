using IhsanDev.Shared.Kernel.Entities.Identity;
using IhsanDev.Shared.Kernel.Enums.Identity;

namespace Identity.Domain.Entities;

public class User : BaseUser
{
    public UserRole Role { get; set; } = UserRole.User;
    
    public string? RefreshToken { get; set; }
    
    public DateTime? RefreshTokenExpiryTime { get; set; }
    
    public string? FirebaseToken { get; set; }
    
    // Navigation properties for other microservices
    public string? ProfilePictureUrl { get; set; }
}