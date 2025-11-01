using Tenant.API.Tests.Infrastructure;
using Tenant.Application.Commands.Tenant;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace Tenant.API.Tests.Endpoints;

/// <summary>
/// Integration tests for admin-only tenant management endpoints
/// Tests require Admin or SuperAdmin role authorization
/// </summary>
[Collection("Sequential")]
public class AdminTenantEndpointsTests : IntegrationTestBase
{
    public AdminTenantEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region Admin Get All Active Tenants

    [Fact]
    public async Task GetAllActiveTenants_AsAdmin_ShouldReturnAllActiveTenants()
    {
        // Arrange - Create mix of active and inactive tenants
        await CreateTestTenantAsync(tenantName: "Active Tenant 1", isActive: true);
        await CreateTestTenantAsync(tenantName: "Active Tenant 2", isActive: true);
        await CreateTestTenantAsync(tenantName: "Active Tenant 3", isActive: true);
        await CreateTestTenantAsync(tenantName: "Inactive Tenant", isActive: false);

        var query = new GetAllActiveTenantsQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterOrEqualTo(3);
        result.Items.Should().OnlyContain(t => t.IsActive);
        result.Items.Should().NotContain(t => t.TenantName == "Inactive Tenant");
    }

    [Fact]
    public async Task GetAllActiveTenants_WithLargePageSize_ShouldReturnAllTenantsInOnePage()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            await CreateTestTenantAsync(isActive: true);
        }

        var query = new GetAllActiveTenantsQuery(PageNumber: 1, PageSize: 100);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterOrEqualTo(3);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllActiveTenants_WithSmallPageSize_ShouldPaginateCorrectly()
    {
        // Arrange - Create 5 active tenants
        for (int i = 0; i < 5; i++)
        {
            await CreateTestTenantAsync(isActive: true);
        }

        var page1Query = new GetAllActiveTenantsQuery(PageNumber: 1, PageSize: 2);
        var page2Query = new GetAllActiveTenantsQuery(PageNumber: 2, PageSize: 2);

        // Act
        var page1Result = await SendAsync(page1Query);
        var page2Result = await SendAsync(page2Query);

        // Assert
        page1Result.Should().NotBeNull();
        page1Result.Items.Should().HaveCount(2);
        page1Result.PageNumber.Should().Be(1);
        page1Result.HasNextPage.Should().BeTrue();
        page1Result.HasPreviousPage.Should().BeFalse();

        page2Result.Should().NotBeNull();
        page2Result.Items.Should().HaveCount(2);
        page2Result.PageNumber.Should().Be(2);
        page2Result.HasPreviousPage.Should().BeTrue();

        // Verify no duplicate items between pages
        var page1Ids = page1Result.Items.Select(t => t.TenantId);
        var page2Ids = page2Result.Items.Select(t => t.TenantId);
        page1Ids.Should().NotIntersectWith(page2Ids);
    }

    #endregion

    #region Admin Get Tenant By User

    [Fact]
    public async Task GetTenantByUser_AsAdmin_WithValidUserId_ShouldReturnTenant()
    {
        // Arrange
        var userId = 777;
        var tenant = await CreateTestTenantAsync(userId: userId, tenantName: "User 777 Tenant");
        var query = new GetTenantByUserQuery(userId);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenant.TenantId);
        result.TenantName.Should().Be("User 777 Tenant");
    }

    [Fact]
    public async Task GetTenantByUser_AsAdmin_WithNonExistentUserId_ShouldReturnNull()
    {
        // Arrange
        var query = new GetTenantByUserQuery(999999);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTenantByUser_AsAdmin_WithInactiveTenant_ShouldStillReturnTenant()
    {
        // Arrange
        var userId = 888;
        var tenant = await CreateTestTenantAsync(userId: userId, isActive: false);
        var query = new GetTenantByUserQuery(userId);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.IsActive.Should().BeFalse();
    }

    #endregion

    #region Admin Create Tenant

    [Fact]
    public async Task CreateTenant_AsAdmin_WithValidData_ShouldCreateTenant()
    {
        // Arrange
        var tenantId = GenerateUniqueTenantId();
        var createCommand = new CreateTenantCommand(
            TenantId: tenantId,
            TenantName: "Admin Created Tenant",
            UserId: 123,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: new TenantConfiguration
            {
                Jwt = new JwtSettings
                {
                    Secret = "admin-secret-key-minimum-32-chars",
                    Issuer = "AdminCompany",
                    Audience = "AdminApp",
                    AccessTokenExpirationMinutes = 60,
                    RefreshTokenExpirationDays = 7
                }
            }
        );

        // Act
        var result = await SendAsync(createCommand);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenantId);
        result.TenantName.Should().Be("Admin Created Tenant");
        result.UserId.Should().Be(123);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTenant_AsAdmin_WithComplexConfiguration_ShouldPersistCorrectly()
    {
        // Arrange
        var complexData = new TenantConfiguration
        {
            Jwt = new JwtSettings
            {
                Secret = "complex-secret-key-minimum-32-chars",
                Issuer = "ComplexIssuer",
                Audience = "ComplexAudience",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7
            },
            DatabaseSettings = new DatabaseSettings
            {
                Provider = "PostgreSql",
                ConnectionString = "Host=smtp.example.com;Port=587;Database=ComplexDB"
            },
            Cors = new CorsSettings
            {
                AllowedOrigins = new[] { "http://example.com" }
            }
        };

        var createCommand = new CreateTenantCommand(
            TenantId: GenerateUniqueTenantId(),
            TenantName: "Complex Config Tenant",
            UserId: 456,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: complexData
        );

        // Act
        var result = await SendAsync(createCommand);

        // Assert
        result.Should().NotBeNull();
        
        // Verify we can retrieve the config and it contains our data
        var configQuery = new GetTenantConfigQuery(result.TenantId);
        var config = await SendAsync(configQuery);
        config.Should().NotBeNull();
        config!.Data.Should().NotBeNull();
        config.Data!.Jwt.Should().NotBeNull();
        config.Data.Jwt!.Issuer.Should().Be("ComplexIssuer");
    }

    #endregion

    #region Admin Update Tenant

    [Fact]
    public async Task UpdateTenant_AsAdmin_ShouldUpdateAllFields()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var newExpireDate = DateTime.UtcNow.AddYears(3);
        var newData = new TenantConfiguration
        {
            Jwt = new JwtSettings
            {
                Secret = "updated-by-admin-key-minimum-32",
                Issuer = "AdminUpdated",
                Audience = "UpdatedApp",
                AccessTokenExpirationMinutes = 90,
                RefreshTokenExpirationDays = 14
            }
        };

        var updateCommand = new UpdateTenantCommand(
            TenantId: tenant.TenantId,
            TenantName: "Admin Updated Tenant",
            StartDate: DateTime.UtcNow,
            ExpireDate: newExpireDate,
            Data: newData,
            IsActive: false
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.TenantName.Should().Be("Admin Updated Tenant");
        result.IsActive.Should().BeFalse();
        result.LastModified.Should().NotBeNull();
        
        // Verify data was updated by getting config
        var configQuery = new GetTenantConfigQuery(tenant.TenantId);
        var config = await SendAsync(configQuery);
        config!.Data.Should().NotBeNull();
        config.Data!.Jwt!.Secret.Should().Be("updated-by-admin-key-minimum-32");
    }

    [Fact]
    public async Task UpdateTenant_AsAdmin_TogglingActiveState_ShouldWork()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync(isActive: true);
        var tenantData = DeserializeTenantData(tenant.Data)!;

        // Act 1 - Deactivate
        var deactivateCommand = new UpdateTenantCommand(
            TenantId: tenant.TenantId,
            TenantName: tenant.TenantName,
            StartDate: tenant.StartDate,
            ExpireDate: tenant.ExpireDate,
            Data: tenantData,
            IsActive: false
        );
        var deactivatedResult = await SendAsync(deactivateCommand);

        // Assert 1
        deactivatedResult.IsActive.Should().BeFalse();

        // Act 2 - Reactivate
        var reactivateCommand = new UpdateTenantCommand(
            TenantId: tenant.TenantId,
            TenantName: tenant.TenantName,
            StartDate: tenant.StartDate,
            ExpireDate: tenant.ExpireDate,
            Data: tenantData,
            IsActive: true
        );
        var reactivatedResult = await SendAsync(reactivateCommand);

        // Assert 2
        reactivatedResult.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTenant_AsAdmin_ExtendingExpiration_ShouldUpdateIsExpired()
    {
        // Arrange - Create expired tenant
        var expiredTenant = await CreateTestTenantAsync(
            startDate: DateTime.UtcNow.AddDays(-30),
            expireDate: DateTime.UtcNow.AddDays(-1)
        );
        var tenantData = DeserializeTenantData(expiredTenant.Data)!;

        // Verify it's expired
        var beforeQuery = new GetTenantByIdQuery(expiredTenant.TenantId);
        var beforeResult = await SendAsync(beforeQuery);
        beforeResult!.IsExpired.Should().BeTrue();

        // Act - Extend expiration
        var updateCommand = new UpdateTenantCommand(
            TenantId: expiredTenant.TenantId,
            TenantName: expiredTenant.TenantName,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: tenantData,
            IsActive: true
        );
        var updatedResult = await SendAsync(updateCommand);

        // Assert
        updatedResult.IsExpired.Should().BeFalse();
    }

    #endregion

    #region Admin Delete Tenant

    [Fact]
    public async Task DeleteTenant_AsAdmin_ShouldSoftDeleteTenant()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var deleteCommand = new DeleteTenantCommand(tenant.TenantId);

        // Act
        await SendAsync(deleteCommand);

        // Assert - Tenant should not be retrievable after deletion
        var query = new GetTenantByIdQuery(tenant.TenantId);
        var result = await SendAsync(query);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTenant_AsAdmin_WithActiveTenant_ShouldStillDelete()
    {
        // Arrange
        var activeTenant = await CreateTestTenantAsync(isActive: true);
        var deleteCommand = new DeleteTenantCommand(activeTenant.TenantId);

        // Act
        await SendAsync(deleteCommand);

        // Assert - Should be deleted even if active
        var query = new GetTenantByIdQuery(activeTenant.TenantId);
        var result = await SendAsync(query);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTenant_AsAdmin_ThenVerifyDeletion()
    {
        // Arrange
        var tenantId = GenerateUniqueTenantId();
        var originalTenant = await CreateTestTenantAsync(tenantId: tenantId);
        
        // Act - Delete
        var deleteCommand = new DeleteTenantCommand(tenantId);
        var result = await SendAsync(deleteCommand);

        // Assert
        result.Should().BeTrue();
        
        // Verify tenant is not found after deletion (soft deleted, returns null)
        var query = new GetTenantByIdQuery(tenantId);
        var deletedTenant = await SendAsync(query);
        deletedTenant.Should().BeNull();
    }

    #endregion

    #region Admin Bulk Operations

    [Fact]
    public async Task BulkCreateTenants_AsAdmin_ShouldCreateAllTenants()
    {
        // Arrange - Use GenerateUniqueUserId() for each tenant
        var createCommands = Enumerable.Range(1, 5).Select(i => new CreateTenantCommand(
            TenantId: $"bulk-tenant-{i}-{Guid.NewGuid():N}",
            TenantName: $"Bulk Tenant {i}",
            UserId: GenerateUniqueUserId(), // Generate unique user ID for each
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: CreateDefaultTenantConfiguration()
        )).ToList();

        // Act
        var results = new List<object>();
        foreach (var command in createCommands)
        {
            var result = await SendAsync(command);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r != null);

        // Verify all can be retrieved
        var query = new GetAllActiveTenantsQuery(PageNumber: 1, PageSize: 100);
        var allTenants = await SendAsync(query);
        allTenants.Items.Count().Should().BeGreaterOrEqualTo(5);
    }

    #endregion

    #region Admin Edge Cases

    [Fact]
    public async Task GetTenantConfig_AsAdmin_ShouldIncludeSensitiveData()
    {
        // Arrange
        var sensitiveData = new TenantConfiguration
        {
            Jwt = new JwtSettings { Secret = "super-secret-key-123" }
        };
        var tenant = await CreateTestTenantAsync(data: sensitiveData);
        var query = new GetTenantConfigQuery(tenant.TenantId);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Jwt!.Secret.Should().Be("super-secret-key-123");
    }

    [Fact]
    public async Task UpdateTenant_AsAdmin_WithMinimalChanges_ShouldOnlyUpdateModifiedFields()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var tenantData = DeserializeTenantData(tenant.Data)!;
        var originalName = tenant.TenantName;
        
        // Act - Only change the name
        var updateCommand = new UpdateTenantCommand(
            TenantId: tenant.TenantId,
            TenantName: "Only Name Changed",
            StartDate: tenant.StartDate,
            ExpireDate: tenant.ExpireDate,
            Data: tenantData,
            IsActive: tenant.IsActive
        );
        var result = await SendAsync(updateCommand);

        // Assert
        result.TenantName.Should().Be("Only Name Changed");
        result.TenantName.Should().NotBe(originalName);
        result.IsActive.Should().Be(tenant.IsActive);
        
        // Verify data wasn't changed
        var configQuery = new GetTenantConfigQuery(tenant.TenantId);
        var config = await SendAsync(configQuery);
        var originalData = DeserializeTenantData(tenant.Data)!;
        config!.Data!.Jwt!.Secret.Should().Be(originalData.Jwt!.Secret);
    }

    #endregion
}
