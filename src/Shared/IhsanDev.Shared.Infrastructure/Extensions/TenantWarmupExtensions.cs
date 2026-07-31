using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Database;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Startup-time tenant warm-up: fetches every active tenant's configuration in one bulk call
/// and (optionally) eagerly runs the same per-tenant database connectivity + pending-migrations
/// check that <see cref="DatabaseMigrationMiddleware{TContext}"/> would otherwise defer to each
/// tenant's first real request. Call from Program.cs right before app.Run(), the same place
/// DatabaseExtensions.InitializeDatabaseAsync is called for the global database.
/// </summary>
public static class TenantWarmupExtensions
{
    /// <summary>
    /// Bulk-refreshes the tenant configuration cache for every active tenant. Safe to call
    /// even if multi-tenancy is disabled or Tenant Service is unreachable — returns an empty
    /// list rather than throwing, since this is an optimization, not a startup dependency.
    /// </summary>
    public static async Task<IReadOnlyList<TenantInfo>> WarmTenantConfigCacheAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (!configuration.GetValue<bool>("MultiTenancy:Enabled", false))
        {
            return Array.Empty<TenantInfo>();
        }

        var configProvider = scope.ServiceProvider.GetRequiredService<ITenantConfigurationProvider>();
        return await configProvider.RefreshAllTenantConfigurationsAsync(cancellationToken);
    }

    /// <summary>
    /// For every given tenant, eagerly runs the connectivity + pending-migrations check for
    /// <typeparamref name="TContext"/> and, on success, marks the tenant as already migrated so
    /// <see cref="DatabaseMigrationMiddleware{TContext}"/> skips it on the real first request.
    /// A failure for one tenant is logged and skipped, not fatal — that tenant simply falls back
    /// to the existing lazy check on its first real request.
    /// </summary>
    public static async Task WarmTenantDatabaseMigrationsAsync<TContext>(
        this IServiceProvider services,
        IReadOnlyList<TenantInfo> tenants,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        if (tenants.Count == 0)
        {
            return;
        }

        using var rootScope = services.CreateScope();
        var logger = rootScope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

        foreach (var tenant in tenants)
        {
            if (tenant.Configuration?.DatabaseSettings?.ConnectionString is null)
            {
                continue;
            }

            try
            {
                using var tenantScope = services.CreateScope();
                var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.SetTenant(tenant);

                var dbContext = tenantScope.ServiceProvider.GetRequiredService<TContext>();
                var migrationService = tenantScope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();

                var success = await migrationService.EnsureDatabaseExistsAsync(dbContext, tenant.TenantId, cancellationToken);
                if (success)
                {
                    DatabaseMigrationMiddleware<TContext>.MarkAsMigrated(tenant.TenantId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Startup migration warm-up failed for tenant '{TenantId}' ({ContextType}) — " +
                    "will retry on that tenant's first real request instead",
                    tenant.TenantId, typeof(TContext).Name);
            }
        }

        logger.LogInformation(
            "Startup migration warm-up completed for {ContextType} across {Count} tenant(s)",
            typeof(TContext).Name, tenants.Count);
    }
}
