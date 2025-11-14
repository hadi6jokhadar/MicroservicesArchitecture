using IhsanDev.Shared.Kernel.Enums;

namespace Notification.Application.DTOs;

/// <summary>
/// DTO for device token from Identity Service
/// </summary>
public class DeviceTokenDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public Platform Platform { get; set; }
    public string? DeviceIdentifier { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime Created { get; set; }
}
