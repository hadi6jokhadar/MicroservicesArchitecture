namespace IhsanDev.Shared.Kernel.Interfaces.Database;

/// <summary>
/// Service interface for handling database creation and migrations
/// </summary>
public interface IDatabaseMigrationService
{
    /// <summary>
    /// Ensures the database exists and applies any pending migrations
    /// </summary>
    /// <param name="context">The DbContext to migrate</param>
    /// <param name="tenantId">Optional tenant ID for logging purposes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if database was created/migrated successfully</returns>
    Task<bool> EnsureDatabaseExistsAsync(
        object context, 
        string? tenantId = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the database exists without creating it
    /// </summary>
    Task<bool> DatabaseExistsAsync(
        object context, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of pending migrations for a database
    /// </summary>
    Task<IEnumerable<string>> GetPendingMigrationsAsync(
        object context, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of applied migrations for a database
    /// </summary>
    Task<IEnumerable<string>> GetAppliedMigrationsAsync(
        object context, 
        CancellationToken cancellationToken = default);
}
