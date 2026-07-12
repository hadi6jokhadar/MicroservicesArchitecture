using Npgsql;

namespace IhsanDev.Shared.Infrastructure.Persistence;

public static class NpgsqlConnectionStringHelper
{
    /// <summary>
    /// Caps the connection pool size on a dynamically-resolved (e.g. per-tenant) connection
    /// string. Npgsql opens a separate physical connection pool per unique connection string,
    /// so without this, N tenants x M services multiplies into an ungoverned number of pools —
    /// each defaulting to Npgsql's own 100-connection ceiling if the stored string omits one.
    /// Overrides whatever "Maximum Pool Size" the tenant's stored string specifies.
    /// </summary>
    public static string WithBoundedPoolSize(string connectionString, int maxPoolSize)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = maxPoolSize
        };

        // Npgsql throws at connection-open time if MinPoolSize > MaxPoolSize. A tenant's stored
        // string could specify a MinPoolSize higher than our cap — clamp it down rather than
        // let that surface as a confusing runtime failure instead of a config problem.
        if (builder.MinPoolSize > maxPoolSize)
        {
            builder.MinPoolSize = maxPoolSize;
        }

        return builder.ConnectionString;
    }
}
