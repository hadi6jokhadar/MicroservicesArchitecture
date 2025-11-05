using Notification.Application.Commands;
using Notification.Application.DTOs;

namespace Notification.Application.Services;

/// <summary>
/// Service interface for notification operations
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send a notification by adding it to the queue
    /// </summary>
    Task<SendNotificationResponse> SendNotificationAsync(SendNotificationCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    Task<bool> MarkAsReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge notification delivery
    /// </summary>
    Task<bool> AcknowledgeDeliveryAsync(int queueItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue item status
    /// </summary>
    Task<QueueItemStatusResponse?> GetQueueItemStatusAsync(int queueItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user notifications
    /// </summary>
    Task<List<NotificationResponse>> GetUserNotificationsAsync(
        int userId, 
        bool? unreadOnly = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);
}
