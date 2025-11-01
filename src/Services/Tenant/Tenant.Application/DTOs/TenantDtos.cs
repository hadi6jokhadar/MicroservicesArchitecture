using AutoMapper;
using IhsanDev.Shared.Application.Common.Mappings;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using System.Text.Json;
using Tenant.Domain.Entities;

namespace Tenant.Application.DTOs;

/// <summary>
/// Tenant configuration data transfer object (includes sensitive data field)
/// </summary>
public class TenantConfigDto : IMapFrom<TenantSettings>
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public TenantConfiguration? Data { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }

    public void Mapping(Profile profile)
    {
        profile.CreateMap<TenantSettings, TenantConfigDto>()
            .ForMember(dest => dest.IsExpired, opt => opt.MapFrom(src => src.IsExpired))
            .ForMember(dest => dest.Data, opt => opt.MapFrom(src => DeserializeData(src.Data)));
    }

    private static TenantConfiguration? DeserializeData(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Deserialize<TenantConfiguration>(data, options);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Tenant response DTO (excludes sensitive data field)
/// </summary>
public class TenantDto : IMapFrom<TenantSettings>
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }

    public void Mapping(Profile profile)
    {
        profile.CreateMap<TenantSettings, TenantDto>()
            .ForMember(dest => dest.IsExpired, opt => opt.MapFrom(src => src.IsExpired));
    }
}
