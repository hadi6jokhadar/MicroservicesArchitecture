namespace Notification.Application.DTOs;

/// <summary>
/// Response DTO for notification details
/// </summary>
public class NotificationResponse
{
    /// <summary>
    /// Notification unique identifier
    /// </summary>
    public int Id { get; set; }

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
    /// Timestamp when notification was created
    /// </summary>
    public string CreatedAt { get; set; } = string.Empty;

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
    public static NotificationResponse MapFrom(Domain.Entities.Notification notification)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Data = notification.Data,
            IsRead = notification.IsRead,
            CreatedAt = notification.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            ReadAt = notification.ReadAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            UserId = notification.UserId
        };
    }
}
