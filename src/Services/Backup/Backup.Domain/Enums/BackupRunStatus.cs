namespace Backup.Domain.Enums;

/// <summary>
/// Overall lifecycle status of a <c>BackupRunEntity</c>. Also reused by
/// <c>RestoreRunEntity.Status</c> since both share the same Pending/Running/Completed/Failed lifecycle.
/// </summary>
public enum BackupRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}
