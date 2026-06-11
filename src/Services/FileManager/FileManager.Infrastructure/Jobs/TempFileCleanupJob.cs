using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileManager.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that replaces the polling loop in <c>TempFileCleanupService</c>.
/// Scheduled daily at 02:00 UTC via Hangfire cron. Iterates all tenants in parallel
/// and deletes temp files older than 7 days (30 days for AI temp files).
/// </summary>
public class TempFileCleanupJob
{
    private const int OlderThanDays = 7;
    private const int AiOlderThanDays = 30;

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TempFileCleanupJob> _logger;

    public TempFileCleanupJob(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<TempFileCleanupJob> logger)
    {
        _serviceProvider  = serviceProvider;
        _configuration    = configuration;
        _logger           = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("TempFileCleanupJob started");

        var multiTenancyEnabled = _configuration.GetValue<bool>("MultiTenancy:Enabled");

        if (!multiTenancyEnabled)
        {
            await CleanupSingleTenantAsync(ct);
            return;
        }

        var tenants = await FetchAllTenantsAsync(ct);

        if (tenants.Count == 0)
        {
            _logger.LogWarning("No tenants found for temp file cleanup");
            return;
        }

        _logger.LogInformation("Found {TenantCount} tenants. Starting parallel cleanup...", tenants.Count);

        var results = await Task.WhenAll(tenants.Select(async tenant =>
        {
            try
            {
                await CleanupForTenantAsync(tenant, ct);
                return (tenant.TenantId, success: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup temp files for tenant {TenantId}", tenant.TenantId);
                return (tenant.TenantId, success: false);
            }
        }));

        _logger.LogInformation(
            "TempFileCleanupJob completed. Success: {SuccessCount}, Failed: {FailureCount}",
            results.Count(r => r.success), results.Count(r => !r.success));
    }

    private async Task<List<TenantConfigDto>> FetchAllTenantsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<ITenantServiceClient>();
            return await client.GetAllTenantsWithConfigAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenants for temp file cleanup");
            return [];
        }
    }

    private async Task CleanupSingleTenantAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var deleted = await mediator.Send(
            new DeleteOldTempFilesCommand(OlderThanDays: OlderThanDays, AiOlderThanDays: AiOlderThanDays), ct);
        _logger.LogInformation("Single-tenant cleanup: deleted {Count} temp files", deleted);
    }

    private async Task CleanupForTenantAsync(TenantConfigDto tenant, CancellationToken ct)
    {
        if (tenant.Data?.DatabaseSettings == null)
        {
            _logger.LogWarning("Tenant {TenantId} has no database config. Skipping.", tenant.TenantId);
            return;
        }

        using var scope = _serviceProvider.CreateScope();

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(new TenantInfo
        {
            TenantId      = tenant.TenantId,
            TenantName    = tenant.TenantName,
            IsActive      = tenant.IsActive,
            Configuration = tenant.Data
        });

        var dbContext = scope.ServiceProvider.GetRequiredService<FileManagerDbContext>();
        var pending = await dbContext.Database.GetPendingMigrationsAsync(ct);
        if (pending.Any())
            await dbContext.Database.MigrateWithRecoveryAsync(_logger, ct);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var deleted = await mediator.Send(
            new DeleteOldTempFilesCommand(OlderThanDays: OlderThanDays, AiOlderThanDays: AiOlderThanDays), ct);

        _logger.LogInformation(
            "Tenant {TenantId}: deleted {Count} temp files",
            tenant.TenantId, deleted);
    }
}
