using System.Globalization;
using Identity.Domain.Entities;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Dto.Identity;

namespace Identity.Application.DTOs;

public class UserDto : BaseUserDto
{
    public List<RoleDto> Roles { get; set; } = [];
    
    // Navigation properties for other microservices
    public int? ProfilePictureId { get; set; }
    public FileManagerDto? ProfilePicture { get; set; }
    
    // OTP verification
    public string? VerificationCode { get; set; }
    
    // Additional user data
    public string? Data { get; set; }
    
    /// <summary>
    /// Maps User entity to UserDto
    /// </summary>
    /// <param name="user">User entity to map from</param>
    /// <param name="includeRoles">Whether to include roles (only for SuperAdmin/Admin requests)</param>
    public static UserDto MapFrom(User user, bool includeRoles = false)
    {
        return new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status,
            Created = user.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            LastModified = user.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            Roles = includeRoles ? (user.UserRoles?.Where(ur => ur.Role != null).Select(ur => new RoleDto
            {
                Id = ur.Role.Id,
                Name = ur.Role.Name,
                Description = ur.Role.Description,
                IsSystemRole = ur.Role.IsSystemRole,
                Status = ur.Role.Status,
                Claims = ur.Role.RoleClaims?.Where(rc => rc.Claim != null).Select(rc => new ClaimDto
                {
                    Id = rc.Claim.Id,
                    Name = rc.Claim.Name,
                    Description = rc.Claim.Description,
                    ClaimType = rc.Claim.ClaimType,
                    ClaimValue = rc.Claim.ClaimValue,
                    IsSuperAdminOnly = rc.Claim.IsSuperAdminOnly,
                    Status = rc.Claim.Status
                }).ToList() ?? []
            }).ToList() ?? []) : [],
            ProfilePictureId = user.ProfilePictureId,
            ProfilePicture = null, // Will be populated by handler when requested
            VerificationCode = user.VerificationCode,
            Data = user.Data
        };
    }
}