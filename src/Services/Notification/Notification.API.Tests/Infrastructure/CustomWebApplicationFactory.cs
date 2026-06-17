using System.Data.Common;
using Notification.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Notification.API.Tests.Infrastructure;

/// <summary>
/// Custom web application factory for Notification API integration tests
/// Inherits from shared testing base for common functionality
/// 
/// IMPORTANT: Notification Service uses TWO databases:
/// 1. NotificationDbContext - Global queue database (shared across all tenants)
/// 2. TenantNotificationDbContext - Tenant-specific notification history database
/// </summary>
public class CustomWebApplicationFactory : IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        // Use PostgreSQL — connection string is read from Notification.API/appsettings.Test.json
        UsePostgreSQL = true;
    }
    
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();
        
        // Add Notification-specific configuration
        config["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestNotificationService";
        config["Jwt:Audience"] = "TestMicroservicesApp";
        config["Jwt:AccessTokenExpirationMinutes"] = "60";
        config["Jwt:RefreshTokenExpirationDays"] = "7";
        
        // Disable multi-tenancy for testing
        config["MultiTenancy:Enabled"] = "false";
        
        // Notification processing configuration
        config["NotificationProcessing:WaitableBatchIntervalSeconds"] = "5";
        config["NotificationProcessing:MaxRetryCount"] = "2";
        
        // SignalR configuration
        config["SignalR:EnableDetailedErrors"] = "true";
        
        // Identity Service configuration
        config["IdentityService:BaseUrl"] = "https://localhost:5001";
        
        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        builder.ConfigureServices(services =>
        {
            // Disable background services in tests (they query DB before it's created)
            var hostedServices = services.Where(d => 
                d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
                d.ImplementationType?.Namespace?.StartsWith("Notification.API") == true).ToList();
            foreach (var service in hostedServices)
                services.Remove(service);
            
            // Remove existing DbContext registrations
            services.RemoveAll<DbContextOptions<NotificationDbContext>>();
            services.RemoveAll<NotificationDbContext>();
            services.RemoveAll<DbContextOptions<TenantNotificationDbContext>>();
            services.RemoveAll<TenantNotificationDbContext>();

            // Configure Global Queue Database with proper migration assembly
            if (UsePostgreSQL)
            {
                services.AddDbContext<NotificationDbContext>(options =>
                    options.UseNpgsql(PostgreSqlConnectionString, npgsql =>
                        npgsql.MigrationsAssembly(typeof(NotificationDbContext).Assembly.GetName().Name))
                           .EnableSensitiveDataLogging()
                           .EnableDetailedErrors());
                
                services.AddDbContext<TenantNotificationDbContext>(options =>
                    options.UseNpgsql(PostgreSqlConnectionString, npgsql =>
                        npgsql.MigrationsAssembly(typeof(TenantNotificationDbContext).Assembly.GetName().Name))
                           .EnableSensitiveDataLogging()
                           .EnableDetailedErrors());
            }
            else
            {
                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                
                services.AddDbContext<NotificationDbContext>(options =>
                    options.UseSqlite(connection).EnableSensitiveDataLogging().EnableDetailedErrors());
                
                services.AddDbContext<TenantNotificationDbContext>(options =>
                    options.UseSqlite(connection).EnableSensitiveDataLogging().EnableDetailedErrors());
            }

            // Build service provider and initialize databases
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            
            var globalDb = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var tenantDb = scope.ServiceProvider.GetRequiredService<TenantNotificationDbContext>();
            
            // CRITICAL: Both contexts share the same database but have different tables
            // NotificationDbContext -> NotificationQueue table (global queue)
            // TenantNotificationDbContext -> Notifications table (tenant-specific history)
            
            if (UsePostgreSQL)
            {
                // For PostgreSQL, ensure database exists and apply migrations
                globalDb.Database.Migrate();  // Creates/updates NotificationQueue table
                tenantDb.Database.Migrate();  // Creates/updates Notifications table
                
                // Clean existing data for fresh test state
                try
                {
                    globalDb.Database.ExecuteSqlRaw("TRUNCATE TABLE \"NotificationQueue\" RESTART IDENTITY CASCADE");
                }
                catch
                {
                    // Ignore if table doesn't exist yet (first run)
                }
                
                try
                {
                    tenantDb.Database.ExecuteSqlRaw("TRUNCATE TABLE \"Notifications\" RESTART IDENTITY CASCADE");
                }
                catch
                {
                    // Ignore if table doesn't exist yet (first run)
                }
            }
            else
            {
                // For SQLite in-memory, use EnsureCreated (migrations don't work with in-memory)
                globalDb.Database.EnsureDeleted();
                globalDb.Database.EnsureCreated();  // Creates NotificationQueue table
                tenantDb.Database.EnsureCreated();   // Creates Notifications table
            }
            
            SeedTestData(globalDb);
            SeedTestData(tenantDb);
        });
    }

    protected override void SeedTestData<TDbContext>(TDbContext context)
    {
        // Notification-specific seed data if needed
        context.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up test database when factory is disposed (after each test class)
            try
            {
                using var scope = Services.CreateScope();
                var globalDb = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                var tenantDb = scope.ServiceProvider.GetRequiredService<TenantNotificationDbContext>();
                
                if (UsePostgreSQL)
                {
                    // For PostgreSQL, truncate tables to clean up data but keep schema
                    globalDb.Database.ExecuteSqlRaw("TRUNCATE TABLE \"NotificationQueue\" RESTART IDENTITY CASCADE");
                    tenantDb.Database.ExecuteSqlRaw("TRUNCATE TABLE \"Notifications\" RESTART IDENTITY CASCADE");
                }
            }
            catch
            {
                // Ignore cleanup errors during disposal
            }
        }
        
        base.Dispose(disposing);
    }
}
