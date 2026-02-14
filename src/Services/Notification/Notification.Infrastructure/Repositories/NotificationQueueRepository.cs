using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Infrastructure.Persistence;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Domain.Repositories;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for NotificationQueueItem operations
/// Uses NotificationDbContext (Global DB)
/// </summary>
public class NotificationQueueRepository : Repository<NotificationQueueItem>, INotificationQueueRepository
{
    public NotificationQueueRepository(NotificationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<NotificationQueueItem>> GetPendingItemsAsync(int maxCount = 100, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(q => q.QueueStatus == QueueStatus.Pending && q.ExpiresAt > DateTime.UtcNow)
            .OrderBy(q => q.Priority)
            .ThenBy(q => q.Created)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<NotificationQueueItem>> GetByStatusAsync(QueueStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(q => q.QueueStatus == status)
            .OrderByDescending(q => q.Created)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<NotificationQueueItem>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.Created)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<NotificationQueueItem>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId)
            .OrderByDescending(q => q.Created)
            .ToListAsync(cancellationToken);
    }

    // Note: UpdateAsync inherited from Repository<T>

    public async Task<bool> UpdateStatusAsync(int queueItemId, QueueStatus status, string? error = null, CancellationToken cancellationToken = default)
    {
        var queueItem = await _dbSet.FirstOrDefaultAsync(q => q.Id == queueItemId, cancellationToken);
        if (queueItem == null) return false;

        queueItem.QueueStatus = status;
        queueItem.Error = error;
        queueItem.LastModified = DateTime.UtcNow;

        if (status == QueueStatus.Sent || status == QueueStatus.Failed)
        {
            queueItem.ProcessedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkAsProcessedAsync(int queueItemId, int? notificationId = null, CancellationToken cancellationToken = default)
    {
        var queueItem = await _dbSet.FirstOrDefaultAsync(q => q.Id == queueItemId, cancellationToken);
        if (queueItem == null) return false;

        queueItem.QueueStatus = QueueStatus.Sent;
        queueItem.ProcessedAt = DateTime.UtcNow;
        queueItem.NotificationId = notificationId;
        queueItem.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> IncrementRetryCountAsync(int queueItemId, string? error = null, CancellationToken cancellationToken = default)
    {
        var queueItem = await _dbSet.FirstOrDefaultAsync(q => q.Id == queueItemId, cancellationToken);
        if (queueItem == null) return false;

        queueItem.RetryCount++;
        queueItem.Error = error;
        queueItem.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteExpiredItemsAsync(CancellationToken cancellationToken = default)
    {
        var expiredItems = await _dbSet
            .Where(q => q.ExpiresAt <= DateTime.UtcNow && 
                       (q.QueueStatus == QueueStatus.Sent || q.QueueStatus == QueueStatus.Failed))
            .ToListAsync(cancellationToken);

        _dbSet.RemoveRange(expiredItems);
        await _context.SaveChangesAsync(cancellationToken);

        return expiredItems.Count;
    }

    public async Task<IEnumerable<NotificationQueueItem>> GetExpiredItemsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(q => q.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public IQueryable<NotificationQueueItem> GetFilteredQueryable(
        string? tenantId = null,
        int? userId = null,
        QueueStatus? status = null,
        Priority? priority = null,
        DeliveryType? deliveryType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchTerm = null,
        bool isArchived = false)
    {
        var query = _dbSet.AsNoTracking().AsQueryable();

        // Apply filters
        query = query.Where(q => q.IsArchived == isArchived);

        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(q => q.TenantId == tenantId);

        if (userId.HasValue)
            query = query.Where(q => q.UserId == userId.Value);

        if (status.HasValue)
            query = query.Where(q => q.QueueStatus == status.Value);

        if (priority.HasValue)
            query = query.Where(q => q.Priority == priority.Value);

        if (deliveryType.HasValue)
            query = query.Where(q => q.DeliveryType == deliveryType.Value);

        if (fromDate.HasValue)
            query = query.Where(q => q.Created >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(q => q.Created <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.ToLower();
            query = query.Where(q => 
                q.Title.ToLower().Contains(search) || 
                (q.Message != null && q.Message.ToLower().Contains(search)));
        }

        // Default ordering: most recent first
        query = query.OrderByDescending(q => q.Created);

        return query;
    }
}
