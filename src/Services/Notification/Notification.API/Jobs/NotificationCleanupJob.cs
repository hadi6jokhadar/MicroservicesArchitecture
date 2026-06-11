using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notification.Infrastructure.Persistence;

namespace Notification.API.Jobs;

/// <summary>
/// Hangfire recurring job that replaces the polling loop in <c>CleanupService</c>.
/// Scheduled hourly via Hangfire cron. Marks expired queue items and batch-deletes
/// old processed/failed entries to keep the queue table lean.
/// </summary>
public class NotificationCleanupJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationCleanupJob> _logger;

    public NotificationCleanupJob(
        IServiceProvider serviceProvider,
        ILogger<NotificationCleanupJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext       = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var configuration   = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var retentionDays = configuration.GetValue<int>("NotificationProcessing:ExpiredNotificationRetentionDays", 7);
        var cutoffDate    = DateTime.UtcNow.AddDays(-retentionDays);
        const int batchSize = 1000;

        _logger.LogInformation("NotificationCleanupJob: cleaning up items older than {CutoffDate}", cutoffDate);

        var expiredCount = await dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE ""NotificationQueue""
              SET ""QueueStatus"" = 4, ""LastModified"" = @p0
              WHERE ""ExpiresAt"" < @p1 AND ""QueueStatus"" = 0",
            DateTime.UtcNow, DateTime.UtcNow);

        if (expiredCount > 0)
            _logger.LogInformation("Marked {Count} items as expired", expiredCount);

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

            if (deletedInBatch == batchSize)
                await Task.Delay(100, ct);

        } while (deletedInBatch == batchSize && !ct.IsCancellationRequested);

        if (totalDeleted > 0)
            _logger.LogInformation("Deleted {Count} old queue items", totalDeleted);

        var stats = await dbContext.NotificationQueue
            .GroupBy(q => q.QueueStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        _logger.LogInformation(
            "Cleanup complete. Queue stats: {Stats}",
            string.Join(", ", stats.Select(s => $"{s.Status}={s.Count}")));
    }
}
