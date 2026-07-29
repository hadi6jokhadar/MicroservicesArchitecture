using Backup.Application.Interfaces;
using Backup.Domain.Enums;
using Backup.Infrastructure.Persistence;
using Backup.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Jobs;

/// <summary>Hangfire job that performs a single restore run.</summary>
public class RunRestoreJob : IRunRestoreJob
{
    private readonly BackupDbContext _context;
    private readonly IPgToolRunner _pgToolRunner;
    private readonly IBackupBlobStorage _blobStorage;
    private readonly BackupConnectionResolver _connectionResolver;
    private readonly ILogger<RunRestoreJob> _logger;

    public RunRestoreJob(
        BackupDbContext context,
        IPgToolRunner pgToolRunner,
        IBackupBlobStorage blobStorage,
        BackupConnectionResolver connectionResolver,
        ILogger<RunRestoreJob> logger)
    {
        _context = context;
        _pgToolRunner = pgToolRunner;
        _blobStorage = blobStorage;
        _connectionResolver = connectionResolver;
        _logger = logger;
    }

    public async Task ExecuteAsync(int restoreRunId, CancellationToken ct)
    {
        var restoreRun = await _context.RestoreRuns.FirstOrDefaultAsync(r => r.Id == restoreRunId, ct);
        if (restoreRun == null)
        {
            _logger.LogWarning("RunRestoreJob: restore run {RestoreRunId} not found", restoreRunId);
            return;
        }

        var backupRun = await _context.BackupRuns.FirstOrDefaultAsync(r => r.Id == restoreRun.BackupRunId, ct);
        if (backupRun == null)
        {
            restoreRun.Status = BackupRunStatus.Failed;
            restoreRun.ErrorMessage = $"Referenced backup run {restoreRun.BackupRunId} no longer exists.";
            restoreRun.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return;
        }

        restoreRun.Status = BackupRunStatus.Running;
        restoreRun.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        string? tempPath = null;

        try
        {
            var localPath = backupRun.LocalFilePath;
            var needsDownload = string.IsNullOrWhiteSpace(localPath)
                || backupRun.LocalStatus != LocalBackupStatus.Saved
                || !File.Exists(localPath);

            if (needsDownload)
            {
                if (string.IsNullOrWhiteSpace(backupRun.CloudStorageKey) || backupRun.CloudStatus != CloudBackupStatus.Uploaded)
                {
                    throw new InvalidOperationException(
                        "Backup file is not available locally and no cloud copy was uploaded for this run.");
                }

                tempPath = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid():N}.dump");
                await _blobStorage.DownloadAsync(backupRun.CloudStorageKey, tempPath, ct);
                localPath = tempPath;
            }

            var connectionString = !string.IsNullOrWhiteSpace(restoreRun.TargetConnectionOverride)
                ? restoreRun.TargetConnectionOverride
                : await _connectionResolver.ResolveAsync(backupRun.Scope, backupRun.ServiceName, backupRun.TenantId, ct);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("No target connection string could be resolved for this restore.");
            }

            await _pgToolRunner.RestoreAsync(connectionString, localPath!, ct);

            restoreRun.Status = BackupRunStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunRestoreJob: restore failed for restore run {RestoreRunId}", restoreRun.Id);
            restoreRun.Status = BackupRunStatus.Failed;
            restoreRun.ErrorMessage = ex.Message;
        }
        finally
        {
            restoreRun.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            if (tempPath != null && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RunRestoreJob: failed to delete temp restore file {Path}", tempPath);
                }
            }
        }
    }
}
