using System.Globalization;
using Identity.Domain.Entities;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Dto.Identity;

namespace Identity.Application.DTOs;

public class UserDtoIncludesToken : BaseUserDto
{
    public List<RoleDto> Roles { get; set; } = [];
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
    /// Note: Roles and claims are included in the JWT token, not in the response DTO (unless explicitly requested by SuperAdmin/Admin)
    /// </summary>
    /// <param name="user">User entity to map from</param>
    /// <param name="includeRoles">Whether to include roles in response (only for SuperAdmin/Admin requests, roles are always in JWT)</param>
    public static UserDtoIncludesToken MapFrom(User user, bool includeRoles = false)
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
            // Roles are always in JWT, but only included in response body for SuperAdmin/Admin
            Roles = includeRoles ? (user.UserRoles?.Select(ur => new RoleDto
            {
                Id = ur.Role.Id,
                Name = ur.Role.Name,
                Description = ur.Role.Description,
                IsSystemRole = ur.Role.IsSystemRole,
                Status = ur.Role.Status,
                Claims = ur.Role.RoleClaims?.Select(rc => new ClaimDto
                {
                    Id = rc.Claim.Id,
                    Name = rc.Claim.Name,
                    Description = rc.Claim.Description,
                    ClaimType = rc.Claim.ClaimType,
                    ClaimValue = rc.Claim.ClaimValue,
                    IsSuperAdminOnly = rc.Claim.IsSuperAdminOnly,
                    Status = rc.Claim.Status
                }).ToList()
            }).ToList() ?? []) : [],
            ProfilePictureId = user.ProfilePictureId,
            ProfilePicture = null, // Will be populated by handler when requested
            VerificationCode = user.VerificationCode,
            Data = user.Data
        };
    }
}