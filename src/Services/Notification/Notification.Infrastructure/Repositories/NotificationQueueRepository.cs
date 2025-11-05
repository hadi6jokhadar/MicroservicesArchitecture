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
}
