using System.Globalization;
using Identity.Domain.Entities;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Dto.Identity;
using IhsanDev.Shared.Kernel.Enums.Identity;

namespace Identity.Application.DTOs;

public class UserDtoIncludesToken : BaseUserDto
{
    public UserRole Role { get; set; } = UserRole.User;
    public string? RoleName { get; set; }    
    public int? ProfilePictureId { get; set; }
    public FileManagerDto? ProfilePicture { get; set; }

    // ^ Token properties
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }    
    public string? RefreshTokenExpiryTime { get; set; }
    
    // OTP verification
    public string? VerificationCode { get; set; }
    
    // Additional user data
    public string? Data { get; set; }
    
    /// <summary>
    /// Maps User entity to UserDtoIncludesToken
    /// </summary>
    public static UserDtoIncludesToken MapFrom(User user)
    {
        return new UserDtoIncludesToken
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status,
            Created = user.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            LastModified = user.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            Role = user.Role,
            RoleName = user.Role.ToString(),
            ProfilePictureId = user.ProfilePictureId,
            ProfilePicture = null, // Will be populated by handler when requested
            VerificationCode = user.VerificationCode,
            Data = user.Data
        };
    }
}