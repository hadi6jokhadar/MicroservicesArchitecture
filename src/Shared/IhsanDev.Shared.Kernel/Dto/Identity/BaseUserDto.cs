using System.Text.Json.Serialization;

namespace IhsanDev.Shared.Kernel.Dto.Identity;

public abstract class BaseUserDto : BaseDto
{
    public string? Email { get; set; }
    
    [JsonIgnore]
    public string? PasswordHash { get; set; }
    
    public string? FirstName { get; set; }
    
    public string? LastName { get; set; }
    
    public string? PhoneNumber { get; set; }
    
    public bool EmailConfirmed { get; set; } = false;
    
    public DateTime? LastLogin { get; set; }
}