namespace Backup.Infrastructure.Services;

/// <summary>
/// Runs the PostgreSQL <c>pg_dump</c>/<c>pg_restore</c> command-line tools against a given
/// connection string. Internal to Infrastructure — consumed only by <c>Backup.Infrastructure.Jobs</c>.
/// </summary>
public interface IPgToolRunner
{
    /// <summary>
    /// Dumps the database identified by <paramref name="connectionString"/> to
    /// <paramref name="outputFilePath"/> (custom format) and returns the SHA-256 checksum (hex)
    /// of the produced file.
    /// </summary>
    Task<string> DumpAsync(string connectionString, string outputFilePath, CancellationToken ct);

    /// <summary>Restores <paramref name="inputFilePath"/> into the database identified by <paramref name="connectionString"/>.</summary>
    Task RestoreAsync(string connectionString, string inputFilePath, CancellationToken ct);
}
