using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Database;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Middleware that automatically ensures tenant databases are created and migrated
/// Only runs when multi-tenancy is enabled and tenant is resolved
/// This runs after TenantMiddleware resolves the tenant context
/// </summary>
public class DatabaseMigrationMiddleware<TContext> where TContext : DbContext
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DatabaseMigrationMiddleware<TContext>> _logger;
    private static readonly HashSet<string> _migratedTenants = new();
    private static readonly SemaphoreSlim _migrationLock = new(1, 1);

    public DatabaseMigrationMiddleware(
        RequestDelegate next, 
        ILogger<DatabaseMigrationMiddleware<TContext>> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IDatabaseMigrationService migrationService)
    {
        // Only run if multi-tenancy is enabled and tenant is resolved
        if (tenantContext.IsMultiTenantMode && 
            tenantContext.HasTenant && 
            tenantContext.CurrentTenant?.Configuration?.DatabaseSettings?.ConnectionString != null)
        {
            var tenantId = tenantContext.CurrentTenant.TenantId;

            // Check if we've already migrated this tenant in this application lifetime
            if (!_migratedTenants.Contains(tenantId))
            {
                await _migrationLock.WaitAsync();
                try
                {
                    // Double-check inside lock
                    if (!_migratedTenants.Contains(tenantId))
                    {
                        _logger.LogDebug(
                            "First request for tenant '{TenantId}', checking database migration status...", 
                            tenantId);

                        // Get the DbContext for this tenant
                        var dbContext = context.RequestServices.GetRequiredService<TContext>();

                        // Ensure database exists and is migrated
                        var success = await migrationService.EnsureDatabaseExistsAsync(
                            dbContext, 
                            tenantId, 
                            context.RequestAborted);

                        if (success)
                        {
                            _migratedTenants.Add(tenantId);
                            _logger.LogInformation(
                                "Database migration check completed successfully for tenant '{TenantId}'", 
                                tenantId);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Database migration check failed for tenant '{TenantId}', continuing anyway...", 
                                tenantId);
                            // Continue anyway - let the database operations fail if there's a real issue
                        }
                    }
                }
                finally
                {
                    _migrationLock.Release();
                }
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Clear the migration cache (useful for testing or when tenants are modified)
    /// </summary>
    public static void ClearMigrationCache(string? tenantId = null)
    {
        _migrationLock.Wait();
        try
        {
            if (tenantId != null)
            {
                _migratedTenants.Remove(tenantId);
            }
            else
            {
                _migratedTenants.Clear();
            }
        }
        finally
        {
            _migrationLock.Release();
        }
    }
}
