using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace Nasheed.Infrastructure.Services;

/// <summary>
/// Singleton cache for the single tenant that Nasheed serves.
/// Populated by NasheedTenantLoaderService at startup.
/// </summary>
public interface INasheedTenantCache
{
    /// <summary>Whether the tenant config has been loaded.</summary>
    bool IsReady { get; }

    /// <summary>The loaded tenant info (null until ready).</summary>
    TenantInfo? Tenant { get; }

    /// <summary>Waits until the tenant is loaded (or token is cancelled).</summary>
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>Called by NasheedTenantLoaderService once tenant is fetched.</summary>
    void SetTenant(TenantInfo tenant);
}
