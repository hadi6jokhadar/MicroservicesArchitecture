using System.Globalization;
using Backup.Domain.Entities;

namespace Backup.Application.DTOs;

/// <summary>
/// One row per known <see cref="Backup.Domain.Entities.BackupTargetEntity"/>, enriched with its
/// most recent backup run (if any). Used by the admin "backup health" dashboard.
/// </summary>
public class BackupSummaryDto
{
    public string Scope { get; set; } = string.Empty;

    public string? ServiceName { get; set; }

    public string? TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int? LastBackupRunId { get; set; }

    public string? LastBackupAt { get; set; }

    public string? LastBackupStatus { get; set; }

    public string? LastLocalStatus { get; set; }

    public string? LastCloudStatus { get; set; }

    public long? LastFileSizeBytes { get; set; }

    public string? LastErrorMessage { get; set; }

    public static BackupSummaryDto MapFrom(BackupTargetEntity target, BackupRunEntity? lastRun) => new()
    {
        Scope = target.Scope.ToString(),
        ServiceName = target.ServiceName,
        TenantId = target.TenantId,
        DisplayName = target.DisplayName,
        LastBackupRunId = lastRun?.Id,
        LastBackupAt = lastRun?.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LastBackupStatus = lastRun?.Status.ToString(),
        LastLocalStatus = lastRun?.LocalStatus.ToString(),
        LastCloudStatus = lastRun?.CloudStatus.ToString(),
        LastFileSizeBytes = lastRun?.FileSizeBytes,
        LastErrorMessage = lastRun?.ErrorMessage
    };
}
