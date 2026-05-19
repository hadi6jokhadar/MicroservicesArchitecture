using Category.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Category.API.Tests.Infrastructure;

/// <summary>
/// Custom web application factory for Category API integration tests.
/// Inherits from the shared testing base and overrides DB + seed configuration.
/// </summary>
public class CustomWebApplicationFactory : IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        // CRITICAL: Configure Npgsql to handle all DateTime as UTC.
        // Must be set before any Npgsql operations.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

        // Use PostgreSQL so that JSONB columns (NameTranslations, Attributes) work correctly.
        UsePostgreSQL = true;
    }

    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();

        // JWT settings required by the shared auth middleware
        config["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestCategoryService";
        config["Jwt:Audience"] = "TestMicroservicesApp";
        config["Jwt:AccessTokenExpirationMinutes"] = "60";
        config["Jwt:RefreshTokenExpirationDays"] = "7";

        // Disable multi-tenancy so tests use the global database
        config["MultiTenancy:Enabled"] = "false";

        // Localization
        config["Localization:DefaultCulture"] = "en";
        config["Localization:SupportedCultures"] = "en,ar";

        // Disable rate limiting for tests
        config["RateLimiting:Enabled"] = "false";

        // Disable Redis (Category API references StackExchange.Redis via the shared infra)
        config["Redis:Enabled"] = "false";

        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Remove Redis / IDistributedCache registrations so tests are self-contained
            services.RemoveAll(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache));
            services.RemoveAll(typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache));
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();

            // Register in-memory ICacheService (multi-tenancy is disabled, so it won't be registered by default)
            services.AddInMemoryCache();

            // Replace the real CategoryDbContext with the test-configured one
            ConfigureDbContext<CategoryDbContext>(services);

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CategoryDbContext>();

            InitializeDatabase(dbContext);
            SeedTestData(dbContext);
        });
    }
}
