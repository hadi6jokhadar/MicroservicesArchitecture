using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tenant.Domain.Repositories;

namespace Tenant.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that replaces the polling loop in <c>TenantCacheRefreshService</c>.
/// Scheduled every 30 minutes via Hangfire cron — pre-loads all active tenant configs into
/// Redis so the tenant middleware can resolve them without database queries.
/// </summary>
public class TenantCacheRefreshJob
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromDays(7);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TenantCacheRefreshJob> _logger;

    public TenantCacheRefreshJob(
        IServiceProvider serviceProvider,
        ILogger<TenantCacheRefreshJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("TenantCacheRefreshJob started");

        using var scope = _serviceProvider.CreateScope();
        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var cacheService     = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var activeTenants = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(
                tenantRepository.GetAll().Where(t => t.IsActive && !t.IsArchived),
                ct);

        if (activeTenants.Count == 0)
        {
            _logger.LogWarning("No active tenants found to cache");
            return;
        }

        _logger.LogInformation("Caching {TenantCount} active tenants", activeTenants.Count);

        var results = await Task.WhenAll(activeTenants.Select(async tenant =>
        {
            try
            {
                var tenantInfo = BuildTenantInfo(tenant);
                await cacheService.SetAsync($"tenant_config_{tenant.TenantId}", tenantInfo, CacheExpiration, ct);
                _logger.LogDebug("Cached tenant {TenantId}", tenant.TenantId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cache tenant {TenantId}", tenant.TenantId);
                return false;
            }
        }));

        _logger.LogInformation(
            "TenantCacheRefreshJob completed. Cached: {Cached}, Failed: {Failed}",
            results.Count(r => r), results.Count(r => !r));
    }

    private TenantInfo BuildTenantInfo(Domain.Entities.TenantSettings tenant)
    {
        TenantConfiguration? configuration = null;

        if (!string.IsNullOrWhiteSpace(tenant.Data))
        {
            try
            {
                configuration = System.Text.Json.JsonSerializer.Deserialize<TenantConfiguration>(
                    tenant.Data,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to parse tenant config for {TenantId}. Using null config.", tenant.TenantId);
            }
        }

        return new TenantInfo
        {
            TenantId      = tenant.TenantId,
            TenantName    = tenant.TenantName,
            UserId        = tenant.UserId,
            IsActive      = tenant.IsActive,
            Configuration = configuration
        };
    }
}
