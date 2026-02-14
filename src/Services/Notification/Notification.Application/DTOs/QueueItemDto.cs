using Notification.Domain.Entities;
using Notification.Domain.Enums;

using IhsanDev.Shared.Kernel.Dto.Identity;

namespace Notification.Application.DTOs;

/// <summary>
/// DTO for notification queue item details (SuperAdmin view)
/// </summary>
public class QueueItemDto : BaseDto
{
    public string? TenantId { get; set; }
    public int? UserId { get; set; }
    public DeliveryType DeliveryType { get; set; }
    public Priority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Data { get; set; }
    public QueueStatus QueueStatus { get; set; }
    public int RetryCount { get; set; }
    public string? ProcessedAt { get; set; }
    public string ExpiresAt { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int? NotificationId { get; set; }

    /// <summary>
    /// Maps NotificationQueueItem entity to QueueItemDto
    /// </summary>
    public static QueueItemDto MapFrom(NotificationQueueItem queueItem)
    {
        return new QueueItemDto
        {
            Id = queueItem.Id,
            TenantId = queueItem.TenantId,
            UserId = queueItem.UserId,
            DeliveryType = queueItem.DeliveryType,
            Priority = queueItem.Priority,
            Title = queueItem.Title,
            Message = queueItem.Message,
            Data = queueItem.Data,
            QueueStatus = queueItem.QueueStatus,
            RetryCount = queueItem.RetryCount,
            ProcessedAt = queueItem.ProcessedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            ExpiresAt = queueItem.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            Error = queueItem.Error,
            NotificationId = queueItem.NotificationId,
            Created = queueItem.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            LastModified = queueItem.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            IsArchived = queueItem.IsArchived
        };
    }
}
