using IhsanDev.Shared.Infrastructure.Persistence;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Domain.Repositories;

/// <summary>
/// Repository interface for NotificationQueueItem operations
/// Manages global notification queue in NotificationDbContext
/// </summary>
public interface INotificationQueueRepository : IRepository<NotificationQueueItem>
{
    // Note: GetByIdAsync, AddAsync, UpdateAsync, DeleteAsync inherited from IRepository<T>

    /// <summary>
    /// Get pending queue items for processing
    /// </summary>
    Task<IEnumerable<NotificationQueueItem>> GetPendingItemsAsync(int maxCount = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue items by status
    /// </summary>
    Task<IEnumerable<NotificationQueueItem>> GetByStatusAsync(QueueStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue items for a specific user
    /// </summary>
    Task<IEnumerable<NotificationQueueItem>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue items for a specific tenant
    /// </summary>
    Task<IEnumerable<NotificationQueueItem>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update queue item status
    /// </summary>
    Task<bool> UpdateStatusAsync(int queueItemId, QueueStatus status, string? error = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark queue item as processed
    /// </summary>
    Task<bool> MarkAsProcessedAsync(int queueItemId, int? notificationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment retry count for a queue item
    /// </summary>
    Task<bool> IncrementRetryCountAsync(int queueItemId, string? error = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete expired queue items
    /// </summary>
    Task<int> DeleteExpiredItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get expired queue items
    /// </summary>
    Task<IEnumerable<NotificationQueueItem>> GetExpiredItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queryable for filtered queue items with pagination support
    /// </summary>
    IQueryable<NotificationQueueItem> GetFilteredQueryable(
        string? tenantId = null,
        int? userId = null,
        QueueStatus? status = null,
        Priority? priority = null,
        DeliveryType? deliveryType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchTerm = null);
}
