using Backup.Domain.Enums;
using IhsanDev.Shared.Kernel.Entities;

namespace Backup.Domain.Entities;

/// <summary>
/// A single backup execution — one row per attempt (scheduled or manual), tracking local file
/// and offsite/cloud storage status independently.
/// </summary>
public class BackupRunEntity : BaseEntity
{
    /// <summary>
    /// Nullable FK to the <see cref="BackupTargetEntity"/> that produced this run.
    /// Set to null (via <c>DeleteBehavior.SetNull</c>) if the target is later deleted — history is preserved.
    /// </summary>
    public int? BackupTargetId { get; set; }

    /// <summary>Denormalized snapshot of the target's scope at the time this run was created.</summary>
    public BackupScope Scope { get; set; }

    /// <summary>Denormalized snapshot of the target's service name at the time this run was created.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Denormalized snapshot of the target's tenant id at the time this run was created.</summary>
    public string? TenantId { get; set; }

    /// <summary>Name of the database that was backed up.</summary>
    public string? DatabaseName { get; set; }

    /// <summary>How this run was initiated.</summary>
    public BackupTriggerType TriggerType { get; set; }

    /// <summary>Identity user id who triggered a manual run. Null for scheduled runs.</summary>
    public int? TriggeredByUserId { get; set; }

    /// <summary>Identity user email who triggered a manual run. Null for scheduled runs.</summary>
    public string? TriggeredByEmail { get; set; }

    /// <summary>
    /// Overall lifecycle status of this backup run. Intentionally hides
    /// <see cref="IhsanDev.Shared.Kernel.Entities.BaseEntity.Status"/> (an unrelated generic
    /// soft-toggle bool) — the task's requested shape calls for a <c>Status</c> property of type
    /// <see cref="BackupRunStatus"/> here, so the hide is explicit via <c>new</c>.
    /// </summary>
    public new BackupRunStatus Status { get; set; } = BackupRunStatus.Pending;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Path to the on-disk backup file produced by this run.</summary>
    public string? LocalFilePath { get; set; }

    /// <summary>Status of the local on-disk copy.</summary>
    public LocalBackupStatus LocalStatus { get; set; } = LocalBackupStatus.Pending;

    /// <summary>Object key of the offsite/cloud copy (e.g. Cloudflare R2 key).</summary>
    public string? CloudStorageKey { get; set; }

    /// <summary>Status of the offsite/cloud copy.</summary>
    public CloudBackupStatus CloudStatus { get; set; } = CloudBackupStatus.Pending;

    /// <summary>Size, in bytes, of the produced backup file.</summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>SHA-256 checksum of the produced backup file, used to verify integrity on restore.</summary>
    public string? Sha256Checksum { get; set; }

    /// <summary>Error message captured when <see cref="Status"/> is <see cref="BackupRunStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; set; }
}
