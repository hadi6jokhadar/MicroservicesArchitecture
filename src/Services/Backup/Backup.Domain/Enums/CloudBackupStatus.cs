namespace Backup.Domain.Enums;

/// <summary>Status of the offsite/cloud copy of a backup file (<c>BackupRunEntity.CloudStorageKey</c>).</summary>
public enum CloudBackupStatus
{
    Pending = 0,
    Uploading = 1,
    Uploaded = 2,
    Failed = 3,

    /// <summary>Cloud upload is not configured/enabled for this run — not an error state.</summary>
    Disabled = 4
}
