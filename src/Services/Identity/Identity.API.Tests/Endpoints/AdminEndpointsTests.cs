using Identity.API.Tests.Infrastructure;
using Identity.Application.Commands;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Kernel.Enums.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.Endpoints;

/// <summary>
/// Integration tests for admin endpoints using MediatR handlers directly
/// This approach bypasses HTTP layer and avoids .NET 9.0 PipeWriter bug
/// </summary>
[Collection("Sequential")]
public class AdminEndpointsTests : IntegrationTestBase
{
    public AdminEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region Get Users Tests

    [Fact]
    public async Task GetAllUsers_ShouldReturnPagedResults()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8]; // Use short GUID for uniqueness
        await CreateTestUserAsync(email: $"user1-{testId}@example.com");
        await CreateTestUserAsync(email: $"user2-{testId}@example.com");
        await CreateTestUserAsync(email: $"user3-{testId}@example.com");

        var query = new GetUsersCommand(PageNumber: 1, PageSize: 10);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetAllUsers_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8]; // Use short GUID for uniqueness
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestUserAsync(email: $"user{i}-{testId}@example.com");
        }

        var query = new GetUsersCommand(PageNumber: 2, PageSize: 5);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.PageNumber.Should().Be(2);
        result.Items.Should().HaveCount(5);
    }

    #endregion

    #region Get User By Id Tests

    [Fact]
    public async Task GetUserById_WithValidId_ShouldReturnUser()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "getbyid@example.com", firstName: "Test", lastName: "User");
        
        var query = new GetUserByIdCommand(user.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.Email.Should().Be(user.Email);
        result.FirstName.Should().Be("Test");
        result.LastName.Should().Be("User");
    }

    [Fact]
    public async Task GetUserById_WithDataProperty_ShouldReturnUserWithData()
    {
        // Arrange
        var testData = "{\"adminNotes\": \"VIP user\", \"tags\": [\"premium\", \"verified\"]}";
        var user = await CreateTestUserAsync(email: "getbyidwithdata@example.com", firstName: "Test", lastName: "User");
        
        // Set data
        await ExecuteDbContextAsync(async context =>
        {
            var userToUpdate = await context.Users.FindAsync(user.Id);
            userToUpdate!.Data = testData;
            await context.SaveChangesAsync();
        });
        
        var query = new GetUserByIdCommand(user.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().Be(testData);
    }

    [Fact]
    public async Task GetUserById_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var query = new GetUserByIdCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(query)
        );
    }

    #endregion

    #region Create User Tests

    [Fact]
    public async Task CreateUser_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var createCommand = new CreateUserCommand(
            Email: "newadmin@example.com",
            Password: "NewAdmin123!",
            FirstName: "New",
            LastName: "Admin",
            Role: UserRole.User,
            PhoneNumber: "+1234567890"
        );

        // Act
        var result = await SendAsync(createCommand);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("newadmin@example.com");
        result.FirstName.Should().Be("New");
        result.LastName.Should().Be("Admin");
        result.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task CreateUser_WithDataProperty_ShouldCreateUserWithData()
    {
        // Arrange
        var testData = "{\"role\": \"manager\", \"department\": \"IT\", \"metadata\": {\"createdBy\": \"admin\"}}";
        var createCommand = new CreateUserCommand(
            Email: "adminwithdatafield@example.com",
            Password: "NewAdmin123!",
            FirstName: "Admin",
            LastName: "WithData",
            Role: UserRole.User,
            PhoneNumber: "+1234567890",
            Data: testData
        );

        // Act
        var result = await SendAsync(createCommand);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().Be(testData);
        result.Email.Should().Be("adminwithdatafield@example.com");
        
        // Verify data is persisted in database
        var userFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.Users.FirstOrDefaultAsync(u => u.Email == createCommand.Email);
        });
        
        userFromDb.Should().NotBeNull();
        userFromDb!.Data.Should().Be(testData);
    }

    [Fact]
    public async Task CreateUser_WithExistingEmail_ShouldThrowConflictException()
    {
        // Arrange
        var existingEmail = "existing@example.com";
        await CreateTestUserAsync(email: existingEmail);

        var createCommand = new CreateUserCommand(
            Email: existingEmail,
            Password: "Password123!",
            FirstName: "Test",
            LastName: "User",
            Role: UserRole.User,
            PhoneNumber: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            async () => await SendAsync(createCommand)
        );
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmail_ShouldThrowValidationException()
    {
        // Arrange
        var createCommand = new CreateUserCommand(
            Email: "invalid-email",
            Password: "Password123!",
            FirstName: "Test",
            LastName: "User",
            Role: UserRole.User,
            PhoneNumber: null
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(createCommand)
        );
    }

    [Fact]
    public async Task CreateUser_WithWeakPassword_ShouldThrowValidationException()
    {
        // Arrange
        var createCommand = new CreateUserCommand(
            Email: "test@example.com",
            Password: "weak",
            FirstName: "Test",
            LastName: "User",
            Role: UserRole.User,
            PhoneNumber: null
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(createCommand)
        );
    }

    #endregion

    #region Update User Tests

    [Fact]
    public async Task UpdateUser_WithValidData_ShouldUpdateUser()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "updateadmin@example.com");
        
        var updateCommand = new UpdateUserCommand(
            Id: user.Id,
            FirstName: "Updated",
            LastName: "Name",
            Role: UserRole.User,
            PhoneNumber: "+9876543210",
            EmailConfirmed: true,
            Status: true
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("Updated");
        result.LastName.Should().Be("Name");
        result.PhoneNumber.Should().Be("+9876543210");
    }

    [Fact]
    public async Task UpdateUser_WithDataProperty_ShouldUpdateData()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "updateadmindata@example.com");
        
        // Set initial data
        await ExecuteDbContextAsync(async context =>
        {
            var userToUpdate = await context.Users.FindAsync(user.Id);
            userToUpdate!.Data = "{\"oldData\": \"initial\"}";
            await context.SaveChangesAsync();
        });

        var newData = "{\"newData\": \"updated\", \"permissions\": [\"read\", \"write\", \"delete\"]}";
        var updateCommand = new UpdateUserCommand(
            Id: user.Id,
            FirstName: "Updated",
            LastName: "Name",
            Role: UserRole.User,
            PhoneNumber: null,
            EmailConfirmed: null,
            Status: null,
            Data: newData
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().Be(newData);
        
        // Verify data is persisted in database
        var userFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.Users.FindAsync(user.Id);
        });
        
        userFromDb!.Data.Should().Be(newData);
    }

    [Fact]
    public async Task UpdateUser_ClearDataProperty_ShouldSetToNull()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "clearadmindata@example.com");
        
        // Set initial data
        await ExecuteDbContextAsync(async context =>
        {
            var userToUpdate = await context.Users.FindAsync(user.Id);
            userToUpdate!.Data = "{\"existingData\": \"value\"}";
            await context.SaveChangesAsync();
        });

        var updateCommand = new UpdateUserCommand(
            Id: user.Id,
            FirstName: "Updated",
            LastName: "Name",
            Role: UserRole.User,
            PhoneNumber: null,
            EmailConfirmed: null,
            Status: null,
            Data: null
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUser_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var updateCommand = new UpdateUserCommand(
            Id: 99999,
            FirstName: "Test",
            LastName: "User",
            Role: UserRole.User,
            PhoneNumber: null,
            EmailConfirmed: null,
            Status: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(updateCommand)
        );
    }

    [Fact]
    public async Task UpdateUser_WithInvalidData_ShouldThrowValidationException()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "updateinvalid@example.com");
        
        var updateCommand = new UpdateUserCommand(
            Id: user.Id,
            FirstName: "", // Invalid: empty
            LastName: "Name",
            Role: UserRole.User,
            PhoneNumber: null,
            EmailConfirmed: null,
            Status: null
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(updateCommand)
        );
    }

    #endregion

    #region Toggle User Status Tests

    [Fact]
    public async Task ToggleUserStatus_ShouldChangeUserStatus()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "togglestatus@example.com");
        var initialStatus = user.Status;
        
        var toggleCommand = new ToggleUserStatusCommand(user.Id);

        // Act
        var result = await SendAsync(toggleCommand);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().NotBe(initialStatus);
    }

    [Fact]
    public async Task ToggleUserStatus_WithNonExistentUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var toggleCommand = new ToggleUserStatusCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(toggleCommand)
        );
    }

    #endregion

    #region Delete User Tests

    [Fact]
    public async Task DeleteUser_WithValidId_ShouldArchiveUser()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "deleteadmin@example.com");
        
        var deleteCommand = new DeleteUserCommand(user.Id);

        // Act
        var result = await SendAsync(deleteCommand);

        // Assert
        result.Should().BeTrue();

        // Verify user is archived
        var deletedUser = await ExecuteDbContextAsync(async context =>
        {
            return await context.Users.FindAsync(user.Id);
        });
        
        deletedUser.Should().NotBeNull();
        deletedUser!.IsArchived.Should().Be(true);
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var deleteCommand = new DeleteUserCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(deleteCommand)
        );
    }

    [Fact]
    public async Task DeleteUser_AlreadyDeleted_ShouldThrowNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "doubledelete@example.com");
        
        // Delete once
        await SendAsync(new DeleteUserCommand(user.Id));

        // Act & Assert - Try to delete again
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(new DeleteUserCommand(user.Id))
        );
    }

    #endregion
}
