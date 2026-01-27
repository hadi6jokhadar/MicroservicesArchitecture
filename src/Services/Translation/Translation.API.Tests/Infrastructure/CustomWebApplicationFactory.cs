using System.Data.Common;
using Translation.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Translation.API.Tests.Infrastructure;

/// <summary>
/// Custom web application factory for Translation API integration tests
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
        // PostgreSqlConnectionString = "Host=localhost;Port=5432;Database=translation_test;Username=postgres;Password=postgres;";
    }
    
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();
        
        // Add Translation-specific configuration
        config["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestTranslationService";
        config["Jwt:Audience"] = "TestMicroservicesApp";
        config["Jwt:AccessTokenExpirationMinutes"] = "60";
        config["Jwt:RefreshTokenExpirationDays"] = "7";
        
        // Disable multi-tenancy for testing (Translation uses global database)
        config["MultiTenancy:Enabled"] = "false";
        
        // Configure localization for tests (use English)
        config["Localization:DefaultCulture"] = "en";
        config["Localization:SupportedCultures"] = "en,ar";
        
        // Disable rate limiting for tests
        config["RateLimiting:Enabled"] = "false";
        
        // Disable Redis for tests (use in-memory cache instead)
        config["Redis:Enabled"] = "false";
        
        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        builder.ConfigureServices(services =>
        {
            // CRITICAL: Remove ALL IDistributedCache registrations (including Redis)
            // Use RemoveAll which handles the type correctly
            services.RemoveAll(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache));
            services.RemoveAll(typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache));
            
            // Add fresh memory cache for each test (scoped to avoid cross-test pollution)
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            
            // Configure the TranslationDbContext
            ConfigureDbContext<TranslationDbContext>(services);

            // Build service provider and initialize database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TranslationDbContext>();
            
            InitializeDatabase(dbContext);
            SeedTestData(dbContext);
        });
    }

    protected override void SeedTestData<TDbContext>(TDbContext context)
    {
        // Seed initial test translation keys if needed
        if (context is TranslationDbContext translationContext)
        {
            // Check if test data already exists
            if (!translationContext.TranslationKeys.Any())
            {
                var testKey1 = Translation.Domain.Entities.TranslationKey.Create(
                    "test_key_1",
                    "general",
                    "Test translation key 1"
                );

                var testKey2 = Translation.Domain.Entities.TranslationKey.Create(
                    "test_key_2",
                    "errors",
                    "Test translation key 2"
                );

                translationContext.TranslationKeys.AddRange(testKey1, testKey2);
                translationContext.SaveChanges();

                // Add some translation values
                var testValue1 = Translation.Domain.Entities.TranslationValue.CreateGlobal(
                    testKey1.Id,
                    "en",
                    "Test Key 1 English"
                );

                var testValue2 = Translation.Domain.Entities.TranslationValue.CreateGlobal(
                    testKey1.Id,
                    "ar",
                    "مفتاح الاختبار 1"
                );

                var testValue3 = Translation.Domain.Entities.TranslationValue.CreateGlobal(
                    testKey2.Id,
                    "en",
                    "Test Key 2 English"
                );

                translationContext.TranslationValues.AddRange(testValue1, testValue2, testValue3);
            }
        }
        
        context.SaveChanges();
    }
}
