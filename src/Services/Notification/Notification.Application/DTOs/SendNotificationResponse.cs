using Notification.Domain.Entities;

namespace Notification.Application.DTOs;

/// <summary>
/// Response DTO after sending notification
/// </summary>
public class SendNotificationResponse
{
    /// <summary>
    /// Queue item unique identifier
    /// </summary>
    public int QueueItemId { get; set; }

    /// <summary>
    /// Current status of the notification
    /// </summary>
    public string Status { get; set; } = "Queued";

    /// <summary>
    /// Timestamp when notification was queued
    /// </summary>
    public string QueuedAt { get; set; } = string.Empty;

    /// <summary>
    /// Priority level
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Delivery channels configured
    /// </summary>
    public string? DeliveryType { get; set; }

    /// <summary>
    /// Maps NotificationQueueItem entity to SendNotificationResponse
    /// </summary>
    public static SendNotificationResponse MapFrom(NotificationQueueItem queueItem)
    {
        return new SendNotificationResponse
        {
            QueueItemId = queueItem.Id,
            Status = queueItem.QueueStatus.ToString(),
            QueuedAt = queueItem.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            Priority = queueItem.Priority.ToString(),
            DeliveryType = queueItem.DeliveryType.ToString()
        };
    }
}
