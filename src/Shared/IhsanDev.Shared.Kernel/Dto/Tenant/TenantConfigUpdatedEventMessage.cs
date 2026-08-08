namespace IhsanDev.Shared.Kernel.Dto.Tenant;

/// <summary>
/// Slim Redis Pub/Sub payload broadcast by the Tenant Service immediately after a tenant's
/// configuration (including feature flags) is updated, archived/unarchived, or deleted, so any
/// service holding its own local in-process snapshot of that tenant's config (e.g. Nasheed's
/// INasheedTenantCache, kept fresh per-request by every other multi-tenant service instead) can
/// refresh it without waiting for a restart. Best-effort only — a missed message just means the
/// consumer keeps its stale snapshot until its own next periodic fallback refresh, same philosophy
/// as <see cref="TenantProvisionedEventMessage"/>.
/// </summary>
public record TenantConfigUpdatedEventMessage
{
    /// <summary>Global Redis channel — not per-tenant, since the tenant ID IS the payload.</summary>
    public const string Channel = "tenant:updated";

    /// <summary>Bump this when adding/removing/renaming fields. Consumers skip unknown versions.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string TenantId { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
