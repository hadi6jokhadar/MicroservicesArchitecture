namespace Backup.Application.Interfaces;

/// <summary>
/// Abstraction over the Hangfire-executed backup job, defined here (not in Infrastructure) so
/// Application-layer command handlers can enqueue work via <c>Hangfire.IBackgroundJobClient</c>
/// without taking a compile-time dependency on Backup.Infrastructure.
/// </summary>
public interface IRunBackupJob
{
    /// <summary>Manual-trigger entry point — the <see cref="Backup.Domain.Entities.BackupRunEntity"/> already exists as Pending.</summary>
    Task ExecuteAsync(int backupRunId, CancellationToken ct);

    /// <summary>Scheduled entry point — creates its own run for the given target, then delegates to the same core logic.</summary>
    Task ExecuteForTargetAsync(int backupTargetId, CancellationToken ct);
}
