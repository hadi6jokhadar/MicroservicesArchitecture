using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Services.Tenant;

/// <summary>
/// Periodically re-fetches every active tenant's configuration from Tenant Service and
/// repopulates the cache, independent of the cache's own TTL.
///
/// In practice this only needs to be registered for FileManager. Identity, Category, and
/// Notification share one Redis instance/key prefix with Tenant.API, and Tenant.API's own
/// Hangfire job (TenantCacheRefreshJob, every 30 min) already writes directly into the same
/// tenant_config_{tenantId} keys those services read — a second, slower, HTTP-based refresh
/// loop on top of that would be redundant. FileManager runs with Redis disabled (isolated
/// in-process cache), so it never sees that job's writes at all; this service is what keeps
/// its tenant-config cache warm instead.
///
/// Opt-in via <see cref="TenantConfigCacheRefreshExtensions.AddTenantConfigCacheRefresh"/> —
/// NOT part of AddMultiTenancy, for the reasons above, and also because single-tenant-per-
/// deployment services (e.g. Nasheed, which pins one fixed MultiTenancy:TenantId) have no
/// "every active tenant" to refresh and already run their own tailored startup loader.
/// </summary>
public class TenantConfigCacheRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _interval;
    private readonly ILogger<TenantConfigCacheRefreshService> _logger;

    public TenantConfigCacheRefreshService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<TenantConfigCacheRefreshService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interval = TimeSpan.FromHours(
            configuration.GetValue<double>("MultiTenancy:CacheRefreshIntervalHours", 6));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Skip the immediate first tick — WarmTenantConfigCacheAsync already ran once at
        // startup (see TenantWarmupExtensions), so wait a full interval before the first refresh.
        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var configProvider = scope.ServiceProvider.GetRequiredService<ITenantConfigurationProvider>();
                var tenants = await configProvider.RefreshAllTenantConfigurationsAsync(stoppingToken);
                _logger.LogDebug("Periodic tenant config cache refresh completed for {Count} tenant(s)", tenants.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a transient Tenant Service outage kill this background loop — the
                // existing cache entries (however stale) remain in place until the next tick.
                _logger.LogWarning(ex, "Periodic tenant config cache refresh failed — will retry next interval");
            }
        }
    }
}

public static class TenantConfigCacheRefreshExtensions
{
    /// <summary>
    /// Registers the periodic tenant-config cache refresh as a hosted service. Call only from
    /// services that serve many tenants per process (Identity, Category, FileManager,
    /// Notification) — not from single-tenant-per-deployment services like Nasheed.
    /// </summary>
    public static IServiceCollection AddTenantConfigCacheRefresh(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("MultiTenancy:Enabled", false))
        {
            return services;
        }

        return services.AddHostedService<TenantConfigCacheRefreshService>();
    }
}
