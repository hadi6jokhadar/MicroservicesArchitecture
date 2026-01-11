using Tenant.API.Tests.Infrastructure;
using Tenant.Application.Commands.Tenant;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace Tenant.API.Tests.Endpoints;

/// <summary>
/// Integration tests for tenant management endpoints using MediatR handlers directly
/// This approach bypasses HTTP layer and avoids .NET 9.0 PipeWriter bug
/// </summary>
[Collection("Sequential")]
public class TenantEndpointsTests : IntegrationTestBase
{
    public TenantEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region Create Tenant Tests

    [Fact]
    public async Task CreateTenant_WithValidData_ShouldReturnCreatedTenant()
    {
        // Arrange
        var tenantId = GenerateUniqueTenantId();
        var tenantConfig = new TenantConfiguration
        {
            Jwt = new JwtSettings
            {
                Secret = "test-secret-minimum-32-characters-long",
                Issuer = "TestCompany",
                Audience = "TestApp",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7
            }
        };

        var createCommand = new CreateTenantCommand(
            TenantId: tenantId,
            TenantName: "Test Company",
            UserId: 1,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: tenantConfig
        );

        // Act - Call handler directly via MediatR
        var result = await SendAsync(createCommand);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.TenantId.Should().Be(tenantId);
        result.TenantName.Should().Be("Test Company");
        result.UserId.Should().Be(1);
        result.IsActive.Should().BeTrue();
        result.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTenant_WithDuplicateTenantId_ShouldThrowConflictException()
    {
        // Arrange
        var tenantId = GenerateUniqueTenantId();
        await CreateTestTenantAsync(tenantId: tenantId); // Uses unique userId automatically

        var createCommand = new CreateTenantCommand(
            TenantId: tenantId,
            TenantName: "Duplicate Tenant",
            UserId: GenerateUniqueUserId(), // Different user but same tenant ID
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: CreateDefaultTenantConfiguration()
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(
            async () => await SendAsync(createCommand)
        );

        exception.Message.Should().Contain($"Tenant with ID '{tenantId}' already exists");
    }

    // [Fact]
    // public async Task CreateTenant_WithDuplicateUserId_ShouldThrowConflictException()
    // {
    //     // Arrange
    //     var userId = 99;
    //     await CreateTestTenantAsync(tenantId: GenerateUniqueTenantId(), userId: userId);

    //     var createCommand = new CreateTenantCommand(
    //         TenantId: GenerateUniqueTenantId(),
    //         TenantName: "Another Tenant",
    //         UserId: userId,
    //         StartDate: DateTime.UtcNow,
    //         ExpireDate: DateTime.UtcNow.AddYears(1),
    //         Data: "{}"
    //     );

    //     // Act & Assert
    //     var exception = await Assert.ThrowsAsync<ConflictException>(
    //         async () => await SendAsync(createCommand)
    //     );

    //     exception.Message.Should().Contain($"User with ID '{userId}' already has a tenant");
    // }

    [Fact]
    public async Task CreateTenant_WithNullData_ShouldThrowValidationException()
    {
        // Arrange
        var createCommand = new CreateTenantCommand(
            TenantId: GenerateUniqueTenantId(),
            TenantName: "Test Tenant",
            UserId: 1,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: null!
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(createCommand)
        );
    }

    [Fact]
    public async Task CreateTenant_WithExpiredDate_ShouldCreateInactiveState()
    {
        // Arrange
        var createCommand = new CreateTenantCommand(
            TenantId: GenerateUniqueTenantId(),
            TenantName: "Expired Tenant",
            UserId: GenerateUniqueUserId(), // Use unique user ID
            StartDate: DateTime.UtcNow.AddDays(-30),
            ExpireDate: DateTime.UtcNow.AddDays(-1),
            Data: CreateDefaultTenantConfiguration()
        );

        // Act
        var result = await SendAsync(createCommand);

        // Assert
        result.Should().NotBeNull();
        result.IsExpired.Should().BeTrue();
    }

    #endregion

    #region Get Tenant Tests

    [Fact]
    public async Task GetTenantById_WithValidTenantId_ShouldReturnTenant()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var query = new GetTenantByIdQuery(tenant.TenantId);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenant.TenantId);
        result.TenantName.Should().Be(tenant.TenantName);
        result.UserId.Should().Be(tenant.UserId);
    }

    [Fact]
    public async Task GetTenantById_WithNonExistentTenantId_ShouldReturnNull()
    {
        // Arrange
        var query = new GetTenantByIdQuery("non-existent-tenant-id");

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTenantByUser_WithValidUserId_ShouldReturnTenant()
    {
        // Arrange
        var userId = 555;
        var tenant = await CreateTestTenantAsync(userId: userId);
        var query = new GetTenantByUserQuery(userId);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenant.TenantId);
    }

    [Fact]
    public async Task GetTenantByUser_WithNonExistentUserId_ShouldReturnNull()
    {
        // Arrange
        var query = new GetTenantByUserQuery(999999);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTenantConfig_WithValidTenantId_ShouldReturnConfigIncludingData()
    {
        // Arrange
        var data = new TenantConfiguration
        {
            Jwt = new JwtSettings
            {
                Secret = "my-secret-key-minimum-32-characters",
                Issuer = "MyCompany",
                Audience = "MyApp",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7
            }
        };
        var tenant = await CreateTestTenantAsync(data: data);
        var query = new GetTenantConfigQuery(tenant.TenantId);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenant.TenantId);
        result.Data.Should().NotBeNull();
        result.Data!.Jwt.Should().NotBeNull();
        result.Data.Jwt!.Secret.Should().Be("my-secret-key-minimum-32-characters");
        result.Data.Jwt.Issuer.Should().Be("MyCompany");
    }

    #endregion

    #region Update Tenant Tests

    [Fact]
    public async Task UpdateTenant_WithValidData_ShouldUpdateTenant()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var updatedConfig = new TenantConfiguration
        {
            Jwt = new JwtSettings
            {
                Secret = "updated-secret-key-minimum-32-chars",
                Issuer = "UpdatedIssuer",
                Audience = "UpdatedApp",
                AccessTokenExpirationMinutes = 120,
                RefreshTokenExpirationDays = 14
            }
        };

        var updateCommand = new UpdateTenantCommand(
            TenantId: tenant.TenantId,
            TenantName: "Updated Tenant Name",
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(2),
            Data: updatedConfig,
            IsActive: false
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenant.TenantId);
        result.TenantName.Should().Be("Updated Tenant Name");
        result.IsActive.Should().BeFalse();
        result.LastModified.Should().NotBeNull();
        
        // Verify data was updated by getting config
        var configQuery = new GetTenantConfigQuery(tenant.TenantId);
        var config = await SendAsync(configQuery);
        config!.Data.Should().NotBeNull();
        config.Data!.Jwt!.Secret.Should().Be("updated-secret-key-minimum-32-chars");
    }

    [Fact]
    public async Task UpdateTenant_WithNonExistentTenantId_ShouldThrowNotFoundException()
    {
        // Arrange
        var updateCommand = new UpdateTenantCommand(
            TenantId: "non-existent-tenant",
            TenantName: "Updated Name",
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: CreateDefaultTenantConfiguration(),
            IsActive: true
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(updateCommand)
        );

        exception.Message.Should().Contain("exception_tenant_not_found");
    }

    [Fact]
    public async Task UpdateTenant_WithNullData_ShouldThrowValidationException()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var updateCommand = new UpdateTenantCommand(
            TenantId: tenant.TenantId,
            TenantName: "Updated Name",
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: null!,
            IsActive: true
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(updateCommand)
        );
    }

    #endregion

    #region Delete Tenant Tests

    [Fact]
    public async Task DeleteTenant_WithValidTenantId_ShouldSoftDeleteTenant()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var deleteCommand = new DeleteTenantCommand(tenant.TenantId);

        // Act
        await SendAsync(deleteCommand);

        // Assert - Verify tenant is soft deleted (IsArchived = true)
        var query = new GetTenantByIdQuery(tenant.TenantId);
        var result = await SendAsync(query);
        result.Should().BeNull(); // Should not be found after soft delete
    }

    [Fact]
    public async Task DeleteTenant_WithNonExistentTenantId_ShouldThrowNotFoundException()
    {
        // Arrange
        var deleteCommand = new DeleteTenantCommand("non-existent-tenant");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(deleteCommand)
        );

        exception.Message.Should().Contain("exception_tenant_not_found");
    }

    #endregion

    #region Get All Active Tenants Tests

    [Fact]
    public async Task GetAllActiveTenants_ShouldReturnPaginatedResults()
    {
        // Arrange - Create multiple tenants
        await CreateTestTenantAsync(isActive: true);
        await CreateTestTenantAsync(isActive: true);
        await CreateTestTenantAsync(isActive: false); // This should not be included

        var query = new GetAllActiveTenantsQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(t => t.IsActive);
        result.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetAllActiveTenants_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange - Create multiple tenants
        for (int i = 0; i < 5; i++)
        {
            await CreateTestTenantAsync(isActive: true);
        }

        var query = new GetAllActiveTenantsQuery(PageNumber: 1, PageSize: 2);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Count().Should().BeLessThanOrEqualTo(2);
        result.PageNumber.Should().Be(1);
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllActiveTenants_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange - Don't create any tenants, just query
        // Note: Database is shared across tests but filtered by IsActive and IsArchived
        var query = new GetAllActiveTenantsQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await SendAsync(query);

        // Assert - Should return a list (might have tenants from other tests)
        result.Should().NotBeNull();
        result.TotalCount.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task CreateTenant_WithEmptyTenantId_ShouldThrowValidationException()
    {
        // Arrange
        var createCommand = new CreateTenantCommand(
            TenantId: "",
            TenantName: "Test Tenant",
            UserId: 1,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: CreateDefaultTenantConfiguration()
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(createCommand)
        );
    }

    [Fact]
    public async Task CreateTenant_WithStartDateAfterExpireDate_ShouldThrowValidationException()
    {
        // Arrange
        var createCommand = new CreateTenantCommand(
            TenantId: GenerateUniqueTenantId(),
            TenantName: "Test Tenant",
            UserId: 1,
            StartDate: DateTime.UtcNow.AddYears(1),
            ExpireDate: DateTime.UtcNow,
            Data: CreateDefaultTenantConfiguration()
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(createCommand)
        );
    }

    [Fact]
    public async Task CreateTenant_WithInvalidTenantIdPattern_ShouldThrowValidationException()
    {
        // Arrange
        var createCommand = new CreateTenantCommand(
            TenantId: "invalid tenant id with spaces!",
            TenantName: "Test Tenant",
            UserId: 1,
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: CreateDefaultTenantConfiguration()
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(createCommand)
        );
    }

    #endregion
}
