using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FileManager.Infrastructure.Persistence;

public class FileManagerDbContextFactory : IDesignTimeDbContextFactory<FileManagerDbContext>
{
    public FileManagerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../FileManager.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<FileManagerDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DatabaseSettings:ConnectionString"]
            ?? "Host=localhost;Database=filemanager_db;Username=postgres;Password=postgres";

        var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

        switch (provider.ToLower())
        {
            case "postgresql":
            case "postgres":
            case "npgsql":
                optionsBuilder.UseNpgsql(connectionString,
                    b => b.MigrationsAssembly(typeof(FileManagerDbContext).Assembly.GetName().Name));
                break;
            case "sqlserver":
            case "mssql":
                optionsBuilder.UseSqlServer(connectionString,
                    b => b.MigrationsAssembly(typeof(FileManagerDbContext).Assembly.GetName().Name));
                break;
            case "mysql":
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    b => b.MigrationsAssembly(typeof(FileManagerDbContext).Assembly.GetName().Name));
                break;
            case "sqlite":
                optionsBuilder.UseSqlite(connectionString,
                    b => b.MigrationsAssembly(typeof(FileManagerDbContext).Assembly.GetName().Name));
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider: {provider}");
        }

        return new FileManagerDbContext(optionsBuilder.Options);
    }
}
