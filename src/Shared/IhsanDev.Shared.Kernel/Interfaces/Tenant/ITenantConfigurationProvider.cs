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
}
