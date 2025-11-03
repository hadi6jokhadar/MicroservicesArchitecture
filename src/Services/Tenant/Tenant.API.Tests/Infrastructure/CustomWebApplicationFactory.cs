using System.Data.Common;
using Tenant.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tenant.API.Tests.Infrastructure;

/// <summary>
/// Custom web application factory for Tenant API integration tests
/// Inherits from shared testing base for common functionality
/// </summary>
public class CustomWebApplicationFactory : IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        // Set database provider - change this to switch between SQLite and PostgreSQL
        UsePostgreSQL = true;  // Set to true to use PostgreSQL for tests

        // Optional: Customize PostgreSQL connection string
        PostgreSqlConnectionString = "Host=localhost;Port=5432;Database=tenantTestdb;Username=postgres;Password=CHANGE_ME_DB_PASSWORD;Minimum Pool Size=5;Maximum Pool Size=50;Connection Idle Lifetime=300;Connection Pruning Interval=10;Pooling=true;";
    }
    
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();
        
        // Add Tenant-specific configuration
        config["MultiTenancy:Enabled"] = "false"; // Disable multi-tenancy for tests
        
        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        builder.ConfigureServices(services =>
        {
            // Configure the TenantDbContext
            ConfigureDbContext<TenantDbContext>(services);

            // Build service provider and initialize database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            
            InitializeDatabase(dbContext);
            SeedTestData(dbContext);
        });
    }

    protected override void SeedTestData<TDbContext>(TDbContext context)
    {
        // Tenant-specific seed data if needed
        context.SaveChanges();
    }
}
