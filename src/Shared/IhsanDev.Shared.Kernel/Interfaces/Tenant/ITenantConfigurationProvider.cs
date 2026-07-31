using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace IhsanDev.Shared.Kernel.Interfaces.Tenant;

/// <summary>
/// Provides tenant-specific configuration by fetching from Tenant Service
/// </summary>
public interface ITenantConfigurationProvider
{
    /// <summary>
    /// Get tenant configuration by tenant ID
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant information with configuration, or null if not found</returns>
    Task<TenantInfo?> GetTenantConfigurationAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear cached configuration for a specific tenant (for cache invalidation)
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    void ClearCache(string tenantId);

    /// <summary>
    /// Clear all cached tenant configurations
    /// </summary>
    void ClearAllCache();

    /// <summary>
    /// Fetches every active tenant's full configuration from Tenant Service in one bulk call
    /// and repopulates the cache for each (same "tenant_config_{tenantId}" key/TTL that
    /// <see cref="GetTenantConfigurationAsync"/> reads) — used for startup cache warm-up and
    /// periodic background refresh, so a request never has to pay a cold-cache round-trip.
    /// Returns an empty list (never throws) if Tenant Service is unreachable — this is an
    /// optimization, not a hard startup dependency, so callers fall back to the existing
    /// lazy per-request fetch on a genuine miss.
    /// </summary>
    Task<IReadOnlyList<TenantInfo>> RefreshAllTenantConfigurationsAsync(CancellationToken cancellationToken = default);
}
