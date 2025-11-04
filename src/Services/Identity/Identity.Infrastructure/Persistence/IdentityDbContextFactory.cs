using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating IdentityDbContext during migrations.
/// This bypasses multi-tenancy requirements by using appsettings.json directly.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Identity.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration["DatabaseSettings:ConnectionString"] 
            ?? throw new InvalidOperationException("Database connection string not found in appsettings.json");
        
        var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();

        switch (provider)
        {
            case "PostgreSql":
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.GetName().Name);
                });
                break;

            case "Sqlite":
                optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.GetName().Name);
                });
                break;

            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported");
        }

        // Create context without tenant context (for migrations only)
        return new IdentityDbContext(optionsBuilder.Options);
    }
}
