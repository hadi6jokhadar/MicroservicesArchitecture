using System.Globalization;
using IhsanDev.Shared.Kernel.Entities;

namespace IhsanDev.Shared.Application.Audit;

public class AuditLogDto
{
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
    public string OccurredAt { get; set; } = string.Empty;

    public static AuditLogDto MapFrom(AuditLogEntity entity) => new()
    {
        Id = entity.Id,
        Action = entity.Action,
        EntityType = entity.EntityType,
        EntityId = entity.EntityId,
        TenantId = entity.TenantId,
        UserId = entity.UserId,
        UserEmail = entity.UserEmail,
        Before = entity.Before,
        After = entity.After,
        IpAddress = entity.IpAddress,
        OccurredAt = entity.OccurredAt.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
