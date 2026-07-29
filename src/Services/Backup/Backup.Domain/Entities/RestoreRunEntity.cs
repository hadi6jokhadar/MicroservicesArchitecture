using Backup.Domain.Enums;
using IhsanDev.Shared.Kernel.Entities;

namespace Backup.Domain.Entities;

/// <summary>
/// A single restore execution — always tied to the specific <see cref="BackupRunEntity"/> it restored from.
/// </summary>
public class RestoreRunEntity : BaseEntity
{
    /// <summary>
    /// Required FK to the <see cref="BackupRunEntity"/> being restored.
    /// Deleting a backup run while restore history references it is blocked (<c>DeleteBehavior.Restrict</c>).
    /// </summary>
    public int BackupRunId { get; set; }

    /// <summary>
    /// Overall lifecycle status of this restore run. Reuses <see cref="BackupRunStatus"/>.
    /// Intentionally hides <see cref="IhsanDev.Shared.Kernel.Entities.BaseEntity.Status"/> (an
    /// unrelated generic soft-toggle bool) — see the matching note on <c>BackupRunEntity.Status</c>.
    /// </summary>
    public new BackupRunStatus Status { get; set; } = BackupRunStatus.Pending;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Optional connection string override — restores to a different target than the original
    /// database (e.g. restoring into a scratch/staging database for verification).
    /// </summary>
    public string? TargetConnectionOverride { get; set; }

    /// <summary>Identity user id who triggered this restore.</summary>
    public int? TriggeredByUserId { get; set; }

    /// <summary>Identity user email who triggered this restore.</summary>
    public string? TriggeredByEmail { get; set; }

    /// <summary>Error message captured when <see cref="Status"/> is <see cref="BackupRunStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; set; }
}
