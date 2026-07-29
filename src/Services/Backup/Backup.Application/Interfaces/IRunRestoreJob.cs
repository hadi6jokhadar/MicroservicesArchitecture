namespace Backup.Application.Interfaces;

/// <summary>
/// Abstraction over the Hangfire-executed restore job — see <see cref="IRunBackupJob"/> for why
/// this lives in Application rather than Infrastructure.
/// </summary>
public interface IRunRestoreJob
{
    Task ExecuteAsync(int restoreRunId, CancellationToken ct);
}
