using IhsanDev.Shared.Infrastructure.Persistence;
using Notification.Domain.Entities;

namespace Notification.Domain.Repositories;

/// <summary>
/// Repository interface for Notification operations
/// Manages tenant-specific notifications in TenantNotificationDbContext
/// </summary>
public interface INotificationRepository : IRepository<Entities.Notification>
{
    // Note: GetByIdAsync, AddAsync, UpdateAsync, DeleteAsync inherited from IRepository<T>

    /// <summary>
    /// Get notifications for a specific user
    /// </summary>
    Task<IEnumerable<Entities.Notification>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get unread notifications for a user
    /// </summary>
    Task<IEnumerable<Entities.Notification>> GetUnreadByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get notifications for all users in tenant (broadcast)
    /// </summary>
    Task<IEnumerable<Entities.Notification>> GetBroadcastNotificationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    Task<bool> MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark all notifications as read for a user
    /// </summary>
    Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get notification count for a user
    /// </summary>
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get notifications by queue item reference
    /// </summary>
    Task<Entities.Notification?> GetByQueueItemIdAsync(int queueItemId, CancellationToken cancellationToken = default);
}
