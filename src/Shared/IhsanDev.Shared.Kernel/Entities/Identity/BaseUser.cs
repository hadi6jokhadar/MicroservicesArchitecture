namespace IhsanDev.Shared.Kernel.Entities.Identity;

public abstract class BaseUser : BaseEntity
{    
    public required string FirstName { get; set; }
    
    public required string LastName { get; set; }
    
    public string? RefreshToken { get; set; }
    
    public DateTime? RefreshTokenExpiryTime { get; set; }
    
    public DateTime? LastLogin { get; set; }
}