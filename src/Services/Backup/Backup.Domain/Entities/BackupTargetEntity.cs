using Backup.Domain.Enums;
using IhsanDev.Shared.Kernel.Entities;

namespace Backup.Domain.Entities;

/// <summary>
/// A configured backup target — either a service's global database or a single tenant's database.
/// </summary>
public class BackupTargetEntity : BaseEntity
{
    /// <summary>Whether this target is a service-level global database or a tenant database.</summary>
    public BackupScope Scope { get; set; }

    /// <summary>Set when <see cref="Scope"/> is <see cref="BackupScope.GlobalService"/>.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Set when <see cref="Scope"/> is <see cref="BackupScope.Tenant"/>.</summary>
    public string? TenantId { get; set; }

    /// <summary>Human-readable name shown in admin UIs.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Whether scheduled backups are currently enabled for this target.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Number of days to retain backups for this target before cleanup. Null = keep indefinitely.</summary>
    public int? RetentionDays { get; set; }
}
