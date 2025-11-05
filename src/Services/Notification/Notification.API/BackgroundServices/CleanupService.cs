using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure.Persistence;
using Notification.Domain.Enums;

namespace Notification.API.BackgroundServices;

/// <summary>
/// Background service to cleanup expired notifications
/// Runs periodically to remove old queue items and tenant notifications
/// </summary>
public class CleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanupService> _logger;
    private readonly TimeSpan _cleanupInterval;

    public CleanupService(
        IServiceProvider serviceProvider,
        ILogger<CleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Get cleanup interval from configuration (default 1 hour)
        using var scope = _serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var intervalHours = configuration.GetValue<int>("NotificationProcessing:CleanupIntervalHours", 1);
        _cleanupInterval = TimeSpan.FromHours(intervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cleanup Service started");

        // Wait a bit before first cleanup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredItemsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Cleanup Service stopped");
    }

    private async Task CleanupExpiredItemsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Get retention settings
        var retentionDays = configuration.GetValue<int>("NotificationProcessing:ExpiredNotificationRetentionDays", 7);
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation("Starting cleanup of notifications older than {CutoffDate}", cutoffDate);

        // Mark expired items
        var expiredItems = await dbContext.NotificationQueue
            .Where(q => q.ExpiresAt < DateTime.UtcNow && q.QueueStatus == QueueStatus.Pending)
            .ToListAsync(cancellationToken);

        if (expiredItems.Any())
        {
            foreach (var item in expiredItems)
            {
                item.QueueStatus = QueueStatus.Expired;
                item.LastModified = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Marked {Count} items as expired", expiredItems.Count);
        }

        // Delete old processed items (Sent, Failed, Expired)
        var oldItems = await dbContext.NotificationQueue
            .Where(q => q.LastModified < cutoffDate && 
                       (q.QueueStatus == QueueStatus.Sent || 
                        q.QueueStatus == QueueStatus.Failed || 
                        q.QueueStatus == QueueStatus.Expired))
            .ToListAsync(cancellationToken);

        if (oldItems.Any())
        {
            dbContext.NotificationQueue.RemoveRange(oldItems);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted {Count} old queue items", oldItems.Count);
        }

        // Note: Tenant-specific notification cleanup requires tenant iteration mechanism
    }
}
