using IhsanDev.Shared.Kernel.Dto.Identity;
using Notification.Domain.Entities;

namespace Notification.Application.DTOs;

/// <summary>
/// Response DTO for notification details
/// </summary>
public class NotificationResponse : BaseDto
{
    /// <summary>
    /// Notification title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Notification message body
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Additional JSON payload data
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// Whether notification has been read
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Timestamp when notification was read
    /// </summary>
    public string? ReadAt { get; set; }

    /// <summary>
    /// User ID (null for broadcast notifications)
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Maps Notification entity to NotificationResponse
    /// </summary>
    public static NotificationResponse MapFrom(Notification.Domain.Entities.Notification notification)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Data = notification.Data,
            IsRead = notification.IsRead,
            Created = notification.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            ReadAt = notification.ReadAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            UserId = notification.UserId,
            Status = notification.Status,
            IsArchived = notification.IsArchived
        };
    }
}
