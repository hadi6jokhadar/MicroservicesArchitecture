using System.Globalization;
using Backup.Domain.Entities;

namespace Backup.Application.DTOs;

/// <summary>
/// Read-model for a single backup execution. Manual mapping only — no AutoMapper.
/// Enums are rendered as their string name (not the raw int) for readability in JSON.
/// </summary>
public class BackupRunDto
{
    public int Id { get; set; }

    public int? BackupTargetId { get; set; }

    public string Scope { get; set; } = string.Empty;

    public string? ServiceName { get; set; }

    public string? TenantId { get; set; }

    public string? DatabaseName { get; set; }

    public string TriggerType { get; set; } = string.Empty;

    public int? TriggeredByUserId { get; set; }

    public string? TriggeredByEmail { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? StartedAt { get; set; }

    public string? CompletedAt { get; set; }

    public string? LocalFilePath { get; set; }

    public string LocalStatus { get; set; } = string.Empty;

    public string? CloudStorageKey { get; set; }

    public string CloudStatus { get; set; } = string.Empty;

    public long? FileSizeBytes { get; set; }

    public string? Sha256Checksum { get; set; }

    public string? ErrorMessage { get; set; }

    public string Created { get; set; } = string.Empty;

    public static BackupRunDto MapFrom(BackupRunEntity entity) => new()
    {
        Id = entity.Id,
        BackupTargetId = entity.BackupTargetId,
        Scope = entity.Scope.ToString(),
        ServiceName = entity.ServiceName,
        TenantId = entity.TenantId,
        DatabaseName = entity.DatabaseName,
        TriggerType = entity.TriggerType.ToString(),
        TriggeredByUserId = entity.TriggeredByUserId,
        TriggeredByEmail = entity.TriggeredByEmail,
        Status = entity.Status.ToString(),
        StartedAt = entity.StartedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        CompletedAt = entity.CompletedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LocalFilePath = entity.LocalFilePath,
        LocalStatus = entity.LocalStatus.ToString(),
        CloudStorageKey = entity.CloudStorageKey,
        CloudStatus = entity.CloudStatus.ToString(),
        FileSizeBytes = entity.FileSizeBytes,
        Sha256Checksum = entity.Sha256Checksum,
        ErrorMessage = entity.ErrorMessage,
        Created = entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
