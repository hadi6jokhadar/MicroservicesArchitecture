using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Services;

/// <summary>
/// Hosted service that fetches the single tenant's configuration from TenantService on startup,
/// populates INasheedTenantCache, and runs the database migration. After the initial load, it also
/// starts a periodic background refresh (see <see cref="RefreshTenantConfigurationPeriodicallyAsync"/>)
/// as a FALLBACK safety net — the primary refresh path is
/// <see cref="NasheedTenantConfigUpdatedListenerService"/>, which reacts to Tenant Service's
/// tenant:updated Redis Pub/Sub broadcast within a second or two of a save. This periodic loop only
/// matters if that broadcast is ever missed (Redis briefly disconnected, this service restarting at
/// the exact moment of the publish) — same best-effort-push + slow-fallback philosophy already used
/// for tenant:provisioned. Reuses the same ITenantConfigurationProvider (and the Redis
/// tenant_config_{tenantId} cache Tenant Service already invalidates on every save) rather than a
/// separate refresh mechanism, per Dotnet.instructions.md pitfall #25.
/// The NasheedIngestionWorker awaits INasheedTenantCache.WaitUntilReadyAsync before starting.
/// </summary>
public class NasheedTenantLoaderService : IHostedService
{
    private readonly INasheedTenantCache _tenantCache;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NasheedTenantLoaderService> _logger;
    private readonly CancellationTokenSource _refreshCts = new();

    // Fallback-only interval — the push-based NasheedTenantConfigUpdatedListenerService is what
    // normally keeps INasheedTenantCache fresh within seconds. This loop exists purely to bound
    // staleness in case a tenant:updated broadcast is ever missed, so it can be long.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    public NasheedTenantLoaderService(
        INasheedTenantCache tenantCache,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<NasheedTenantLoaderService> logger)
    {
        _tenantCache = tenantCache;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tenantId = _configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. " +
                "Nasheed is a single-tenant service — set MultiTenancy:TenantId in appsettings.json.");

        _logger.LogInformation("NasheedTenantLoaderService: loading tenant '{TenantId}'...", tenantId);

        const int maxRetries = 12;
        const int retryDelaySeconds = 5;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tenantProvider = scope.ServiceProvider
                    .GetRequiredService<ITenantConfigurationProvider>();

                var tenant = await tenantProvider.GetTenantConfigurationAsync(tenantId, cancellationToken);

                if (tenant == null)
                {
                    _logger.LogWarning(
                        "Attempt {Attempt}/{Max}: Tenant '{TenantId}' not found in TenantService. Retrying in {Delay}s...",
                        attempt, maxRetries, tenantId, retryDelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(tenant.Configuration?.DatabaseSettings?.ConnectionString))
                {
                    _logger.LogWarning(
                        "Attempt {Attempt}/{Max}: Tenant '{TenantId}' has no database connection string. Retrying in {Delay}s...",
                        attempt, maxRetries, tenantId, retryDelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
                    continue;
                }

                // Signal the cache as ready (unblocks WaitUntilReadyAsync callers)
                _tenantCache.SetTenant(tenant);
                _logger.LogInformation(
                    "Tenant '{TenantId}' loaded successfully. Running database migration...", tenantId);

                await RunMigrationAsync(cancellationToken);

                // Fire-and-forget: keep refreshing tenant config after startup completes. Must not be
                // awaited here — StartAsync is awaited by the host, and migration must still finish
                // before HTTP traffic begins, same as before this change.
                _ = RefreshTenantConfigurationPeriodicallyAsync(tenantId, _refreshCts.Token);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex,
                        "Could not load tenant '{TenantId}' after {Max} attempts. " +
                        "Background ingestion will not start until the cache is populated.",
                        tenantId, maxRetries);
                    return;
                }

                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{Max}: Error loading tenant '{TenantId}'. Retrying in {Delay}s...",
                    attempt, maxRetries, tenantId, retryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _refreshCts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fallback-only: re-fetches this tenant's configuration every <see cref="RefreshInterval"/> and
    /// pushes it back into INasheedTenantCache, in case NasheedTenantConfigUpdatedListenerService ever
    /// misses a tenant:updated broadcast. Uses the same ITenantConfigurationProvider (and its Redis
    /// cache/invalidation) as the initial load — this is not a new caching mechanism, just calling the
    /// existing one again.
    /// </summary>
    private async Task RefreshTenantConfigurationPeriodicallyAsync(string tenantId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RefreshInterval, cancellationToken);

                using var scope = _serviceProvider.CreateScope();
                var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantConfigurationProvider>();
                var tenant = await tenantProvider.GetTenantConfigurationAsync(tenantId, cancellationToken);

                if (tenant != null)
                {
                    _tenantCache.SetTenant(tenant);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to refresh tenant '{TenantId}' configuration; keeping previously cached value.",
                    tenantId);
            }
        }
    }

    private async Task RunMigrationAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NasheedDbContext>();
            await dbContext.Database.MigrateWithRecoveryAsync(_logger, cancellationToken);
            _logger.LogInformation("Nasheed database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database migration failed. The service may not function correctly.");
        }
    }
}
