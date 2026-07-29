using Backup.Application.Interfaces;
using Backup.Domain.Entities;
using Backup.Domain.Enums;
using Backup.Infrastructure.Persistence;
using Backup.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Backup.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that performs a single backup run. Two entry points share one core:
/// <see cref="ExecuteAsync"/> is the manual-trigger path (the <see cref="BackupRunEntity"/>
/// already exists as Pending), <see cref="ExecuteForTargetAsync"/> is the scheduled path
/// (creates its own run for the target first).
/// </summary>
public class RunBackupJob : IRunBackupJob
{
    private readonly BackupDbContext _context;
    private readonly IPgToolRunner _pgToolRunner;
    private readonly IBackupBlobStorage _blobStorage;
    private readonly BackupConnectionResolver _connectionResolver;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RunBackupJob> _logger;

    public RunBackupJob(
        BackupDbContext context,
        IPgToolRunner pgToolRunner,
        IBackupBlobStorage blobStorage,
        BackupConnectionResolver connectionResolver,
        IConfiguration configuration,
        ILogger<RunBackupJob> logger)
    {
        _context = context;
        _pgToolRunner = pgToolRunner;
        _blobStorage = blobStorage;
        _connectionResolver = connectionResolver;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(int backupRunId, CancellationToken ct)
    {
        var run = await _context.BackupRuns.FirstOrDefaultAsync(r => r.Id == backupRunId, ct);
        if (run == null)
        {
            _logger.LogWarning("RunBackupJob.ExecuteAsync: backup run {RunId} not found", backupRunId);
            return;
        }

        await RunCoreAsync(run, ct);
    }

    public async Task ExecuteForTargetAsync(int backupTargetId, CancellationToken ct)
    {
        var target = await _context.BackupTargets.FirstOrDefaultAsync(t => t.Id == backupTargetId, ct);
        if (target == null)
        {
            _logger.LogWarning("RunBackupJob.ExecuteForTargetAsync: backup target {TargetId} not found", backupTargetId);
            return;
        }

        var run = new BackupRunEntity
        {
            BackupTargetId = target.Id,
            Scope = target.Scope,
            ServiceName = target.ServiceName,
            TenantId = target.TenantId,
            TriggerType = BackupTriggerType.Scheduled,
            Status = BackupRunStatus.Pending
        };
        _context.BackupRuns.Add(run);
        await _context.SaveChangesAsync(ct);

        await RunCoreAsync(run, ct);
    }

    private async Task RunCoreAsync(BackupRunEntity run, CancellationToken ct)
    {
        run.Status = BackupRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var connectionString = await _connectionResolver.ResolveAsync(run.Scope, run.ServiceName, run.TenantId, ct);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError(
                "RunBackupJob: no connection string could be resolved for run {RunId} (Scope={Scope}, ServiceName={ServiceName}, TenantId={TenantId})",
                run.Id, run.Scope, run.ServiceName, run.TenantId);
            run.Status = BackupRunStatus.Failed;
            run.ErrorMessage = "No connection string could be resolved for this backup target.";
            run.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return;
        }

        run.DatabaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;

        var identifier = run.Scope == BackupScope.GlobalService ? run.ServiceName : run.TenantId;
        var rootPath = _configuration["Backup:LocalStorageRootPath"] ?? "C:\\Backups\\PostgreSQL";
        var dayFolder = Path.Combine(rootPath, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dayFolder);

        var fileName = $"{run.Scope.ToString().ToLowerInvariant()}_{identifier}_{DateTime.UtcNow:yyyyMMddHHmmss}.dump";
        var outputPath = Path.Combine(dayFolder, fileName);

        try
        {
            var checksum = await _pgToolRunner.DumpAsync(connectionString, outputPath, ct);
            run.LocalFilePath = outputPath;
            run.LocalStatus = LocalBackupStatus.Saved;
            run.FileSizeBytes = new FileInfo(outputPath).Length;
            run.Sha256Checksum = checksum;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunBackupJob: pg_dump failed for run {RunId}", run.Id);
            run.LocalStatus = LocalBackupStatus.Failed;
            run.Status = BackupRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return;
        }

        // Cloud upload is independent of the local dump outcome — a storage failure must never
        // flip LocalStatus back to failed.
        if (!_blobStorage.IsConfigured)
        {
            run.CloudStatus = CloudBackupStatus.Disabled;
        }
        else
        {
            try
            {
                run.CloudStatus = CloudBackupStatus.Uploading;
                await _context.SaveChangesAsync(ct);

                var objectKey = $"{run.Scope.ToString().ToLowerInvariant()}/{identifier}/{fileName}";
                await _blobStorage.UploadAsync(objectKey, outputPath, ct);
                run.CloudStorageKey = objectKey;
                run.CloudStatus = CloudBackupStatus.Uploaded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RunBackupJob: cloud upload failed for run {RunId}", run.Id);
                run.CloudStatus = CloudBackupStatus.Failed;
                run.ErrorMessage = string.IsNullOrWhiteSpace(run.ErrorMessage)
                    ? $"Cloud upload failed: {ex.Message}"
                    : $"{run.ErrorMessage}; Cloud upload failed: {ex.Message}";
            }
        }

        run.Status = BackupRunStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }
}
