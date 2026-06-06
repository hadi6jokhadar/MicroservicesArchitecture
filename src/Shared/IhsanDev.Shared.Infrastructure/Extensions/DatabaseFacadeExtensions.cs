using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace IhsanDev.Shared.Infrastructure.Extensions;

public static class DatabaseFacadeExtensions
{
    /// <summary>
    /// Applies pending migrations with automatic recovery for the "relation already exists" case.
    /// When tables exist but are not tracked in __EFMigrationsHistory (e.g. database restored
    /// without migration history, or previously created via EnsureCreated), pending migrations
    /// are marked as applied without re-executing their SQL.
    /// </summary>
    public static async Task MigrateWithRecoveryAsync(
        this DatabaseFacade database,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await database.MigrateAsync(cancellationToken);
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "42P07")
        {
            // 42P07 = relation already exists. Tables exist but __EFMigrationsHistory has no
            // record of them. Mark pending migrations as applied to bring history in sync.
            logger?.LogWarning(
                "Tables already exist — marking pending migrations as applied without re-running SQL");
            await MarkPendingMigrationsAsAppliedAsync(database, logger, cancellationToken);
        }
    }

    private static async Task MarkPendingMigrationsAsAppliedAsync(
        DatabaseFacade database,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        var pending = (await database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0) return;

        // __EFMigrationsHistory may not exist when the DB was created via EnsureCreated
        await database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" character varying(150) NOT NULL,
                ""ProductVersion"" character varying(32) NOT NULL,
                CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
            )");

        var efVersion = typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "9.0.0";

        foreach (var migration in pending)
        {
            await database.ExecuteSqlRawAsync(
                @"INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                  VALUES ({0}, {1})
                  ON CONFLICT (""MigrationId"") DO NOTHING",
                migration, efVersion);

            logger?.LogInformation("Migration '{Migration}' marked as applied", migration);
        }

        logger?.LogWarning(
            "Marked {Count} pending migration(s) as applied without executing SQL — " +
            "the database schema already matched. Verify the schema matches the current model.",
            pending.Count);
    }
}
