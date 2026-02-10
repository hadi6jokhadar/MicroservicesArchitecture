using IhsanDev.Shared.Kernel.Dto.Tenant;
using System.Text.Json;
using Tenant.Domain.Entities;

namespace Tenant.Application.DTOs;

/// <summary>
/// Tenant configuration data transfer object (includes sensitive data field)
/// </summary>
public class TenantConfigDto
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string ExpireDate { get; set; } = string.Empty;
    public TenantConfiguration? Data { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public string Created { get; set; } = string.Empty;
    public string? LastModified { get; set; }

    /// <summary>
    /// Maps TenantSettings entity to TenantConfigDto
    /// </summary>
    public static TenantConfigDto MapFrom(TenantSettings tenant)
    {
        return new TenantConfigDto
        {
            Id = tenant.Id,
            TenantId = tenant.TenantId,
            TenantName = tenant.TenantName,
            UserId = tenant.UserId,
            StartDate = tenant.StartDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            ExpireDate = tenant.ExpireDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            IsActive = tenant.IsActive,
            IsExpired = tenant.IsExpired,
            Created = tenant.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            LastModified = tenant.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            Data = DeserializeData(tenant.Data)
        };
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
public class TenantDto
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string ExpireDate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public string Created { get; set; } = string.Empty;
    public string? LastModified { get; set; }

    /// <summary>
    /// Maps TenantSettings entity to TenantDto
    /// </summary>
    public static TenantDto MapFrom(TenantSettings tenant)
    {
        return new TenantDto
        {
            Id = tenant.Id,
            TenantId = tenant.TenantId,
            TenantName = tenant.TenantName,
            UserId = tenant.UserId,
            StartDate = tenant.StartDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            ExpireDate = tenant.ExpireDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            IsActive = tenant.IsActive,
            IsExpired = tenant.IsExpired,
            Created = tenant.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            LastModified = tenant.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
