namespace IhsanDev.Shared.Kernel.Entities.Identity;

public abstract class BaseUser : BaseEntity
{
    public required string Email { get; set; }
    
    public required string PasswordHash { get; set; }
    
    public required string FirstName { get; set; }
    
    public required string LastName { get; set; }
    
    public string? PhoneNumber { get; set; }
    
    public bool EmailConfirmed { get; set; } = false;
    
    public DateTime? LastLogin { get; set; }
}