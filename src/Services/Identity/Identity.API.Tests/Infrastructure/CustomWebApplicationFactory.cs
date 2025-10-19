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
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();
        
        // Add Identity-specific configuration
        config["Jwt:Key"] = "test-super-secret-jwt-key-minimum-32-characters-long";
        config["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestIdentityService";
        config["Jwt:Audience"] = "TestMicroservicesApp";
        config["Jwt:ExpiryInMinutes"] = "60";
        config["Jwt:RefreshTokenExpiryInDays"] = "7";
        
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
