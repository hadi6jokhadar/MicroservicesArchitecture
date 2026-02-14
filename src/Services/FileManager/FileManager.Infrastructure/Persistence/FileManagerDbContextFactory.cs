using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using IhsanDev.Shared.Infrastructure.Services.Identity;

namespace FileManager.Infrastructure.Persistence;

public class FileManagerDbContextFactory : IDesignTimeDbContextFactory<FileManagerDbContext>
{
    public FileManagerDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../FileManager.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // Configure DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<FileManagerDbContext>();
        
        // Get connection string with fallbacks
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DatabaseSettings:ConnectionString"]
            ?? "Host=localhost;Database=filemanager_db;Username=postgres;Password=postgres";

        // Determine database provider
        var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

        // Configure provider
        switch (provider.ToLower())
        {
            case "postgresql":
            case "postgres":
            case "npgsql":
                optionsBuilder.UseNpgsql(connectionString);
                break;
            case "sqlserver":
            case "mssql":
                optionsBuilder.UseSqlServer(connectionString);
                break;
            case "mysql":
                var serverVersion = ServerVersion.AutoDetect(connectionString);
                optionsBuilder.UseMySql(connectionString, serverVersion);
                break;
            case "sqlite":
                optionsBuilder.UseSqlite(connectionString);
                break;
            default:
                // Default to PostgreSQL if unknown
                optionsBuilder.UseNpgsql(connectionString);
                break;
        }

        // Return context with dummy services for design-time
        return new FileManagerDbContext(
            optionsBuilder.Options,
            new DesignTimeCurrentUserService());
    }
}

// Dummy implementation for design-time migrations
public class DesignTimeCurrentUserService : ICurrentUserService
{
    public string? UserId => "system";
    public string? Email => "system@localhost";
    public bool IsAuthenticated => true;
    public bool IsSuperAdmin => true;
    public IEnumerable<string> Roles => new[] { "SuperAdmin" };
    public bool HasRole(string role) => true;
}
