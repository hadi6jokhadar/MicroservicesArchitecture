using Tenant.Application.Commands.Tenant;
using Tenant.Domain.Entities;
using Tenant.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;
using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace Tenant.API.Tests.Infrastructure;

/// <summary>
/// Base class for Tenant API integration tests
/// Inherits from shared testing base and adds Tenant-specific helpers
/// </summary>
public abstract class IntegrationTestBase : 
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<TenantDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
        // Note: Setting UsePostgreSQL here is too late - factory is already configured
        // To use PostgreSQL, override in CustomWebApplicationFactory constructor instead
    }

    /// <summary>
    /// Create a test tenant with unique tenant ID
    /// </summary>
    protected async Task<TenantSettings> CreateTestTenantAsync(
        string? tenantId = null,
        string? tenantName = null,
        int? userId = null,
        DateTime? startDate = null,
        DateTime? expireDate = null,
        TenantConfiguration? data = null,
        bool isActive = true)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var dataJson = data != null 
                ? System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = false,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                })
                : "{\"jwt\":{\"secret\":\"test-secret-key\",\"issuer\":\"TestTenant\"}}";

            var tenant = new TenantSettings
            {
                TenantId = tenantId ?? GenerateUniqueId("tenant"),
                TenantName = tenantName ?? $"Test Tenant {Guid.NewGuid().ToString()[..8]}",
                UserId = userId ?? GenerateUniqueUserId(),
                StartDate = startDate ?? DateTime.UtcNow,
                ExpireDate = expireDate ?? DateTime.UtcNow.AddYears(1),
                Data = dataJson,
                IsActive = isActive,
                Created = DateTime.UtcNow,
                IsArchived = false
            };

            context.TenantSettings.Add(tenant);
            await context.SaveChangesAsync();
            return tenant;
        });
    }

    /// <summary>
    /// Generate a unique tenant ID for testing
    /// </summary>
    protected string GenerateUniqueTenantId(string prefix = "test-tenant")
    {
        return $"{prefix}-{Guid.NewGuid().ToString()[..8]}";
    }

    /// <summary>
    /// Thread-safe counter for generating unique user IDs
    /// </summary>
    private static int _userIdCounter = 1000;

    /// <summary>
    /// Generate a unique user ID for testing
    /// </summary>
    protected static int GenerateUniqueUserId()
    {
        return Interlocked.Increment(ref _userIdCounter);
    }

    /// <summary>
    /// Create a tenant via command (integration test)
    /// </summary>
    protected async Task<int> CreateTenantViaCommandAsync(
        string? tenantId = null,
        string? tenantName = null,
        int? userId = null,
        DateTime? startDate = null,
        DateTime? expireDate = null,
        TenantConfiguration? data = null)
    {
        var command = new CreateTenantCommand(
            TenantId: tenantId ?? GenerateUniqueTenantId(),
            TenantName: tenantName ?? "Test Tenant",
            UserId: userId ?? GenerateUniqueUserId(),
            StartDate: startDate ?? DateTime.UtcNow,
            ExpireDate: expireDate ?? DateTime.UtcNow.AddYears(1),
            Data: data ?? CreateDefaultTenantConfiguration()
        );

        var result = await SendAsync(command);
        return result.Id;
    }

    /// <summary>
    /// Create default tenant configuration for testing
    /// </summary>
    protected static TenantConfiguration CreateDefaultTenantConfiguration()
    {
        return new TenantConfiguration
        {
            Jwt = new JwtSettings
            {
                Secret = "test-secret-key-minimum-32-characters",
                Issuer = "TestTenant",
                Audience = "TestApp",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7
            },
            DatabaseSettings = new DatabaseSettings
            {
                Provider = "PostgreSql",
                ConnectionString = "Host=localhost;Database=test_db;Username=test;Password=test"
            },
            Cors = new CorsSettings
            {
                AllowedOrigins = new[] { "http://localhost:3000" }
            },
            Otp = new OtpSettings
            {
                CodeLength = 6,
                ExpirationSeconds = 300,
                MaxAttempts = 3,
                LockoutMinutes = 15,
                ResendCooldownSeconds = 60,
                UseAlphanumeric = false
            }
        };
    }

    /// <summary>
    /// Deserialize tenant data JSON string to TenantConfiguration object
    /// </summary>
    protected static TenantConfiguration? DeserializeTenantData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return CreateDefaultTenantConfiguration();

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            return System.Text.Json.JsonSerializer.Deserialize<TenantConfiguration>(dataJson, options);
        }
        catch
        {
            return CreateDefaultTenantConfiguration();
        }
    }

    /// <summary>
    /// Generate unique ID with prefix
    /// </summary>
    private string GenerateUniqueId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid().ToString()[..8]}";
    }
}
