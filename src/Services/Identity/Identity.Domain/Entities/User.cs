using IhsanDev.Shared.Kernel.Entities.Identity;
using IhsanDev.Shared.Kernel.Enums.Identity;

namespace Identity.Domain.Entities;

public class User : BaseUser
{    
    // Navigation properties for other microservices
    public string? ProfilePictureUrl { get; set; }

    public string? Email { get; set; }
    
    public string? PhoneNumber { get; set; }
    
    public bool EmailConfirmed { get; set; } = false;
    
    public string? PasswordHash { get; set; }
    
    // OTP/Verification Code fields
    public string? VerificationCode { get; set; }
    public DateTime? VerificationCodeExpiry { get; set; }
    public int FailedCodeAttempts { get; set; } = 0;
    public DateTime? CodeLockoutUntil { get; set; }
    public DateTime? LastCodeSentAt { get; set; }
}