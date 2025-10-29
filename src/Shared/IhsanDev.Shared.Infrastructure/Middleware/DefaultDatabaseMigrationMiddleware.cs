using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Kernel.Interfaces.Database;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Middleware that automatically ensures the default database is created and migrated
/// This runs for non-tenant scenarios (when multi-tenancy is disabled or no tenant header is provided)
/// </summary>
public class DefaultDatabaseMigrationMiddleware<TContext> where TContext : DbContext
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DefaultDatabaseMigrationMiddleware<TContext>> _logger;
    private static bool _isMigrated = false;
    private static readonly SemaphoreSlim _migrationLock = new(1, 1);

    public DefaultDatabaseMigrationMiddleware(
        RequestDelegate next, 
        ILogger<DefaultDatabaseMigrationMiddleware<TContext>> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IDatabaseMigrationService migrationService)
    {
        // Check if we've already migrated the default database in this application lifetime
        if (!_isMigrated)
        {
            await _migrationLock.WaitAsync();
            try
            {
                // Double-check inside lock
                if (!_isMigrated)
                {
                    _logger.LogDebug(
                        "First request using default database, checking migration status...");

                    // Get the DbContext for the default database
                    var dbContext = context.RequestServices.GetRequiredService<TContext>();

                    // Ensure database exists and is migrated
                    var success = await migrationService.EnsureDatabaseExistsAsync(
                        dbContext, 
                        "default", 
                        context.RequestAborted);

                    if (success)
                    {
                        _isMigrated = true;
                        _logger.LogInformation(
                            "Database migration check completed successfully for default database");
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Database migration check failed for default database, continuing anyway...");
                        // Continue anyway - let the database operations fail if there's a real issue
                    }
                }
            }
            finally
            {
                _migrationLock.Release();
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Clear the migration cache (useful for testing or when database is modified)
    /// </summary>
    public static void ClearMigrationCache()
    {
        _migrationLock.Wait();
        try
        {
            _isMigrated = false;
        }
        finally
        {
            _migrationLock.Release();
        }
    }
}
