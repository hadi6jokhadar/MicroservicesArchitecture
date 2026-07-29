namespace Backup.Domain.Enums;

/// <summary>How a <c>BackupRunEntity</c> was initiated.</summary>
public enum BackupTriggerType
{
    /// <summary>Triggered automatically by a recurring schedule.</summary>
    Scheduled = 0,

    /// <summary>Triggered manually by a SuperAdmin user.</summary>
    Manual = 1
}
