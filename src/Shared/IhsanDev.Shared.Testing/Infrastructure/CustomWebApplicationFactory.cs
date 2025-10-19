using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IhsanDev.Shared.Testing.Infrastructure;

/// <summary>
/// Generic custom web application factory for integration tests
/// Supports SQLite in-memory (default) or PostgreSQL for testing
/// </summary>
/// <typeparam name="TProgram">The Program class of the API being tested</typeparam>
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private DbConnection? _connection;
    
    /// <summary>
    /// Set to true to use PostgreSQL for tests instead of SQLite in-memory
    /// </summary>
    public bool UsePostgreSQL { get; set; } = false;

    /// <summary>
    /// PostgreSQL connection string (only used if UsePostgreSQL is true)
    /// </summary>
    public string PostgreSqlConnectionString { get; set; } = 
        "Host=localhost;Port=5432;Database=testdb;Username=postgres;Password=postgres;";

    /// <summary>
    /// Additional configuration values to add during test setup
    /// Override this to add service-specific configuration
    /// </summary>
    protected virtual Dictionary<string, string?> GetTestConfiguration()
    {
        var config = new Dictionary<string, string?>
        {
            ["DatabaseSettings:EnableSensitiveDataLogging"] = "true",
            ["DatabaseSettings:EnableDetailedErrors"] = "true"
        };

        if (UsePostgreSQL)
        {
            config["DatabaseSettings:Provider"] = "PostgreSql";
            config["DatabaseSettings:ConnectionString"] = PostgreSqlConnectionString;
        }
        else
        {
            config["DatabaseSettings:Provider"] = "Sqlite";
            config["DatabaseSettings:ConnectionString"] = "DataSource=:memory:";
        }

        return config;
    }

    /// <summary>
    /// Override to configure DbContext for the specific service
    /// </summary>
    protected virtual void ConfigureDbContext<TDbContext>(IServiceCollection services)
        where TDbContext : DbContext
    {
        // Remove the existing DbContext registration
        services.RemoveAll<DbContextOptions<TDbContext>>();
        services.RemoveAll<TDbContext>();

        if (UsePostgreSQL)
        {
            // Use PostgreSQL for tests
            services.AddDbContext<TDbContext>(options =>
            {
                options.UseNpgsql(PostgreSqlConnectionString);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });
        }
        else
        {
            // Use SQLite in-memory (default)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<TDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });
        }
    }

    /// <summary>
    /// Override to initialize database (migrations, seed data, etc.)
    /// </summary>
    protected virtual void InitializeDatabase<TDbContext>(TDbContext context)
        where TDbContext : DbContext
    {
        if (UsePostgreSQL)
        {
            // For PostgreSQL, apply migrations
            context.Database.Migrate();
        }
        else
        {
            // For SQLite in-memory, ensure created
            context.Database.EnsureCreated();
        }
    }

    /// <summary>
    /// Override to seed test data
    /// </summary>
    protected virtual void SeedTestData<TDbContext>(TDbContext context)
        where TDbContext : DbContext
    {
        // Override in derived class to add test data
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing
        builder.UseEnvironment("Testing");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration
            var testConfig = GetTestConfiguration();
            config.AddInMemoryCollection(testConfig);
        });

        // Override in derived class to configure services
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}
