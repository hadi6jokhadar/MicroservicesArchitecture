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
/// populates INasheedTenantCache, and runs the database migration.
/// The NasheedIngestionWorker awaits INasheedTenantCache.WaitUntilReadyAsync before starting.
/// </summary>
public class NasheedTenantLoaderService : IHostedService
{
    private readonly INasheedTenantCache _tenantCache;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NasheedTenantLoaderService> _logger;

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunMigrationAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NasheedDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Nasheed database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database migration failed. The service may not function correctly.");
        }
    }
}
