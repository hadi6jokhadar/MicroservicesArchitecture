using Identity.API.Tests.Infrastructure;
using Identity.Application.Commands;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Kernel.Enums.Identity;

namespace Identity.API.Tests.Endpoints;

/// <summary>
/// Integration tests for user profile using MediatR handlers directly
/// This approach bypasses HTTP layer and avoids .NET 9.0 PipeWriter bug
/// </summary>
[Collection("Sequential")]
public class UserEndpointsTests : IntegrationTestBase
{
    public UserEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region Get Profile Tests

    [Fact]
    public async Task GetProfile_WithValidUser_ShouldReturnUserProfile()
    {
        // Arrange
        var email = "profiletest@example.com";
        var password = "Profile123!";
        var user = await CreateTestUserAsync(email: email, password: password, firstName: "John", lastName: "Doe");
        
        var command = new GetUserProfileCommand(user.Id);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(email);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetProfile_WithDataProperty_ShouldReturnData()
    {
        // Arrange
        var email = "profilewithdatatest@example.com";
        var password = "Profile123!";
        var testData = "{\"userPreferences\": {\"language\": \"en\", \"timezone\": \"UTC\"}}";
        var user = await CreateTestUserAsync(email: email, password: password, firstName: "John", lastName: "Doe");
        
        // Set data
        await ExecuteDbContextAsync(async context =>
        {
            var userToUpdate = await context.Users.FindAsync(user.Id);
            userToUpdate!.Data = testData;
            await context.SaveChangesAsync();
        });
        
        var command = new GetUserProfileCommand(user.Id);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().Be(testData);
    }

    [Fact]
    public async Task GetProfile_WithNonExistentUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new GetUserProfileCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command)
        );
    }

    #endregion

    #region Update Profile Tests

    [Fact]
    public async Task UpdateProfile_WithValidData_ShouldReturnUpdatedProfile()
    {
        // Arrange
        var email = "updatetest@example.com";
        var password = "Update123!";
        var user = await CreateTestUserAsync(email: email, password: password, firstName: "Old", lastName: "Name");

        var updateRequest = new UpdateProfileCommand(
            FirstName: "New",
            LastName: "UpdatedName",
            PhoneNumber: "+1234567890",
            ProfilePictureUrl: null,
            Id: user.Id
        );

        // Act
        var result = await SendAsync(updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("New");
        result.LastName.Should().Be("UpdatedName");
        result.PhoneNumber.Should().Be("+1234567890");
    }

    [Fact]
    public async Task UpdateProfile_WithDataProperty_ShouldUpdateData()
    {
        // Arrange
        var email = "updatedatatest@example.com";
        var password = "Update123!";
        var initialData = "{\"version\": \"1.0\"}";
        var user = await CreateTestUserAsync(email: email, password: password, firstName: "Test", lastName: "User");
        
        // Set initial data
        await ExecuteDbContextAsync(async context =>
        {
            var userToUpdate = await context.Users.FindAsync(user.Id);
            userToUpdate!.Data = initialData;
            await context.SaveChangesAsync();
        });

        var updatedData = "{\"version\": \"2.0\", \"settings\": {\"notifications\": true}}";
        var updateRequest = new UpdateProfileCommand(
            FirstName: "Test",
            LastName: "User",
            PhoneNumber: null,
            ProfilePictureUrl: null,
            Id: user.Id,
            Data: updatedData
        );

        // Act
        var result = await SendAsync(updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().Be(updatedData);
        
        // Verify data is persisted in database
        var userFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.Users.FindAsync(user.Id);
        });
        
        userFromDb!.Data.Should().Be(updatedData);
    }

    [Fact]
    public async Task UpdateProfile_ClearDataProperty_ShouldSetToNull()
    {
        // Arrange
        var email = "cleardatatest@example.com";
        var password = "Update123!";
        var user = await CreateTestUserAsync(email: email, password: password, firstName: "Test", lastName: "User");
        
        // Set initial data
        await ExecuteDbContextAsync(async context =>
        {
            var userToUpdate = await context.Users.FindAsync(user.Id);
            userToUpdate!.Data = "{\"oldData\": \"value\"}";
            await context.SaveChangesAsync();
        });

        var updateRequest = new UpdateProfileCommand(
            FirstName: "Test",
            LastName: "User",
            PhoneNumber: null,
            ProfilePictureUrl: null,
            Id: user.Id,
            Data: null
        );

        // Act
        var result = await SendAsync(updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfile_WithInvalidData_ShouldThrowValidationException()
    {
        // Arrange
        var email = "updateinvalid@example.com";
        var password = "Update123!";
        var user = await CreateTestUserAsync(email: email, password: password);

        var updateRequest = new UpdateProfileCommand(
            FirstName: "", // Invalid: empty first name
            LastName: "Name",
            PhoneNumber: null,
            ProfilePictureUrl: null,
            Id: user.Id
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(updateRequest)
        );
    }

    [Fact]
    public async Task UpdateProfile_WithNonExistentUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var updateRequest = new UpdateProfileCommand(
            FirstName: "New",
            LastName: "Name",
            PhoneNumber: null,
            ProfilePictureUrl: null,
            Id: 99999
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(updateRequest)
        );
    }

    #endregion

    #region Delete User Tests

    [Fact]
    public async Task DeleteUser_WithValidUser_ShouldDeleteAccount()
    {
        // Arrange
        var email = "deletetest@example.com";
        var password = "Delete123!";
        var user = await CreateTestUserAsync(email: email, password: password);
        
        var deleteCommand = new DeleteUserCommand(user.Id);

        // Act
        var result = await SendAsync(deleteCommand);

        // Assert
        result.Should().BeTrue();

        // Verify user is deleted/archived
        var deletedUser = await ExecuteDbContextAsync(async context =>
        {
            return await context.Users.FindAsync(user.Id);
        });
        
        deletedUser.Should().NotBeNull(); // User exists but should be archived
        deletedUser!.IsArchived.Should().Be(true); // Should be soft deleted
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var deleteCommand = new DeleteUserCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(deleteCommand)
        );
    }

    [Fact]
    public async Task DeleteUser_AfterDeletion_ShouldNotBeAbleToLogin()
    {
        // Arrange
        var email = "deletelogintest@example.com";
        var password = "Delete123!";
        var user = await CreateTestUserAsync(email: email, password: password);
        
        // Delete the account
        await SendAsync(new DeleteUserCommand(user.Id));

        // Act & Assert - Try to login with deleted account
        var loginCommand = new LoginCommand(email, password);
        await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(loginCommand)
        );
    }

    #endregion
}
