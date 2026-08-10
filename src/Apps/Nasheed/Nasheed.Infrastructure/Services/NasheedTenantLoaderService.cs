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
/// If the fast retry loop below exhausts without success, StartAsync does NOT give up permanently —
/// see <see cref="RetryTenantLoadInBackgroundAsync"/>.
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

    // Used only after the fast startup retry loop (12 attempts x 5s) exhausts without success —
    // keeps trying indefinitely at this slower pace so a Tenant Service outage longer than a minute
    // doesn't permanently strand NasheedIngestionWorker (blocked on WaitUntilReadyAsync with no
    // timeout) until someone notices and restarts the process.
    private static readonly TimeSpan FallbackRetryInterval = TimeSpan.FromMinutes(1);

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
                if (await TryLoadTenantAsync(tenantId, cancellationToken))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{Max}: Error loading tenant '{TenantId}'.",
                    attempt, maxRetries, tenantId);
            }

            if (attempt < maxRetries)
            {
                _logger.LogWarning(
                    "Attempt {Attempt}/{Max}: Tenant '{TenantId}' not ready yet. Retrying in {Delay}s...",
                    attempt, maxRetries, tenantId, retryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
            }
        }

        // Fast retries exhausted — do NOT give up permanently. NasheedIngestionWorker is blocked on
        // INasheedTenantCache.WaitUntilReadyAsync with no timeout of its own, so if nothing ever calls
        // SetTenant, it stays parked forever and the only recovery is a full service restart. Keep
        // trying at a slower pace in the background instead, so a Tenant Service outage longer than
        // ~a minute can still self-heal once it comes back.
        _logger.LogError(
            "Could not load tenant '{TenantId}' after {Max} attempts. Falling back to a background " +
            "retry every {Interval} — ingestion stays paused until it succeeds.",
            tenantId, maxRetries, FallbackRetryInterval);
        _ = RetryTenantLoadInBackgroundAsync(tenantId, _refreshCts.Token);
    }

    /// <summary>
    /// One tenant-load attempt: fetches config, and if it's usable (found, has a connection string),
    /// populates the cache, runs the DB migration, and starts the periodic refresh loop — the same
    /// three things the original inline success path did. Shared by the fast startup retry loop and
    /// <see cref="RetryTenantLoadInBackgroundAsync"/> so both paths stay in sync; a caller must never
    /// populate the cache without also migrating, or NasheedIngestionWorker would wake up and query a
    /// database that doesn't have its schema yet.
    /// </summary>
    private async Task<bool> TryLoadTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantConfigurationProvider>();
        var tenant = await tenantProvider.GetTenantConfigurationAsync(tenantId, cancellationToken);

        if (tenant == null)
        {
            _logger.LogWarning("Tenant '{TenantId}' not found in TenantService yet.", tenantId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(tenant.Configuration?.DatabaseSettings?.ConnectionString))
        {
            _logger.LogWarning("Tenant '{TenantId}' has no database connection string yet.", tenantId);
            return false;
        }

        // Signal the cache as ready (unblocks WaitUntilReadyAsync callers)
        _tenantCache.SetTenant(tenant);
        _logger.LogInformation(
            "Tenant '{TenantId}' loaded successfully. Running database migration...", tenantId);

        await RunMigrationAsync(cancellationToken);

        // Fire-and-forget: keep refreshing tenant config after this returns. Must not be awaited here
        // — the caller (StartAsync, or the fallback loop below) needs migration to finish first.
        _ = RefreshTenantConfigurationPeriodicallyAsync(tenantId, _refreshCts.Token);
        return true;
    }

    /// <summary>
    /// Runs only after StartAsync's fast retry loop exhausts without success. Keeps retrying
    /// indefinitely (until the host shuts down) at <see cref="FallbackRetryInterval"/> — recovering
    /// from a longer Tenant Service outage is worth an occasional retry even at a slow pace, rather
    /// than requiring someone to notice and restart the process.
    /// </summary>
    private async Task RetryTenantLoadInBackgroundAsync(string tenantId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(FallbackRetryInterval, cancellationToken);

                if (await TryLoadTenantAsync(tenantId, cancellationToken))
                {
                    _logger.LogInformation(
                        "Tenant '{TenantId}' loaded successfully via background fallback retry.", tenantId);
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Background fallback retry failed to load tenant '{TenantId}'; will try again in {Interval}.",
                    tenantId, FallbackRetryInterval);
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
