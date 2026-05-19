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
        // Skip migration check for static paths (Swagger, health checks, etc.)
        if (ShouldSkipMigration(context.Request.Path))
        {
            await _next(context);
            return;
        }

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

                    // Use a child scope so the request-scope DbContext is NOT pre-created here.
                    // If we resolved TContext from context.RequestServices directly, the scoped
                    // DbContext would be constructed before tenant resolution (using the global DB
                    // connection string). DatabaseMigrationMiddleware would then receive that same
                    // scoped instance and incorrectly mark the tenant as migrated against the global
                    // DB instead of the actual tenant DB.
                    using var migrationScope = context.RequestServices.CreateScope();
                    var dbContext = migrationScope.ServiceProvider.GetRequiredService<TContext>();

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
    /// Determines if migration check should be skipped for the given path
    /// </summary>
    private static bool ShouldSkipMigration(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        if (string.IsNullOrEmpty(pathValue))
            return false;

        // Skip Swagger UI and API documentation paths
        if (pathValue.StartsWith("/swagger"))
            return true;

        // Skip health check endpoints
        if (pathValue.StartsWith("/health"))
            return true;

        // Skip metrics endpoints
        if (pathValue.StartsWith("/metrics"))
            return true;

        return false;
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
