using System.Globalization;
using Backup.Domain.Entities;

namespace Backup.Application.DTOs;

/// <summary>
/// Read-model for a single restore execution. Manual mapping only — no AutoMapper.
/// Enums are rendered as their string name (not the raw int) for readability in JSON.
/// </summary>
public class RestoreRunDto
{
    public int Id { get; set; }

    public int BackupRunId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? StartedAt { get; set; }

    public string? CompletedAt { get; set; }

    public string? TargetConnectionOverride { get; set; }

    public int? TriggeredByUserId { get; set; }

    public string? TriggeredByEmail { get; set; }

    public string? ErrorMessage { get; set; }

    public string Created { get; set; } = string.Empty;

    public static RestoreRunDto MapFrom(RestoreRunEntity entity) => new()
    {
        Id = entity.Id,
        BackupRunId = entity.BackupRunId,
        Status = entity.Status.ToString(),
        StartedAt = entity.StartedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        CompletedAt = entity.CompletedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        TargetConnectionOverride = entity.TargetConnectionOverride,
        TriggeredByUserId = entity.TriggeredByUserId,
        TriggeredByEmail = entity.TriggeredByEmail,
        ErrorMessage = entity.ErrorMessage,
        Created = entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
