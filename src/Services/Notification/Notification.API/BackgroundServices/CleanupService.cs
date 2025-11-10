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
        var batchSize = 1000; // Process in batches to avoid locking

        _logger.LogInformation("Starting cleanup of notifications older than {CutoffDate}", cutoffDate);

        // Step 1: Mark expired items (using optimized index)
        var expiredCount = await dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE ""NotificationQueue"" 
              SET ""QueueStatus"" = 4, ""LastModified"" = @p0
              WHERE ""ExpiresAt"" < @p1 AND ""QueueStatus"" = 0",
            DateTime.UtcNow, DateTime.UtcNow);

        if (expiredCount > 0)
        {
            _logger.LogInformation("Marked {Count} items as expired", expiredCount);
        }

        // Step 2: Delete old processed items in batches (uses IX_NotificationQueue_Cleanup index)
        var totalDeleted = 0;
        int deletedInBatch;

        do
        {
            deletedInBatch = await dbContext.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""NotificationQueue""
                  WHERE ""Id"" IN (
                      SELECT ""Id"" 
                      FROM ""NotificationQueue""
                      WHERE ""LastModified"" < @p0 
                        AND ""QueueStatus"" IN (2, 3, 4)
                      LIMIT @p1
                  )",
                cutoffDate, batchSize);

            totalDeleted += deletedInBatch;

            _logger.LogDebug("Deleted {Count} items in this batch", deletedInBatch);

            // Small delay between batches to avoid overwhelming the database
            if (deletedInBatch == batchSize)
            {
                await Task.Delay(100, cancellationToken);
            }

        } while (deletedInBatch == batchSize && !cancellationToken.IsCancellationRequested);

        if (totalDeleted > 0)
        {
            _logger.LogInformation("Deleted {Count} old queue items (Sent/Failed/Expired)", totalDeleted);
        }

        // Step 3: Log statistics
        var stats = await dbContext.NotificationQueue
            .GroupBy(q => q.QueueStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Cleanup complete. Queue stats: {Stats}",
            string.Join(", ", stats.Select(s => $"{s.Status}={s.Count}")));

        // Note: Tenant-specific notification cleanup requires tenant iteration mechanism
    }
}
