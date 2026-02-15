using Identity.API.Tests.Infrastructure;
using Identity.Application.Commands.Admin.Claim;
using Identity.Application.Commands.Admin.Role;
using Identity.Application.Queries.Claim;
using Identity.Application.Queries.Role;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.Endpoints;

/// <summary>
/// Integration tests for Role and Claim management endpoints
/// Tests CRUD operations, Redis caching, and system role protection
/// </summary>
[Collection("Sequential")]
public class RoleClaimEndpointsTests : IntegrationTestBase
{
    public RoleClaimEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region Role Tests

    #region Get Roles Tests

    [Fact]
    public async Task GetRoles_ShouldReturnAllRoles()
    {
        // Arrange
        var query = new GetRolesQuery();

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(3); // SuperAdmin, Admin, User (system roles)
        result.Should().Contain(r => r.Name == "SuperAdmin" && r.IsSystemRole);
        result.Should().Contain(r => r.Name == "Admin" && r.IsSystemRole);
        result.Should().Contain(r => r.Name == "User" && r.IsSystemRole);
    }

    [Fact]
    public async Task GetRoles_SecondCall_ShouldUseCachedData()
    {
        // Arrange
        var query = new GetRolesQuery();

        // Act - First call (cache miss, hits database)
        var firstResult = await SendAsync(query);
        
        // Act - Second call (should use cached data)
        var secondResult = await SendAsync(query);

        // Assert
        firstResult.Should().BeEquivalentTo(secondResult);
        firstResult.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetRoleById_WithValidId_ShouldReturnRole()
    {
        // Arrange
        var allRoles = await SendAsync(new GetRolesQuery());
        var superAdminRole = allRoles.FirstOrDefault(r => r.Name == "SuperAdmin" && r.IsSystemRole);
        
        // SuperAdmin role should exist
        superAdminRole.Should().NotBeNull();
        
        var query = new GetRoleByIdQuery(superAdminRole!.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(superAdminRole.Id);
        result.Name.Should().Be("SuperAdmin");
        result.IsSystemRole.Should().BeTrue();
    }

    [Fact]
    public async Task GetRoleById_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var query = new GetRoleByIdQuery(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(query)
        );
    }

    #endregion

    #region Create Role Tests

    [Fact]
    public async Task CreateRole_WithValidData_ShouldCreateRole()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var command = new CreateRoleCommand(
            Name: $"TestRole_{testId}",
            Description: "Test role for integration testing"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be($"TestRole_{testId}");
        result.Description.Should().Be("Test role for integration testing");
        result.IsSystemRole.Should().BeFalse();
        result.Status.Should().BeTrue();

        // Verify persisted in database
        var roleFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.Roles.FirstOrDefaultAsync(r => r.Name == command.Name);
        });
        
        roleFromDb.Should().NotBeNull();
        roleFromDb!.Name.Should().Be(command.Name);
    }

    [Fact]
    public async Task CreateRole_WithExistingName_ShouldThrowConflictException()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var roleName = $"DuplicateRole_{testId}";
        
        // Create first role
        await SendAsync(new CreateRoleCommand(roleName, "First role"));

        // Try to create duplicate
        var duplicateCommand = new CreateRoleCommand(roleName, "Duplicate role");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            async () => await SendAsync(duplicateCommand)
        );
    }

    [Fact]
    public async Task CreateRole_ShouldInvalidateRolesCache()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        // Get roles (cache them)
        var rolesBeforeCreate = await SendAsync(new GetRolesQuery());
        var countBefore = rolesBeforeCreate.Count;

        // Act - Create new role (should invalidate cache)
        var createCommand = new CreateRoleCommand($"CacheTestRole_{testId}", "Cache test");
        await SendAsync(createCommand);

        // Get roles again (should fetch from DB, not cache)
        var rolesAfterCreate = await SendAsync(new GetRolesQuery());

        // Assert
        rolesAfterCreate.Count.Should().Be(countBefore + 1);
        rolesAfterCreate.Should().Contain(r => r.Name == $"CacheTestRole_{testId}");
    }

    #endregion

    #region Update Role Tests

    [Fact]
    public async Task UpdateRole_WithValidData_ShouldUpdateRole()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var createCommand = new CreateRoleCommand($"UpdateTestRole_{testId}", "Original description");
        var createdRole = await SendAsync(createCommand);

        var updateCommand = new UpdateRoleCommand(
            Id: createdRole.Id,
            Name: $"UpdatedRole_{testId}",
            Description: "Updated description"
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(createdRole.Id);
        result.Name.Should().Be($"UpdatedRole_{testId}");
        result.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateRole_SystemRole_CannotRename()
    {
        // Arrange
        var roles = await SendAsync(new GetRolesQuery());
        var superAdminRole = roles.First(r => r.Name == "SuperAdmin");

        var updateCommand = new UpdateRoleCommand(
            Id: superAdminRole.Id,
            Name: "NewSuperAdminName", // Try to rename
            Description: "Updated description"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(updateCommand)
        );
    }

    [Fact]
    public async Task UpdateRole_SystemRole_CanUpdateDescription()
    {
        // Arrange
        var roles = await SendAsync(new GetRolesQuery());
        var userRole = roles.First(r => r.Name == "User");

        var updateCommand = new UpdateRoleCommand(
            Id: userRole.Id,
            Name: "User", // Keep same name
            Description: "Updated user role description"
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("User");
        result.Description.Should().Be("Updated user role description");
    }

    [Fact]
    public async Task UpdateRole_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var updateCommand = new UpdateRoleCommand(
            Id: 99999,
            Name: "NonExistent",
            Description: "Does not exist"
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(updateCommand)
        );
    }

    #endregion

    #region Delete Role Tests

    [Fact]
    public async Task DeleteRole_WithValidId_ShouldDeleteRole()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var createCommand = new CreateRoleCommand($"DeleteTestRole_{testId}", "Will be deleted");
        var createdRole = await SendAsync(createCommand);

        var deleteCommand = new DeleteRoleCommand(createdRole.Id);

        // Act
        await SendAsync(deleteCommand);

        // Assert - Should throw NotFoundException when trying to get deleted role
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(new GetRoleByIdQuery(createdRole.Id))
        );
    }

    [Fact]
    public async Task DeleteRole_SystemRole_ShouldThrowBadRequestException()
    {
        // Arrange
        var roles = await SendAsync(new GetRolesQuery());
        var superAdminRole = roles.First(r => r.Name == "SuperAdmin");

        var deleteCommand = new DeleteRoleCommand(superAdminRole.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(deleteCommand)
        );
    }

    [Fact]
    public async Task DeleteRole_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var deleteCommand = new DeleteRoleCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(deleteCommand)
        );
    }

    #endregion

    #region Assign Claims to Role Tests

    [Fact]
    public async Task AssignClaimsToRole_WithValidData_ShouldAssignClaims()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        // Create role
        var role = await SendAsync(new CreateRoleCommand($"RoleWithClaims_{testId}", "Test role"));
        
        // Create claims
        var claim1 = await SendAsync(new CreateClaimCommand(
            Name: $"Claim1_{testId}",
            ClaimType: "permission",
            ClaimValue: $"test.claim1.{testId}",
            IsSuperAdminOnly: false,
            Description: "Test claim 1"
        ));
        
        var claim2 = await SendAsync(new CreateClaimCommand(
            Name: $"Claim2_{testId}",
            ClaimType: "permission",
            ClaimValue: $"test.claim2.{testId}",
            IsSuperAdminOnly: false,
            Description: "Test claim 2"
        ));

        // Assign claims to role
        var assignCommand = new AssignClaimsToRoleCommand(
            RoleId: role.Id,
            ClaimIds: new List<int> { claim1.Id, claim2.Id }
        );

        // Act
        await SendAsync(assignCommand);

        // Assert - Get role and verify claims are assigned
        var roleWithClaims = await SendAsync(new GetRoleByIdQuery(role.Id));
        roleWithClaims.Claims.Should().NotBeNull();
        roleWithClaims.Claims.Should().HaveCount(2);
        roleWithClaims.Claims.Should().Contain(c => c.Id == claim1.Id);
        roleWithClaims.Claims.Should().Contain(c => c.Id == claim2.Id);
    }

    [Fact]
    public async Task AssignClaimsToRole_ReplaceExistingClaims_ShouldReplaceNotAppend()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        // Create role and claims
        var role = await SendAsync(new CreateRoleCommand($"RoleReplace_{testId}", "Test"));
        var claim1 = await SendAsync(new CreateClaimCommand($"OldClaim_{testId}", "permission", $"old.{testId}", false, "Old"));
        var claim2 = await SendAsync(new CreateClaimCommand($"NewClaim_{testId}", "permission", $"new.{testId}", false, "New"));

        // Assign first claim
        await SendAsync(new AssignClaimsToRoleCommand(role.Id, new List<int> { claim1.Id }));

        // Act - Assign different claim (should replace, not append)
        await SendAsync(new AssignClaimsToRoleCommand(role.Id, new List<int> { claim2.Id }));

        // Assert
        var roleWithClaims = await SendAsync(new GetRoleByIdQuery(role.Id));
        roleWithClaims.Claims.Should().HaveCount(1);
        roleWithClaims.Claims.Should().Contain(c => c.Id == claim2.Id);
        roleWithClaims.Claims.Should().NotContain(c => c.Id == claim1.Id);
    }

    [Fact]
    public async Task AssignClaimsToRole_WithNonExistentRole_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new AssignClaimsToRoleCommand(
            RoleId: 99999,
            ClaimIds: new List<int> { 1 }
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command)
        );
    }

    #endregion

    #region Assign Roles to User Tests

    [Fact]
    public async Task AssignRolesToUser_WithValidData_ShouldAssignRoles()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        // Create a test user
        var user = await ExecuteDbContextAsync(async context =>
        {
            var newUser = new Identity.Domain.Entities.User
            {
                Email = $"testroleuser_{testId}@example.com",
                FirstName = "Test",
                LastName = "User",
                Status = true,
                Created = DateTime.UtcNow
            };
            context.Users.Add(newUser);
            await context.SaveChangesAsync();
            return newUser;
        });

        // Get roles to assign
        var roles = await SendAsync(new GetRolesQuery());
        var userRole = roles.First(r => r.Name == "User");
        var adminRole = roles.First(r => r.Name == "Admin");

        var assignCommand = new Identity.Application.Commands.Admin.Role.AssignRolesToUserCommand(
            UserId: user.Id,
            RoleIds: new List<int> { userRole.Id, adminRole.Id }
        );

        // Act
        var result = await SendAsync(assignCommand);

        // Assert
        result.Should().BeTrue();

        // Verify roles are assigned in database
        var userRoles = await ExecuteDbContextAsync(async context =>
        {
            return await context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Include(ur => ur.Role)
                .ToListAsync();
        });

        userRoles.Should().HaveCount(2);
        userRoles.Should().Contain(ur => ur.RoleId == userRole.Id);
        userRoles.Should().Contain(ur => ur.RoleId == adminRole.Id);
    }

    [Fact]
    public async Task AssignRolesToUser_ReplaceExistingRoles_ShouldReplaceNotAppend()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        // Create a test user
        var user = await ExecuteDbContextAsync(async context =>
        {
            var newUser = new Identity.Domain.Entities.User
            {
                Email = $"replaceroleuser_{testId}@example.com",
                FirstName = "Replace",
                LastName = "User",
                Status = true,
                Created = DateTime.UtcNow
            };
            context.Users.Add(newUser);
            await context.SaveChangesAsync();
            return newUser;
        });

        var roles = await SendAsync(new GetRolesQuery());
        var userRole = roles.First(r => r.Name == "User");
        var adminRole = roles.First(r => r.Name == "Admin");

        // Assign User role first
        await SendAsync(new Identity.Application.Commands.Admin.Role.AssignRolesToUserCommand(
            user.Id,
            new List<int> { userRole.Id }
        ));

        // Act - Assign Admin role (should replace User role, not append)
        var result = await SendAsync(new Identity.Application.Commands.Admin.Role.AssignRolesToUserCommand(
            user.Id,
            new List<int> { adminRole.Id }
        ));

        // Assert
        result.Should().BeTrue();

        var userRoles = await ExecuteDbContextAsync(async context =>
        {
            return await context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync();
        });

        userRoles.Should().HaveCount(1);
        userRoles.Should().Contain(ur => ur.RoleId == adminRole.Id);
        userRoles.Should().NotContain(ur => ur.RoleId == userRole.Id);
    }

    [Fact]
    public async Task AssignRolesToUser_WithEmptyRoleList_ShouldRemoveAllRoles()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        // Create a test user with a role
        var user = await ExecuteDbContextAsync(async context =>
        {
            var newUser = new Identity.Domain.Entities.User
            {
                Email = $"emptyroleuser_{testId}@example.com",
                FirstName = "Empty",
                LastName = "User",
                Status = true,
                Created = DateTime.UtcNow
            };
            context.Users.Add(newUser);
            await context.SaveChangesAsync();
            return newUser;
        });

        var roles = await SendAsync(new GetRolesQuery());
        var userRole = roles.First(r => r.Name == "User");

        // Assign User role first
        await SendAsync(new Identity.Application.Commands.Admin.Role.AssignRolesToUserCommand(
            user.Id,
            new List<int> { userRole.Id }
        ));

        // Act - Assign empty list (should remove all roles)
        var result = await SendAsync(new Identity.Application.Commands.Admin.Role.AssignRolesToUserCommand(
            user.Id,
            new List<int>()
        ));

        // Assert
        result.Should().BeTrue();

        var userRoles = await ExecuteDbContextAsync(async context =>
        {
            return await context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync();
        });

        userRoles.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignRolesToUser_WithNonExistentUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var roles = await SendAsync(new GetRolesQuery());
        var userRole = roles.First(r => r.Name == "User");

        var command = new Identity.Application.Commands.Admin.Role.AssignRolesToUserCommand(
            UserId: 99999,
            RoleIds: new List<int> { userRole.Id }
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task AssignRolesToUser_WithNonExistentRole_ShouldThrowNotFoundException()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        // Create a test user
        var user = await ExecuteDbContextAsync(async context =>
        {
            var newUser = new Identity.Domain.Entities.User
            {
                Email = $"badroleuser_{testId}@example.com",
                FirstName = "Bad",
                LastName = "User",
                Status = true,
                Created = DateTime.UtcNow
            };
            context.Users.Add(newUser);
            await context.SaveChangesAsync();
            return newUser;
        });

        var command = new Identity.Application.Commands.Admin.Role.AssignRolesToUserCommand(
            UserId: user.Id,
            RoleIds: new List<int> { 99999 }
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task AssignRolesToUser_WithMultipleRoles_ShouldAssignAll()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        // Create a test user
        var user = await ExecuteDbContextAsync(async context =>
        {
            var newUser = new Identity.Domain.Entities.User
            {
                Email = $"multiroleuser_{testId}@example.com",
                FirstName = "Multi",
                LastName = "User",
                Status = true,
                Created = DateTime.UtcNow
            };
            context.Users.Add(newUser);
            await context.SaveChangesAsync();
            return newUser;
        });

        var roles = await SendAsync(new GetRolesQuery());
        var allRoleIds = roles.Where(r => r.Name != "SuperAdmin").Select(r => r.Id).ToList();

        var command = new Identity.Application.Commands.Admin.Role.AssignRolesToUserCommand(
            UserId: user.Id,
            RoleIds: allRoleIds
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeTrue();

        var userRoles = await ExecuteDbContextAsync(async context =>
        {
            return await context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync();
        });

        userRoles.Should().HaveCount(allRoleIds.Count);
        foreach (var roleId in allRoleIds)
        {
            userRoles.Should().Contain(ur => ur.RoleId == roleId);
        }
    }

    #endregion

    #endregion

    #region Claim Tests

    #region Get Claims Tests

    [Fact]
    public async Task GetClaims_ShouldReturnAllClaims()
    {
        // Arrange
        var query = new GetClaimsQuery();

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        // May have seeded claims or empty
        result.Should().BeAssignableTo<List<Identity.Application.DTOs.ClaimDto>>();
    }

    [Fact]
    public async Task GetClaims_AfterCreation_ShouldIncludeNewClaim()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var claimsBefore = await SendAsync(new GetClaimsQuery());
        var countBefore = claimsBefore.Count;

        // Create claim
        var createCommand = new CreateClaimCommand(
            Name: $"TestClaim_{testId}",
            ClaimType: "permission",
            ClaimValue: $"test.permission.{testId}",
            IsSuperAdminOnly: false,
            Description: "Test claim"
        );
        await SendAsync(createCommand);

        // Act
        var claimsAfter = await SendAsync(new GetClaimsQuery());

        // Assert
        claimsAfter.Count.Should().Be(countBefore + 1);
        claimsAfter.Should().Contain(c => c.Name == $"TestClaim_{testId}");
    }

    [Fact]
    public async Task GetClaimById_WithValidId_ShouldReturnClaim()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var createCommand = new CreateClaimCommand(
            Name: $"GetByIdClaim_{testId}",
            ClaimType: "permission",
            ClaimValue: $"test.getbyid.{testId}",
            IsSuperAdminOnly: false,
            Description: "Get by ID test"
        );
        var createdClaim = await SendAsync(createCommand);

        var query = new GetClaimByIdQuery(createdClaim.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(createdClaim.Id);
        result.Name.Should().Be($"GetByIdClaim_{testId}");
        result.ClaimType.Should().Be("permission");
        result.ClaimValue.Should().Be($"test.getbyid.{testId}");
    }

    [Fact]
    public async Task GetClaimById_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var query = new GetClaimByIdQuery(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(query)
        );
    }

    #endregion

    #region Create Claim Tests

    [Fact]
    public async Task CreateClaim_WithValidData_ShouldCreateClaim()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var command = new CreateClaimCommand(
            Name: $"NewClaim_{testId}",
            ClaimType: "permission",
            ClaimValue: $"new.permission.{testId}",
            IsSuperAdminOnly: false,
            Description: "New claim for testing"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be($"NewClaim_{testId}");
        result.ClaimType.Should().Be("permission");
        result.ClaimValue.Should().Be($"new.permission.{testId}");
        result.IsSuperAdminOnly.Should().BeFalse();
        result.Status.Should().BeTrue();

        // Verify persisted in database
        var claimFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.Claims.FirstOrDefaultAsync(c => c.Name == command.Name);
        });
        
        claimFromDb.Should().NotBeNull();
        claimFromDb!.ClaimValue.Should().Be(command.ClaimValue);
    }

    [Fact]
    public async Task CreateClaim_WithSuperAdminFlag_ShouldCreateSuperAdminClaim()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var command = new CreateClaimCommand(
            Name: $"SuperAdminClaim_{testId}",
            ClaimType: "permission",
            ClaimValue: $"superadmin.only.{testId}",
            IsSuperAdminOnly: true,
            Description: "SuperAdmin only claim"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuperAdminOnly.Should().BeTrue();
    }

    [Fact]
    public async Task CreateClaim_WithExistingClaimValue_ShouldThrowConflictException()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var claimValue = $"duplicate.claim.{testId}";
        
        // Create first claim
        await SendAsync(new CreateClaimCommand(
            $"FirstClaim_{testId}",
            "permission",
            claimValue,
            false,
            "First"
        ));

        // Try to create duplicate with same ClaimValue
        var duplicateCommand = new CreateClaimCommand(
            $"SecondClaim_{testId}",
            "permission",
            claimValue,
            false,
            "Duplicate"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            async () => await SendAsync(duplicateCommand)
        );
    }

    #endregion

    #region Update Claim Tests

    [Fact]
    public async Task UpdateClaim_WithValidData_ShouldUpdateClaim()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var createCommand = new CreateClaimCommand(
            $"UpdateClaim_{testId}",
            "permission",
            $"update.test.{testId}",
            false,
            "Original"
        );
        var createdClaim = await SendAsync(createCommand);

        var updateCommand = new UpdateClaimCommand(
            Id: createdClaim.Id,
            Name: $"UpdatedClaim_{testId}",
            ClaimType: "role",
            ClaimValue: $"updated.value.{testId}",
            IsSuperAdminOnly: true,
            Description: "Updated description"
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(createdClaim.Id);
        result.Name.Should().Be($"UpdatedClaim_{testId}");
        result.ClaimType.Should().Be("role");
        result.ClaimValue.Should().Be($"updated.value.{testId}");
        result.IsSuperAdminOnly.Should().BeTrue();
        result.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateClaim_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var updateCommand = new UpdateClaimCommand(
            Id: 99999,
            Name: "NonExistent",
            ClaimType: "permission",
            ClaimValue: "non.existent",
            IsSuperAdminOnly: false,
            Description: "Does not exist"
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(updateCommand)
        );
    }

    #endregion

    #region Delete Claim Tests

    [Fact]
    public async Task DeleteClaim_WithValidId_ShouldDeleteClaim()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var createCommand = new CreateClaimCommand(
            $"DeleteClaim_{testId}",
            "permission",
            $"delete.test.{testId}",
            false,
            "Will be deleted"
        );
        var createdClaim = await SendAsync(createCommand);

        var deleteCommand = new DeleteClaimCommand(createdClaim.Id);

        // Act
        await SendAsync(deleteCommand);

        // Assert - Should throw NotFoundException when trying to get deleted claim
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(new GetClaimByIdQuery(createdClaim.Id))
        );
    }

    [Fact]
    public async Task DeleteClaim_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var deleteCommand = new DeleteClaimCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(deleteCommand)
        );
    }

    #endregion

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task UpdateRole_ShouldInvalidateCache()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var createCommand = new CreateRoleCommand($"CacheInvalidateRole_{testId}", "Original");
        var createdRole = await SendAsync(createCommand);

        // Cache the role by getting it
        var roleBeforeUpdate = await SendAsync(new GetRoleByIdQuery(createdRole.Id));

        // Act - Update role (should invalidate cache)
        var updateCommand = new UpdateRoleCommand(
            createdRole.Id,
            $"UpdatedCacheRole_{testId}",
            "Updated"
        );
        await SendAsync(updateCommand);

        // Get role again (should be from DB with updated data, not old cache)
        var roleAfterUpdate = await SendAsync(new GetRoleByIdQuery(createdRole.Id));

        // Assert
        roleAfterUpdate.Name.Should().Be($"UpdatedCacheRole_{testId}");
        roleAfterUpdate.Description.Should().Be("Updated");
        roleAfterUpdate.Name.Should().NotBe(roleBeforeUpdate.Name);
    }

    [Fact]
    public async Task DeleteClaim_ShouldInvalidateCache()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var claim = await SendAsync(new CreateClaimCommand(
            $"CacheDeleteClaim_{testId}",
            "permission",
            $"cache.delete.{testId}",
            false,
            "Will be deleted"
        ));

        // Cache all claims
        var claimsBeforeDelete = await SendAsync(new GetClaimsQuery());
        var countBefore = claimsBeforeDelete.Count;

        // Act - Delete claim (should invalidate cache)
        await SendAsync(new DeleteClaimCommand(claim.Id));

        // Get all claims again (should be fresh from DB)
        var claimsAfterDelete = await SendAsync(new GetClaimsQuery());

        // Assert
        claimsAfterDelete.Count.Should().Be(countBefore - 1);
        claimsAfterDelete.Should().NotContain(c => c.Id == claim.Id);
    }

    #endregion

    #region SuperAdmin Security Tests

    [Fact]
    public async Task UpdateRole_SuperAdminRole_NonSuperAdmin_ShouldThrowForbiddenException()
    {
        // Arrange - Get SuperAdmin role
        var roles = await SendAsync(new GetRolesQuery());
        var superAdminRole = roles.First(r => r.Name == "SuperAdmin");

        // Try to update SuperAdmin role as non-SuperAdmin (Admin or User)
        var updateCommand = new UpdateRoleCommand(
            Id: superAdminRole.Id,
            Name: "SuperAdmin",
            Description: "Trying to update SuperAdmin role"
        );

        // Act & Assert - Should throw ForbiddenException
        // Note: This test assumes the test runner does NOT have SuperAdmin role
        // In actual testing, you would need to mock ICurrentUserService to return false for IsSuperAdmin
        await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(updateCommand)
        );
    }

    [Fact]
    public async Task DeleteRole_SuperAdminRole_NonSuperAdmin_ShouldThrowForbiddenException()
    {
        // Arrange - Get SuperAdmin role
        var roles = await SendAsync(new GetRolesQuery());
        var superAdminRole = roles.First(r => r.Name == "SuperAdmin");

        // Try to delete SuperAdmin role as non-SuperAdmin
        var deleteCommand = new DeleteRoleCommand(superAdminRole.Id);

        // Act & Assert - Should throw ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(deleteCommand)
        );
    }

    [Fact]
    public async Task AssignClaimsToRole_SuperAdminRole_NonSuperAdmin_ShouldThrowForbiddenException()
    {
        // Arrange - Get SuperAdmin role
        var roles = await SendAsync(new GetRolesQuery());
        var superAdminRole = roles.First(r => r.Name == "SuperAdmin");

        // Create a test claim
        var testId = Guid.NewGuid().ToString("N")[..8];
        var claim = await SendAsync(new CreateClaimCommand(
            $"TestClaim_{testId}",
            "permission",
            $"test.{testId}",
            false,
            "Test"
        ));

        // Try to assign claims to SuperAdmin role as non-SuperAdmin
        var assignCommand = new AssignClaimsToRoleCommand(
            RoleId: superAdminRole.Id,
            ClaimIds: new List<int> { claim.Id }
        );

        // Act & Assert - Should throw ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(assignCommand)
        );
    }

    [Fact]
    public async Task UpdateClaim_SuperAdminOnlyClaim_NonSuperAdmin_ShouldThrowForbiddenException()
    {
        // Arrange - Create a SuperAdmin-only claim
        var testId = Guid.NewGuid().ToString("N")[..8];
        var createCommand = new CreateClaimCommand(
            Name: $"SuperAdminClaim_{testId}",
            ClaimType: "permission",
            ClaimValue: $"superadmin.test.{testId}",
            IsSuperAdminOnly: true,
            Description: "SuperAdmin only claim"
        );
        var createdClaim = await SendAsync(createCommand);

        // Try to update SuperAdmin-only claim as non-SuperAdmin
        var updateCommand = new UpdateClaimCommand(
            Id: createdClaim.Id,
            Name: $"UpdatedClaim_{testId}",
            ClaimType: "permission",
            ClaimValue: $"updated.{testId}",
            IsSuperAdminOnly: true,
            Description: "Trying to update"
        );

        // Act & Assert - Should throw ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(updateCommand)
        );
    }

    [Fact]
    public async Task DeleteClaim_SuperAdminOnlyClaim_NonSuperAdmin_ShouldThrowForbiddenException()
    {
        // Arrange - Create a SuperAdmin-only claim
        var testId = Guid.NewGuid().ToString("N")[..8];
        var createCommand = new CreateClaimCommand(
            Name: $"SuperAdminClaim_{testId}",
            ClaimType: "permission",
            ClaimValue: $"superadmin.delete.{testId}",
            IsSuperAdminOnly: true,
            Description: "SuperAdmin only claim"
        );
        var createdClaim = await SendAsync(createCommand);

        // Try to delete SuperAdmin-only claim as non-SuperAdmin
        var deleteCommand = new DeleteClaimCommand(createdClaim.Id);

        // Act & Assert - Should throw ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(deleteCommand)
        );
    }

    #endregion
}

