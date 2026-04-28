using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Infrastructure.Persistence;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileManager.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that cleans up temporary files across all tenants every 24 hours
/// </summary>
public class TempFileCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TempFileCleanupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);
    private readonly int _olderThanDays = 7; // Delete temp files older than 7 days
    private readonly int _aiOlderThanDays = 30; // Delete AI temp files older than 30 days

    public TempFileCleanupService(
        IServiceProvider serviceProvider,
        ILogger<TempFileCleanupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Temp File Cleanup Service started. Running every 24 hours.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupTempFilesAcrossAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up temp files across tenants");
            }

            // Wait for 24 hours before next cleanup
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CleanupTempFilesAcrossAllTenantsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting temp file cleanup across all tenants...");

        // Check if multi-tenancy is enabled
        var multiTenancyEnabled = _configuration.GetValue<bool>("MultiTenancy:Enabled");

        if (!multiTenancyEnabled)
        {
            // Single-tenant mode: clean up using default connection
            await CleanupTempFilesForSingleTenantAsync(cancellationToken);
            return;
        }

        // Multi-tenant mode: fetch all tenants and clean up for each
        var tenants = await FetchAllTenantsAsync(cancellationToken);

        if (tenants == null || tenants.Count == 0)
        {
            _logger.LogWarning("No tenants found for temp file cleanup");
            return;
        }

        _logger.LogInformation("Found {TenantCount} tenants. Starting parallel cleanup process...", tenants.Count);

        // OPTIMIZATION: Parallel processing for tenant cleanup (5-10x faster)
        var cleanupTasks = tenants.Select(async tenant =>
        {
            try
            {
                await CleanupTempFilesForTenantAsync(tenant, cancellationToken);
                return (tenant.TenantId, success: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup temp files for tenant: {TenantId}", tenant.TenantId);
                return (tenant.TenantId, success: false);
            }
        });

        // Wait for all parallel operations to complete
        var results = await Task.WhenAll(cleanupTasks);
        int successCount = results.Count(r => r.success);
        int failureCount = results.Count(r => !r.success);

        _logger.LogInformation(
            "Temp file cleanup completed. Success: {SuccessCount}, Failed: {FailureCount}",
            successCount, failureCount);
    }

    private async Task<List<TenantConfigDto>> FetchAllTenantsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var tenantServiceClient = scope.ServiceProvider.GetRequiredService<ITenantServiceClient>();

            // Fetch all tenants with config from Tenant service
            var tenants = await tenantServiceClient.GetAllTenantsWithConfigAsync(cancellationToken);

            return tenants;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching tenants from Tenant service");
            return new List<TenantConfigDto>();
        }
    }

    private async Task CleanupTempFilesForSingleTenantAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new DeleteOldTempFilesCommand(OlderThanDays: _olderThanDays, AiOlderThanDays: _aiOlderThanDays);
            var deletedCount = await mediator.Send(command, cancellationToken);

            _logger.LogInformation(
                "Single-tenant cleanup completed. Deleted {DeletedCount} temp files",
                deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during single-tenant temp file cleanup");
            throw;
        }
    }

    /// <summary>
    /// Cleans up temporary files for a specific tenant
    /// Sets tenant context, applies migrations if needed, and executes cleanup
    /// </summary>
    private async Task CleanupTempFilesForTenantAsync(TenantConfigDto tenant, CancellationToken cancellationToken)
    {
        if (tenant.Data?.DatabaseSettings == null)
        {
            _logger.LogWarning("Tenant {TenantId} has no database configuration. Skipping...", tenant.TenantId);
            return;
        }

        _logger.LogInformation("Cleaning temp files for tenant: {TenantId}", tenant.TenantId);

        using var scope = _serviceProvider.CreateScope();
        
        // Set tenant context for database resolution
        SetTenantContext(scope, tenant);

        // Ensure database schema is up to date
        if (!await EnsureDatabaseMigratedAsync(scope, tenant.TenantId, cancellationToken))
        {
            return; // Skip cleanup if migration fails
        }

        // Execute cleanup command
        await ExecuteCleanupCommandAsync(scope, tenant.TenantId, cancellationToken);
    }

    /// <summary>
    /// Sets the tenant context in the current scope
    /// </summary>
    private void SetTenantContext(IServiceScope scope, TenantConfigDto tenant)
    {
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        
        var tenantInfo = new TenantInfo
        {
            TenantId = tenant.TenantId,
            TenantName = tenant.TenantName,
            IsActive = tenant.IsActive,
            Configuration = tenant.Data
        };
        
        tenantContext.SetTenant(tenantInfo);
    }

    /// <summary>
    /// Ensures the database is migrated to the latest schema
    /// Returns true if successful, false if migration fails
    /// </summary>
    private async Task<bool> EnsureDatabaseMigratedAsync(
        IServiceScope scope, 
        string tenantId, 
        CancellationToken cancellationToken)
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FileManagerDbContext>();
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            
            if (pendingMigrations.Any())
            {
                _logger.LogInformation(
                    "Applying {Count} pending migrations for tenant {TenantId}", 
                    pendingMigrations.Count(), 
                    tenantId);
                
                await dbContext.Database.MigrateAsync(cancellationToken);
                
                _logger.LogInformation(
                    "Successfully applied migrations for tenant {TenantId}", 
                    tenantId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to apply migrations for tenant {TenantId}. Skipping cleanup.", 
                tenantId);
            return false;
        }
    }

    /// <summary>
    /// Executes the temp file cleanup command via MediatR
    /// </summary>
    private async Task ExecuteCleanupCommandAsync(
        IServiceScope scope, 
        string tenantId, 
        CancellationToken cancellationToken)
    {
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var command = new DeleteOldTempFilesCommand(OlderThanDays: _olderThanDays, AiOlderThanDays: _aiOlderThanDays);
        var deletedCount = await mediator.Send(command, cancellationToken);

        _logger.LogInformation(
            "Tenant {TenantId}: Deleted {DeletedCount} temp files (Standard: {Days} days, AI: {AiDays} days)",
            tenantId, deletedCount, _olderThanDays, _aiOlderThanDays);
    }
}
