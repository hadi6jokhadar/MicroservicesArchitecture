using System.Globalization;
using AutoMapper;
using Identity.Domain.Entities;
using IhsanDev.Shared.Application.Common.Mappings;
using IhsanDev.Shared.Kernel.Dto.Identity;
using IhsanDev.Shared.Kernel.Enums.Identity;

namespace Identity.Application.DTOs;

public class UserDtoIncludesToken : BaseUserDto, IMapFrom<User>
{
    public UserRole Role { get; set; } = UserRole.User;
    public string? RoleName { get; set; }    
    public string? ProfilePictureUrl { get; set; }

    // ^ Token properties
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }    
    public DateTime? RefreshTokenExpiryTime { get; set; }    
    public string? FirebaseToken { get; set; }
    public void Mapping(Profile profile)
    {
        profile.CreateMap<User, UserDtoIncludesToken>()
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.ToString()))
            .ForMember(dust => dust.Created, opt => opt.MapFrom(src => src.Created.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)))
            .ForMember(dust => dust.LastModified, opt => opt.MapFrom(src => src.LastModified != null ? ((DateTime)src.LastModified).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : null));
    }
}