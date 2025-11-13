using IhsanDev.Shared.Kernel.Enums;
using System.ComponentModel.DataAnnotations;

namespace IhsanDev.Shared.Kernel.Entities;

/// <summary>
/// Represents a device token for push notifications (Firebase, APNs, etc.)
/// </summary>
public class DeviceToken : BaseEntity
{
    /// <summary>
    /// Foreign key to the user who owns this device token
    /// </summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// The device token string (Firebase token, APNs token, etc.)
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// The platform this token belongs to (iOS, Android, Web)
    /// </summary>
    [Required]
    public Platform Platform { get; set; }

    /// <summary>
    /// Optional device identifier for tracking multiple devices per user
    /// </summary>
    [StringLength(100)]
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Last time this token was verified as valid
    /// </summary>
    public DateTime? LastVerifiedAt { get; set; }

    /// <summary>
    /// Indicates if this token is the primary device for the user
    /// </summary>
    public bool IsPrimary { get; set; } = false;
}
