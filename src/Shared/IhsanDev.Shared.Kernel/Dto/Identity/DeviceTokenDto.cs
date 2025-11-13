using IhsanDev.Shared.Kernel.Enums;

namespace IhsanDev.Shared.Kernel.Dto;

/// <summary>
/// Data Transfer Object for device token information
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

/// <summary>
/// DTO for creating a new device token
/// </summary>
public class CreateDeviceTokenDto
{
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public Platform Platform { get; set; }
    public string? DeviceIdentifier { get; set; }
    public bool IsPrimary { get; set; } = false;
}

/// <summary>
/// DTO for updating a device token
/// </summary>
public class UpdateDeviceTokenDto
{
    public string? Token { get; set; }
    public string? DeviceIdentifier { get; set; }
    public bool? IsPrimary { get; set; }
}
