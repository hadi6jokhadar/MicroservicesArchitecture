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
/// Generic custom web application factory for integration tests.
/// Supports SQLite in-memory (default) or PostgreSQL for testing.
/// Loads appsettings.Test.json automatically — place it in the API project
/// so it gets copied to the test output directory.
/// </summary>
/// <typeparam name="TProgram">The Program class of the API being tested</typeparam>
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private DbConnection? _connection;

    /// <summary>
    /// Set to true to use PostgreSQL for tests instead of SQLite in-memory.
    /// </summary>
    public bool UsePostgreSQL { get; set; } = false;

    /// <summary>
    /// PostgreSQL connection string. If it still contains "CHANGE_ME" when
    /// ConfigureWebHost runs, the value is replaced by DatabaseSettings:ConnectionString
    /// read from appsettings.Test.json.
    /// </summary>
    public string PostgreSqlConnectionString { get; set; } =
        "Host=localhost;Port=5432;Database=testdb;Username=postgres;Password=CHANGE_ME_DB_PASSWORD;Minimum Pool Size=5;Maximum Pool Size=50;Connection Idle Lifetime=300;Connection Pruning Interval=10;Pooling=true;";

    /// <summary>
    /// Additional configuration values to add during test setup.
    /// Override in derived classes to add service-specific configuration.
    /// </summary>
    protected virtual Dictionary<string, string?> GetTestConfiguration()
    {
        var config = new Dictionary<string, string?>
        {
            ["DatabaseSettings:EnableSensitiveDataLogging"] = "true",
            ["DatabaseSettings:EnableDetailedErrors"] = "true",
            ["Logging:FilePath"] = Path.Combine(Path.GetTempPath(), "MicroservicesTestLogs")
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
    /// Override to configure DbContext for the specific service.
    /// </summary>
    protected virtual void ConfigureDbContext<TDbContext>(IServiceCollection services)
        where TDbContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TDbContext>>();
        services.RemoveAll<TDbContext>();

        if (UsePostgreSQL)
        {
            services.AddDbContext<TDbContext>(options =>
            {
                options.UseNpgsql(PostgreSqlConnectionString);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });
        }
        else
        {
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
            context.Database.EnsureDeleted();
            context.Database.Migrate();
        }
        else
        {
            context.Database.EnsureCreated();
        }
    }

    /// <summary>
    /// Override to seed test data.
    /// </summary>
    protected virtual void SeedTestData<TDbContext>(TDbContext context)
        where TDbContext : DbContext
    {
        // Override in derived class to add test data
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Load test-specific settings (real DB credentials, Redis config).
            // The file is placed in the API project and automatically copied to the
            // test output directory via the project reference.
            config.AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false);

            // Build interim config so we can read values before adding in-memory overrides.
            var builtConfig = config.Build();

            // Replace placeholder connection string with the real one from appsettings.Test.json.
            if (UsePostgreSQL)
            {
                var connStr = builtConfig["DatabaseSettings:ConnectionString"];
                if (!string.IsNullOrEmpty(connStr) && !connStr.Contains("CHANGE_ME"))
                    PostgreSqlConnectionString = connStr;
            }

            // Reject tests early if Redis is required but not reachable.
            var redisEnabled = builtConfig.GetValue<bool>("Redis:Enabled", false);
            if (redisEnabled && !IsRedisAvailable())
            {
                throw new InvalidOperationException(
                    "Redis is required for this service's tests but Redis is not running on localhost:6379. " +
                    "Start Redis before running tests. Use 'node run-all-tests.mjs' which starts Redis automatically.");
            }

            // Add in-memory overrides — these have the highest priority.
            var testConfig = GetTestConfiguration();
            config.AddInMemoryCollection(testConfig);
        });
    }

    /// <summary>
    /// Checks whether a Redis server is reachable on localhost:6379.
    /// </summary>
    private static bool IsRedisAvailable()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);
            var result = socket.BeginConnect("127.0.0.1", 6379, null, null);
            var connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            if (connected) socket.EndConnect(result);
            return connected && socket.Connected;
        }
        catch
        {
            return false;
        }
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
