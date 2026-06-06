using System.ComponentModel.DataAnnotations;

namespace IhsanDev.Shared.Kernel.Entities;

public class AuditLogEntity
{
    [Key]
    public long Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? TenantId { get; set; }

    public string? UserId { get; set; }

    public string? UserEmail { get; set; }

    public string? Before { get; set; }

    public string? After { get; set; }

    public string? IpAddress { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
