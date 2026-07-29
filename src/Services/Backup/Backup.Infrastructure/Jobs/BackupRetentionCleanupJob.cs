using Backup.Domain.Enums;
using Backup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Jobs;

/// <summary>
/// Daily recurring job: deletes local backup files once they've been safely uploaded to cloud
/// storage and have aged past their retention window. Never touches rows where
/// <c>CloudStatus != Uploaded</c> — deleting the only surviving copy of a backup is never safe.
/// </summary>
public class BackupRetentionCleanupJob
{
    private readonly BackupDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupRetentionCleanupJob> _logger;

    public BackupRetentionCleanupJob(
        BackupDbContext context,
        IConfiguration configuration,
        ILogger<BackupRetentionCleanupJob> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var defaultRetentionDays = _configuration.GetValue("Backup:DefaultRetentionDays", 30);

        var candidates = await _context.BackupRuns
            .Where(r => r.LocalStatus == LocalBackupStatus.Saved && r.CloudStatus == CloudBackupStatus.Uploaded)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return;
        }

        var targetIds = candidates
            .Where(r => r.BackupTargetId.HasValue)
            .Select(r => r.BackupTargetId!.Value)
            .Distinct()
            .ToList();

        var retentionByTarget = await _context.BackupTargets
            .Where(t => targetIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.RetentionDays, ct);

        var now = DateTime.UtcNow;
        var deletedCount = 0;

        foreach (var run in candidates)
        {
            var retentionDays = defaultRetentionDays;
            if (run.BackupTargetId.HasValue
                && retentionByTarget.TryGetValue(run.BackupTargetId.Value, out var overrideDays)
                && overrideDays.HasValue)
            {
                retentionDays = overrideDays.Value;
            }

            if (run.Created > now.AddDays(-retentionDays))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(run.LocalFilePath) && File.Exists(run.LocalFilePath))
            {
                try
                {
                    File.Delete(run.LocalFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "BackupRetentionCleanupJob: failed to delete backup file {Path} for run {RunId}, leaving LocalStatus unchanged",
                        run.LocalFilePath, run.Id);
                    continue;
                }
            }

            run.LocalStatus = LocalBackupStatus.Deleted;
            deletedCount++;
        }

        if (deletedCount > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("BackupRetentionCleanupJob: deleted {Count} expired backup file(s)", deletedCount);
        }
    }
}
