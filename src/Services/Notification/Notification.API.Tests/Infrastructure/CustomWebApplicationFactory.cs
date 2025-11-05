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
        // Set database provider - change this to switch between SQLite and PostgreSQL
        UsePostgreSQL = true;  // Set to true to use PostgreSQL for tests
        
        // Use test database for global queue
        PostgreSqlConnectionString = "Host=localhost;Port=5432;Database=notification_global_test;Username=postgres;Password=CHANGE_ME_DB_PASSWORD;Minimum Pool Size=5;Maximum Pool Size=50;Connection Idle Lifetime=300;Connection Pruning Interval=10;Pooling=true;";
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
        
        // Disable multi-tenancy for testing (simplifies test setup)
        // When enabled, tenant middleware requires x-tenant-id header and fetches tenant config
        config["MultiTenancy:Enabled"] = "false";
        config["MultiTenancy:JwtMode"] = "Shared";
        
        // Notification processing configuration
        config["NotificationProcessing:WaitableBatchIntervalSeconds"] = "5";
        config["NotificationProcessing:MaxRetryCount"] = "2";
        
        // SignalR configuration
        config["SignalR:EnableDetailedErrors"] = "true";
        config["SignalR:ClientTimeoutInterval"] = "00:02:00";
        config["SignalR:KeepAliveInterval"] = "00:00:30";
        
        // Identity Service configuration (for device token management)
        config["IdentityService:BaseUrl"] = "https://localhost:5001";
        
        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        builder.ConfigureServices(services =>
        {
            // ============================================
            // Configure Global Notification Queue Database
            // ============================================
            // This database stores the notification queue (NotificationQueueItem)
            // It's shared across all tenants and uses appsettings.json connection
            // Migrations folder: Migrations/Global
            services.RemoveAll<DbContextOptions<NotificationDbContext>>();
            services.RemoveAll<NotificationDbContext>();

            if (UsePostgreSQL)
            {
                services.AddDbContext<NotificationDbContext>(options =>
                {
                    options.UseNpgsql(PostgreSqlConnectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.GetName().Name);
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    });
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            }
            else
            {
                services.AddDbContext<NotificationDbContext>(options =>
                {
                    options.UseSqlite("DataSource=:memory:");
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            }

            // ============================================
            // Configure Tenant-Specific Notification Database
            // ============================================
            // This database stores notification history (Notification entity)
            // In production: uses tenant-specific connection from TenantContext
            // In testing: uses the same test database (multi-tenancy disabled)
            // Migrations folder: Migrations/Tenant
            services.RemoveAll<DbContextOptions<TenantNotificationDbContext>>();
            services.RemoveAll<TenantNotificationDbContext>();

            if (UsePostgreSQL)
            {
                services.AddDbContext<TenantNotificationDbContext>(options =>
                {
                    options.UseNpgsql(PostgreSqlConnectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(TenantNotificationDbContext).Assembly.GetName().Name);
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Tenant", "public");
                    });
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            }
            else
            {
                services.AddDbContext<TenantNotificationDbContext>(options =>
                {
                    options.UseSqlite("DataSource=:memory:");
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            }

            // Build service provider - database migrations already applied manually
            // DO NOT call EnsureDeleted() or EnsureCreated() as it would drop test data
            // The test databases should be set up once before running tests
        });
    }

    protected override void SeedTestData<TDbContext>(TDbContext context)
    {
        // Notification-specific seed data if needed
        // For example, you could seed some test queue items or notifications here
        context.SaveChanges();
    }
}
