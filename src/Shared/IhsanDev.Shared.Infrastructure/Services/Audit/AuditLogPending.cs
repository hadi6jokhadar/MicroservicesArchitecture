namespace IhsanDev.Shared.Infrastructure.Services.Audit;

internal sealed record AuditLogPending(
    string Action,
    string EntityType,
    string? EntityId,
    string? TenantId,
    string? UserId,
    string? UserEmail,
    string? IpAddress,
    DateTimeOffset OccurredAt,
    object? Before,
    object? After);
