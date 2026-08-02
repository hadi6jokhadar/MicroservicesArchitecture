namespace IhsanDev.Shared.Kernel.Dto.Tenant;

/// <summary>
/// Slim Redis Pub/Sub payload broadcast by the Tenant Service immediately after a new tenant
/// is created, so every already-running multi-tenant service can eagerly migrate (and seed)
/// that tenant's database instead of waiting for the tenant's first HTTP request or the next
/// service restart. Best-effort only — a missed message is not fatal, since
/// <see cref="IhsanDev.Shared.Infrastructure.Middleware.DatabaseMigrationMiddleware{TContext}"/>
/// (per-request) and startup tenant warm-up remain as fallbacks.
/// </summary>
public record TenantProvisionedEventMessage
{
    /// <summary>Global Redis channel — not per-tenant, since the tenant ID IS the payload.</summary>
    public const string Channel = "tenant:provisioned";

    /// <summary>Bump this when adding/removing/renaming fields. Consumers skip unknown versions.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string TenantId { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
