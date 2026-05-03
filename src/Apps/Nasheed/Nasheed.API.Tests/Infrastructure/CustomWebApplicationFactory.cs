using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Nasheed.Application.Interfaces;
using Nasheed.Infrastructure.Persistence;
using Nasheed.Infrastructure.Services;
using Nasheed.Infrastructure.Workers;

namespace Nasheed.API.Tests.Infrastructure;

/// <summary>
/// Custom web application factory for Nasheed API integration tests.
/// Inherits from shared testing base and overrides infrastructure services
/// that require external dependencies (TenantService, AI API, Redis).
/// </summary>
public class CustomWebApplicationFactory : IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        // CRITICAL: Configure Npgsql to handle all DateTime as UTC
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

        // Use PostgreSQL so FK constraints and EF migrations behave like production
        UsePostgreSQL = true;
        PostgreSqlConnectionString = "Host=localhost;Port=5432;Database=nasheed_testdb;Username=postgres;Password=CHANGE_ME_DB_PASSWORD;" +
                                     "Minimum Pool Size=2;Maximum Pool Size=20;Connection Idle Lifetime=300;Pooling=true;";
    }

    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();

        // JWT — required by authentication middleware
        config["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestNasheedService";
        config["Jwt:Audience"] = "TestMicroservicesApp";
        config["Jwt:AccessTokenExpirationMinutes"] = "60";
        config["Jwt:RefreshTokenExpirationDays"] = "7";

        // Disable multi-tenancy so NasheedDbContext uses direct connection
        config["MultiTenancy:Enabled"] = "false";

        // Disable Redis to avoid dependency
        config["Redis:Enabled"] = "false";

        // Disable service communication (ServiceAuthHandler / shared-secret checks)
        config["ServiceCommunication:Enabled"] = "false";

        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // ── Remove background workers that require external services ──────────
            // NasheedTenantLoaderService calls TenantService (not available in tests)
            // NasheedIngestionWorker calls AI API (not available in tests)
            RemoveHostedService<NasheedTenantLoaderService>(services);
            RemoveHostedService<NasheedIngestionWorker>(services);

            // ── Replace IAiApiClient with a no-op mock ────────────────────────────
            // Tests that exercise artist/song CRUD do not call AI; tests that need
            // AI behaviour should set up the mock per-test via the factory property.
            services.RemoveAll<IAiApiClient>();
            var aiMock = new Mock<IAiApiClient>();
            aiMock.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<IReadOnlyList<int>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("mock-ai-response");
            aiMock.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });
            services.AddSingleton(aiMock.Object);

            // ── Replace INasheedTenantCache with a no-op singleton ────────────────
            // The real cache waits for TenantLoaderService which we removed.
            services.RemoveAll<INasheedTenantCache>();
            var cacheMock = new Mock<INasheedTenantCache>();
            cacheMock.Setup(x => x.IsReady).Returns(false);
            services.AddSingleton(cacheMock.Object);

            // ── Replace NasheedDbContext with the test database ───────────────────
            ConfigureDbContext<NasheedDbContext>(services);

            // Initialise database schema and run any pending migrations
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NasheedDbContext>();
            InitializeDatabase(dbContext);
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static void RemoveHostedService<TService>(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(TService));
        if (descriptor != null)
            services.Remove(descriptor);
    }
}
