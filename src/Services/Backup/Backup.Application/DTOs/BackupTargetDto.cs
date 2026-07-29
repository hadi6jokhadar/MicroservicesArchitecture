using System.Globalization;
using Backup.Domain.Entities;

namespace Backup.Application.DTOs;

/// <summary>
/// Read-model for a configured backup target (either a service's global database or a
/// single tenant's database). Manual mapping only — no AutoMapper.
/// </summary>
public class BackupTargetDto
{
    public int Id { get; set; }

    public string Scope { get; set; } = string.Empty;

    public string? ServiceName { get; set; }

    public string? TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public int? RetentionDays { get; set; }

    public string Created { get; set; } = string.Empty;

    public static BackupTargetDto MapFrom(BackupTargetEntity entity) => new()
    {
        Id = entity.Id,
        Scope = entity.Scope.ToString(),
        ServiceName = entity.ServiceName,
        TenantId = entity.TenantId,
        DisplayName = entity.DisplayName,
        IsEnabled = entity.IsEnabled,
        RetentionDays = entity.RetentionDays,
        Created = entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
