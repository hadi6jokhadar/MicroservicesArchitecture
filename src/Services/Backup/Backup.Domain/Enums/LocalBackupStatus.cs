namespace Backup.Domain.Enums;

/// <summary>Status of the local on-disk copy of a backup file (<c>BackupRunEntity.LocalFilePath</c>).</summary>
public enum LocalBackupStatus
{
    Pending = 0,
    Saved = 1,
    Failed = 2,
    Deleted = 3
}
