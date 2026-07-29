namespace Backup.Domain.Enums;

/// <summary>
/// Identifies whether a backup target/run applies to an entire service's global database
/// or to a single tenant's database. Shared by <c>BackupTargetEntity</c> and <c>BackupRunEntity</c>.
/// </summary>
public enum BackupScope
{
    /// <summary>The target is a service-level global database (e.g. Tenant, Translation).</summary>
    GlobalService = 0,

    /// <summary>The target is a single tenant's isolated database.</summary>
    Tenant = 1
}
