using Backup.Application.Interfaces;
using Backup.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Jobs;

/// <summary>
/// Daily recurring job (registered via <c>HangfireExtensions.RegisterBackupRecurringJobs</c>):
/// syncs tenant targets, then enqueues a scheduled backup run for every enabled target.
/// </summary>
public class BackupSchedulerJob
{
    private readonly BackupDbContext _context;
    private readonly TenantTargetSyncJob _tenantTargetSyncJob;
    private readonly GlobalTargetSyncJob _globalTargetSyncJob;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<BackupSchedulerJob> _logger;

    public BackupSchedulerJob(
        BackupDbContext context,
        TenantTargetSyncJob tenantTargetSyncJob,
        GlobalTargetSyncJob globalTargetSyncJob,
        IBackgroundJobClient backgroundJobClient,
        ILogger<BackupSchedulerJob> logger)
    {
        _context = context;
        _tenantTargetSyncJob = tenantTargetSyncJob;
        _globalTargetSyncJob = globalTargetSyncJob;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await _globalTargetSyncJob.SyncAsync(ct);
        await _tenantTargetSyncJob.SyncAsync(ct);

        var enabledTargets = await _context.BackupTargets
            .Where(t => t.IsEnabled)
            .ToListAsync(ct);

        foreach (var target in enabledTargets)
        {
            _backgroundJobClient.Enqueue<IRunBackupJob>(job => job.ExecuteForTargetAsync(target.Id, CancellationToken.None));
        }

        _logger.LogInformation("BackupSchedulerJob: enqueued {Count} scheduled backup run(s)", enabledTargets.Count);
    }
}
