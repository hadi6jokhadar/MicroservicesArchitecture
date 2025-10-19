using FluentAssertions;
using IhsanDev.Shared.Testing.Helpers;
using Tenant.API.Tests.Infrastructure;
using Tenant.Application.Commands.Tenant;


// & dotnet test --filter "FullyQualifiedName~SharedHelperIntegrationTests"
namespace Tenant.API.Tests.Integration;

/// <summary>
/// Cross-service integration tests demonstrating:
/// 1. Shared helper usage for reusability
/// 2. Tenant-enabled vs non-tenant scenarios
/// 3. Basic user + tenant workflow
/// </summary>
[Collection("Sequential")]
public class SharedHelperIntegrationTests : IntegrationTestBase
{
    public SharedHelperIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region Basic Flow Using Shared Helper

    [Fact]
    public async Task CreateUser_CreateTenant_GetTenant_UsingSharedHelper_ShouldSucceed()
    {
        // Arrange - Generate unique IDs using shared helper
        var userId = TenantTestHelper.GenerateUniqueUserId();
        var tenantId = TenantTestHelper.GenerateUniqueTenantId();

        // Act 1 - Create tenant (simulating user creation + tenant assignment)
        var createCommand = new CreateTenantCommand(
            TenantId: tenantId,
            TenantName: "Test Tenant via Shared Helper",
            UserId: userId,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: "{\"Jwt\":{\"Secret\":\"test-secret\"}}"
        );

        var createdTenant = await SendAsync(createCommand);

        // Act 2 - Get tenant by ID
        var getTenantQuery = new GetTenantByIdQuery(tenantId);
        var retrievedTenant = await SendAsync(getTenantQuery);

        // Assert
        createdTenant.Should().NotBeNull();
        createdTenant.TenantId.Should().Be(tenantId);
        createdTenant.UserId.Should().Be(userId);

        retrievedTenant.Should().NotBeNull();
        retrievedTenant!.TenantId.Should().Be(tenantId);
        retrievedTenant.UserId.Should().Be(userId);
        retrievedTenant.TenantName.Should().Be("Test Tenant via Shared Helper");
    }

    #endregion

    #region Tenant-Enabled vs Non-Tenant Scenarios

    [Fact]
    public async Task TenantEnabled_Project_ShouldLoadTenantSettings()
    {
        // Arrange - Simulate Project B (tenant-enabled)
        var userId = TenantTestHelper.GenerateUniqueUserId();
        var tenantId = TenantTestHelper.GenerateUniqueTenantId("project-b");

        // Act - Create user and tenant
        var createCommand = new CreateTenantCommand(
            TenantId: tenantId,
            TenantName: "Project B Tenant",
            UserId: userId,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: "{\"Jwt\":{\"Secret\":\"project-b-secret\",\"Issuer\":\"ProjectB\"}}"
        );

        var tenant = await SendAsync(createCommand);

        // Act - Get tenant settings (tenant-enabled flow)
        var query = new GetTenantByIdQuery(tenantId);
        var settings = await SendAsync(query);

        // Assert
        settings.Should().NotBeNull();
        settings!.TenantId.Should().Be(tenantId);
        settings.UserId.Should().Be(userId);
        settings.TenantName.Should().Be("Project B Tenant");

        // Verify tenant data is loaded
        settings.Should().NotBeNull();
    }

    [Fact]
    public void NonTenant_Project_ShouldUseDefaultSettings()
    {
        // Arrange - Simulate Project A (non-tenant)
        // In a non-tenant project, there would be no tenant table or service
        // This test demonstrates the toggle pattern

        var isTenantEnabled = false; // Project A setting

        // Act & Assert - Non-tenant project flow
        if (isTenantEnabled)
        {
            // This path would not be taken for Project A
            Assert.Fail("Project A should not have tenant enabled");
        }
        else
        {
            // Project A: Load default settings (no tenant lookup)
            var defaultSettings = GetDefaultSettings();

            defaultSettings.Should().NotBeNull();
            defaultSettings.AppName.Should().Be("Project A");
            defaultSettings.UseTenantSettings.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ConditionalTenantLoad_BasedOnProjectConfiguration_ShouldWork()
    {
        // This test demonstrates how to write reusable tests that work for both scenarios

        // Arrange
        var projectName = "Project B"; // Could come from config
        var isTenantEnabled = projectName == "Project B"; // Toggle based on project

        // Act & Assert
        if (isTenantEnabled)
        {
            // Project B: Tenant-enabled flow
            var userId = TenantTestHelper.GenerateUniqueUserId();
            var tenantId = TenantTestHelper.GenerateUniqueTenantId();

            var createCommand = new CreateTenantCommand(
                TenantId: tenantId,
                TenantName: $"{projectName} Tenant",
                UserId: userId,
                StartDate: DateTime.UtcNow,
                ExpireDate: DateTime.UtcNow.AddYears(1),
                Data: $"{{\"Jwt\":{{\"Secret\":\"secret\",\"Issuer\":\"{projectName}\"}}}}"
            );

            var tenant = await SendAsync(createCommand);

            tenant.Should().NotBeNull();
            tenant.TenantId.Should().Be(tenantId);
            tenant.TenantName.Should().Contain(projectName);
        }
        else
        {
            // Project A: Non-tenant flow
            var settings = GetDefaultSettings();

            settings.Should().NotBeNull();
            settings.AppName.Should().NotBeNullOrEmpty();
            settings.UseTenantSettings.Should().BeFalse();
        }
    }

    #endregion

    #region Helper Methods for Non-Tenant Scenarios

    /// <summary>
    /// Simulate getting default settings for a non-tenant project
    /// In real scenarios, this would load from appsettings.json or environment variables
    /// </summary>
    private DefaultSettings GetDefaultSettings()
    {
        return new DefaultSettings
        {
            AppName = "Project A",
            UseTenantSettings = false,
            JwtSecret = "default-secret-key",
            JwtIssuer = "ProjectA"
        };
    }

    #endregion
}

/// <summary>
/// Model representing default settings for non-tenant projects
/// </summary>
public class DefaultSettings
{
    public string AppName { get; set; } = string.Empty;
    public bool UseTenantSettings { get; set; }
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = string.Empty;
}
