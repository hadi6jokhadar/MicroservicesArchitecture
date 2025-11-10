using System.ComponentModel.DataAnnotations;
using IhsanDev.Shared.Kernel.Entities;
using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

/// <summary>
/// Global queue item for managing notification delivery workflow
/// Stored in Global DB (NotificationQueue database)
/// </summary>
public class NotificationQueueItem : BaseEntity
{

    /// <summary>
    /// Tenant identifier (null for non-tenant notifications)
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// User ID (null = broadcast to all users in tenant)
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Delivery channel configuration
    /// </summary>
    public DeliveryType DeliveryType { get; set; } = DeliveryType.Both;

    /// <summary>
    /// Processing priority
    /// </summary>
    public Priority Priority { get; set; } = Priority.Immediate;

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
    /// Current processing status
    /// </summary>
    public QueueStatus QueueStatus { get; set; } = QueueStatus.Pending;

    /// <summary>
    /// Number of delivery attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Next retry timestamp (for exponential backoff)
    /// Null = ready for immediate processing
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Timestamp when notification was processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Expiration time for notification
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Error message if delivery failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Reference to persisted notification in tenant DB
    /// </summary>
    public int? NotificationId { get; set; }
}
