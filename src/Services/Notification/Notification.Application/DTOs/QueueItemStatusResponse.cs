using Notification.Domain.Entities;

namespace Notification.Application.DTOs;

/// <summary>
/// Response DTO for notification queue item status
/// </summary>
public class QueueItemStatusResponse
{
    /// <summary>
    /// Queue item unique identifier
    /// </summary>
    public int QueueItemId { get; set; }

    /// <summary>
    /// Current processing status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of delivery attempts
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Timestamp when processed
    /// </summary>
    public string? ProcessedAt { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Tenant notification ID (if persisted)
    /// </summary>
    public int? NotificationId { get; set; }

    /// <summary>
    /// Timestamp when created
    /// </summary>
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>
    /// Maps NotificationQueueItem entity to QueueItemStatusResponse
    /// </summary>
    public static QueueItemStatusResponse MapFrom(NotificationQueueItem queueItem)
    {
        return new QueueItemStatusResponse
        {
            QueueItemId = queueItem.Id,
            Status = queueItem.QueueStatus.ToString(),
            RetryCount = queueItem.RetryCount,
            ProcessedAt = queueItem.ProcessedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            Error = queueItem.Error,
            NotificationId = queueItem.NotificationId,
            CreatedAt = queueItem.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
