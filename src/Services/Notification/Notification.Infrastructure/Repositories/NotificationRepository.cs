using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Infrastructure.Persistence;
using Notification.Domain.Repositories;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Notification operations
/// Uses TenantNotificationDbContext (Tenant-specific DB)
/// </summary>
public class NotificationRepository : Repository<Domain.Entities.Notification>, INotificationRepository
{
    public NotificationRepository(TenantNotificationDbContext context) : base(context)
    {
    }

    // Note: AddAsync, GetByIdAsync, UpdateAsync, DeleteAsync inherited from Repository<T>

    public async Task<IEnumerable<Domain.Entities.Notification>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Created)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Notification>> GetUnreadByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.Created)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Notification>> GetBroadcastNotificationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(n => n.UserId == null)
            .OrderByDescending(n => n.Created)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _dbSet.FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (notification == null) return false;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        notification.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await _dbSet
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.LastModified = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return unreadNotifications.Count;
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task<Domain.Entities.Notification?> GetByQueueItemIdAsync(int queueItemId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.QueueItemId == queueItemId, cancellationToken);
    }
}
