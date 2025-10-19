using Tenant.Application.Commands.Tenant;
using Tenant.Domain.Entities;
using Tenant.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;

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
        string? data = null,
        bool isActive = true)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var tenant = new TenantSettings
            {
                TenantId = tenantId ?? GenerateUniqueId("tenant"),
                TenantName = tenantName ?? $"Test Tenant {Guid.NewGuid().ToString()[..8]}",
                UserId = userId ?? GenerateUniqueUserId(),
                StartDate = startDate ?? DateTime.UtcNow,
                ExpireDate = expireDate ?? DateTime.UtcNow.AddYears(1),
                Data = data ?? "{\"Jwt\":{\"Secret\":\"test-secret-key\",\"Issuer\":\"TestTenant\"}}",
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
        string? data = null)
    {
        var command = new CreateTenantCommand(
            TenantId: tenantId ?? GenerateUniqueTenantId(),
            TenantName: tenantName ?? "Test Tenant",
            UserId: userId ?? GenerateUniqueUserId(),
            StartDate: startDate ?? DateTime.UtcNow,
            ExpireDate: expireDate ?? DateTime.UtcNow.AddYears(1),
            Data: data ?? "{\"Jwt\":{\"Secret\":\"test-secret\"}}"
        );

        var result = await SendAsync(command);
        return result.Id;
    }

    /// <summary>
    /// Generate unique ID with prefix
    /// </summary>
    private string GenerateUniqueId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid().ToString()[..8]}";
    }
}
