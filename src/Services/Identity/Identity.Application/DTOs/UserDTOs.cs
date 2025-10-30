using System.Globalization;
using AutoMapper;
using Identity.Domain.Entities;
using IhsanDev.Shared.Application.Common.Mappings;
using IhsanDev.Shared.Kernel.Dto.Identity;
using IhsanDev.Shared.Kernel.Enums.Identity;

namespace Identity.Application.DTOs;

public class UserDto : BaseUserDto, IMapFrom<User>
{
    public UserRole Role { get; set; } = UserRole.User;
    public string? RoleName { get; set; }
    
    // Navigation properties for other microservices
    public string? ProfilePictureUrl { get; set; }
    
    // OTP verification
    public string? VerificationCode { get; set; }
    
    public void Mapping(Profile profile)
    {
        profile.CreateMap<User, UserDto>()
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.ToString()));
    }
}