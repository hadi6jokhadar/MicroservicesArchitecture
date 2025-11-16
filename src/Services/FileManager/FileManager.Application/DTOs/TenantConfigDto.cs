using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace FileManager.Application.DTOs;

/// <summary>
/// DTO for tenant configuration response from Tenant service
/// </summary>
public class TenantConfigDto
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public TenantConfiguration? Data { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
}
