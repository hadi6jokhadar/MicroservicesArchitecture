using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Kernel.Interfaces.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Services.Database;

/// <summary>
/// Service to handle automatic database creation and migration for tenant databases
/// </summary>
public class DatabaseMigrationService : IDatabaseMigrationService
{
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(ILogger<DatabaseMigrationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Ensures the database exists and applies any pending migrations
    /// </summary>
    /// <param name="context">The DbContext to migrate</param>
    /// <param name="tenantId">Optional tenant ID for logging purposes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if database was created/migrated successfully</returns>
    public async Task<bool> EnsureDatabaseExistsAsync(
        object contextObj, 
        string? tenantId = null, 
        CancellationToken cancellationToken = default)
    {
        if (contextObj is not DbContext context)
        {
            throw new ArgumentException("Context must be a DbContext", nameof(contextObj));
        }

        try
        {
            var contextName = context.GetType().Name;
            var tenantInfo = string.IsNullOrEmpty(tenantId) ? "default database" : $"tenant '{tenantId}'";

            // Check if database can be connected
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                _logger.LogInformation(
                    "Database for {TenantInfo} does not exist. Creating and migrating... (Context: {ContextName})",
                    tenantInfo, contextName);

                // Create database and apply migrations
                await context.Database.MigrateWithRecoveryAsync(_logger, cancellationToken);

                _logger.LogInformation(
                    "Database for {TenantInfo} created and migrated successfully (Context: {ContextName})",
                    tenantInfo, contextName);

                return true;
            }

            // Database exists, check for pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            
            if (pendingMigrations.Any())
            {
                _logger.LogInformation(
                    "Found {Count} pending migration(s) for {TenantInfo}. Applying... (Context: {ContextName})",
                    pendingMigrations.Count(), tenantInfo, contextName);

                await context.Database.MigrateWithRecoveryAsync(_logger, cancellationToken);

                _logger.LogInformation(
                    "Migrations applied successfully for {TenantInfo} (Context: {ContextName})",
                    tenantInfo, contextName);

                return true;
            }

            _logger.LogDebug(
                "Database for {TenantInfo} is up to date (Context: {ContextName})",
                tenantInfo, contextName);

            return true;
        }
        catch (Exception ex)
        {
            var tenantInfo = string.IsNullOrEmpty(tenantId) ? "default database" : $"tenant '{tenantId}'";
            _logger.LogError(ex,
                "Failed to ensure database exists for {TenantInfo}. Error: {Message}",
                tenantInfo, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Checks if the database exists without creating it
    /// </summary>
    public async Task<bool> DatabaseExistsAsync(
        object contextObj, 
        CancellationToken cancellationToken = default)
    {
        if (contextObj is not DbContext context)
        {
            throw new ArgumentException("Context must be a DbContext", nameof(contextObj));
        }

        try
        {
            return await context.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if database exists: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets the list of pending migrations for a database
    /// </summary>
    public async Task<IEnumerable<string>> GetPendingMigrationsAsync(
        object contextObj, 
        CancellationToken cancellationToken = default)
    {
        if (contextObj is not DbContext context)
        {
            throw new ArgumentException("Context must be a DbContext", nameof(contextObj));
        }

        try
        {
            return await context.Database.GetPendingMigrationsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending migrations: {Message}", ex.Message);
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Gets the list of applied migrations for a database
    /// </summary>
    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync(
        object contextObj, 
        CancellationToken cancellationToken = default)
    {
        if (contextObj is not DbContext context)
        {
            throw new ArgumentException("Context must be a DbContext", nameof(contextObj));
        }

        try
        {
            return await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting applied migrations: {Message}", ex.Message);
            return Enumerable.Empty<string>();
        }
    }
}
