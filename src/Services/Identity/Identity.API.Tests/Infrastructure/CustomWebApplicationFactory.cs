using System.Data.Common;
using Identity.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.API.Tests.Infrastructure;

/// <summary>
/// Custom web application factory for Identity API integration tests
/// Inherits from shared testing base for common functionality
/// </summary>
public class CustomWebApplicationFactory : IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        // CRITICAL: Configure Npgsql to handle all DateTime as UTC
        // This MUST be set before any Npgsql operations to ensure proper timezone handling
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
        
        // Set database provider - change this to switch between SQLite and PostgreSQL
        UsePostgreSQL = true;  // Set to true to use PostgreSQL for tests
        
        // Optional: Customize PostgreSQL connection string
        // PostgreSqlConnectionString = "Host=localhost;Port=5432;Database=identity_test;Username=postgres;Password=postgres;";
    }
    
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();
        
        // Add Identity-specific configuration
        config["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestIdentityService";
        config["Jwt:Audience"] = "TestMicroservicesApp";
        config["Jwt:AccessTokenExpirationMinutes"] = "60";
        config["Jwt:RefreshTokenExpirationDays"] = "7";
        
        // Disable multi-tenancy for testing
        config["MultiTenancy:Enabled"] = "false";
        
        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        builder.ConfigureServices(services =>
        {
            // Configure the IdentityDbContext
            ConfigureDbContext<IdentityDbContext>(services);

            // Build service provider and initialize database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            
            InitializeDatabase(dbContext);
            SeedTestData(dbContext);
        });
    }

    protected override void SeedTestData<TDbContext>(TDbContext context)
    {
        // Identity-specific seed data if needed
        context.SaveChanges();
    }
}
