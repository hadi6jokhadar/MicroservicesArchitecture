using Identity.API.Tests.Infrastructure;
using Identity.Application.Commands.DeviceToken;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.Endpoints;

/// <summary>
/// Integration tests for device token endpoints using MediatR handlers directly
/// This approach bypasses HTTP layer and avoids .NET 9.0 PipeWriter bug
/// </summary>
[Collection("Sequential")]
public class DeviceTokenEndpointsTests : IntegrationTestBase
{
    public DeviceTokenEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region Add Device Token Tests

    [Fact]
    public async Task AddDeviceToken_WithValidData_ShouldCreateToken()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "devicetoken@example.com");
        var command = new AddDeviceTokenCommand(
            UserId: user.Id,
            Token: "test-firebase-token-12345",
            Platform: Platform.Android,
            DeviceIdentifier: "device-001",
            IsPrimary: true
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.UserId.Should().Be(user.Id);
        result.Token.Should().Be("test-firebase-token-12345");
        result.Platform.Should().Be(Platform.Android);
        result.DeviceIdentifier.Should().Be("device-001");
        result.IsPrimary.Should().BeTrue();
        result.LastVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AddDeviceToken_WithDuplicateToken_ShouldUpdateExisting()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "duplicatetoken@example.com");
        var token = "duplicate-token-12345";

        // Add first token
        var command1 = new AddDeviceTokenCommand(
            UserId: user.Id,
            Token: token,
            Platform: Platform.Android,
            DeviceIdentifier: "device-001",
            IsPrimary: false
        );
        var result1 = await SendAsync(command1);

        // Add same token again with different device identifier
        var command2 = new AddDeviceTokenCommand(
            UserId: user.Id,
            Token: token,
            Platform: Platform.iOS,
            DeviceIdentifier: "device-002",
            IsPrimary: true
        );

        // Act
        var result2 = await SendAsync(command2);

        // Assert
        result2.Id.Should().Be(result1.Id); // Same ID, updated
        result2.Platform.Should().Be(Platform.iOS);
        result2.DeviceIdentifier.Should().Be("device-002");
        result2.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task AddDeviceToken_WithInvalidUserId_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new AddDeviceTokenCommand(
            UserId: 99999,
            Token: "test-token",
            Platform: Platform.Android
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task AddDeviceToken_AsPrimary_ShouldUnsetOtherPrimaryTokens()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var user = await CreateTestUserAsync(email: $"primarytoken-{testId}@example.com");

        // Add first token as primary
        var command1 = new AddDeviceTokenCommand(
            UserId: user.Id,
            Token: $"token-1-{testId}",
            Platform: Platform.Android,
            IsPrimary: true
        );
        var token1 = await SendAsync(command1);

        // Add second token as primary (same platform)
        var command2 = new AddDeviceTokenCommand(
            UserId: user.Id,
            Token: $"token-2-{testId}",
            Platform: Platform.Android,
            IsPrimary: true
        );

        // Act
        var token2 = await SendAsync(command2);

        // Assert
        token2.IsPrimary.Should().BeTrue();

        // Verify first token is no longer primary
        var query = new GetDeviceTokenByIdQuery(token1.Id);
        var updatedToken1 = await SendAsync(query);
        updatedToken1.Should().NotBeNull();
        updatedToken1!.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public async Task AddDeviceToken_MultipleDevicesPerUser_ShouldAllowMultipleTokens()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "multidevice@example.com");

        var command1 = new AddDeviceTokenCommand(user.Id, "android-token", Platform.Android);
        var command2 = new AddDeviceTokenCommand(user.Id, "ios-token", Platform.iOS);
        var command3 = new AddDeviceTokenCommand(user.Id, "web-token", Platform.Web);

        // Act
        await SendAsync(command1);
        await SendAsync(command2);
        await SendAsync(command3);

        var query = new GetUserDeviceTokensQuery(user.Id);
        var result = await SendAsync(query);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(t => t.Platform == Platform.Android);
        result.Should().Contain(t => t.Platform == Platform.iOS);
        result.Should().Contain(t => t.Platform == Platform.Web);
    }

    #endregion

    #region Get Device Token Tests

    [Fact]
    public async Task GetDeviceTokenById_WithValidId_ShouldReturnToken()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "gettoken@example.com");
        var addCommand = new AddDeviceTokenCommand(user.Id, "test-token", Platform.Android);
        var addedToken = await SendAsync(addCommand);

        var query = new GetDeviceTokenByIdQuery(addedToken.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(addedToken.Id);
        result.Token.Should().Be("test-token");
    }

    [Fact]
    public async Task GetDeviceTokenById_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var query = new GetDeviceTokenByIdQuery(99999);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserDeviceTokens_ShouldReturnAllUserTokens()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "getalltokens@example.com");
        await SendAsync(new AddDeviceTokenCommand(user.Id, "token-1", Platform.Android, IsPrimary: true));
        await SendAsync(new AddDeviceTokenCommand(user.Id, "token-2", Platform.iOS, IsPrimary: false));
        await SendAsync(new AddDeviceTokenCommand(user.Id, "token-3", Platform.Web, IsPrimary: false));

        var query = new GetUserDeviceTokensQuery(user.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().HaveCount(3);
        result[0].IsPrimary.Should().BeTrue(); // Primary should be first
    }

    [Fact]
    public async Task GetUserDeviceTokens_WithNoTokens_ShouldReturnEmptyList()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "notokens@example.com");
        var query = new GetUserDeviceTokensQuery(user.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDeviceTokensByPlatform_ShouldFilterByPlatform()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "filterplatform@example.com");
        await SendAsync(new AddDeviceTokenCommand(user.Id, "android-1", Platform.Android));
        await SendAsync(new AddDeviceTokenCommand(user.Id, "android-2", Platform.Android));
        await SendAsync(new AddDeviceTokenCommand(user.Id, "ios-1", Platform.iOS));

        var query = new GetUserDeviceTokensByPlatformQuery(user.Id, Platform.Android);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.Platform == Platform.Android);
    }

    [Fact]
    public async Task GetDeviceTokenByToken_WithValidToken_ShouldReturnToken()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "getbytoken@example.com");
        var token = "unique-search-token-123";
        await SendAsync(new AddDeviceTokenCommand(user.Id, token, Platform.Android));

        var query = new GetDeviceTokenByTokenQuery(token);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be(token);
    }

    #endregion

    #region Update Device Token Tests

    [Fact]
    public async Task UpdateDeviceToken_WithValidData_ShouldUpdateToken()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "updatetoken@example.com");
        var addCommand = new AddDeviceTokenCommand(user.Id, "original-token", Platform.Android);
        var addedToken = await SendAsync(addCommand);

        var updateCommand = new UpdateDeviceTokenCommand(
            Id: addedToken.Id,
            Token: "updated-token",
            DeviceIdentifier: "new-device-001",
            IsPrimary: true
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(addedToken.Id);
        result.Token.Should().Be("updated-token");
        result.DeviceIdentifier.Should().Be("new-device-001");
        result.IsPrimary.Should().BeTrue();
        result.LastVerifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateDeviceToken_PartialUpdate_ShouldOnlyUpdateProvidedFields()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "partialupdate@example.com");
        var addCommand = new AddDeviceTokenCommand(user.Id, "original-token", Platform.Android, "device-001");
        var addedToken = await SendAsync(addCommand);

        var updateCommand = new UpdateDeviceTokenCommand(
            Id: addedToken.Id,
            DeviceIdentifier: "updated-device"
        );

        // Act
        var result = await SendAsync(updateCommand);

        // Assert
        result.Token.Should().Be("original-token"); // Unchanged
        result.DeviceIdentifier.Should().Be("updated-device"); // Updated
    }

    [Fact]
    public async Task UpdateDeviceToken_SetAsPrimary_ShouldUnsetOtherPrimary()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "updateprimary@example.com");
        var token1 = await SendAsync(new AddDeviceTokenCommand(user.Id, "token-1", Platform.Android, IsPrimary: true));
        var token2 = await SendAsync(new AddDeviceTokenCommand(user.Id, "token-2", Platform.Android, IsPrimary: false));

        var updateCommand = new UpdateDeviceTokenCommand(Id: token2.Id, IsPrimary: true);

        // Act
        await SendAsync(updateCommand);

        // Assert
        var updatedToken1 = await SendAsync(new GetDeviceTokenByIdQuery(token1.Id));
        updatedToken1!.IsPrimary.Should().BeFalse();

        var updatedToken2 = await SendAsync(new GetDeviceTokenByIdQuery(token2.Id));
        updatedToken2!.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDeviceToken_WithInvalidId_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new UpdateDeviceTokenCommand(Id: 99999, Token: "new-token");

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () => await SendAsync(command));
    }

    #endregion

    #region Delete Device Token Tests

    [Fact]
    public async Task DeleteDeviceToken_WithValidId_ShouldSoftDeleteToken()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var user = await CreateTestUserAsync(email: $"deletetoken-{testId}@example.com");
        var addCommand = new AddDeviceTokenCommand(user.Id, $"token-to-delete-{testId}", Platform.Android);
        var addedToken = await SendAsync(addCommand);

        var deleteCommand = new DeleteDeviceTokenCommand(addedToken.Id);

        // Act
        var result = await SendAsync(deleteCommand);

        // Assert
        result.Should().BeTrue();

        // Verify token is soft deleted (returns null from query)
        var getQuery = new GetDeviceTokenByIdQuery(addedToken.Id);
        var deletedToken = await SendAsync(getQuery);
        deletedToken.Should().BeNull();

        // Verify it's still in database but archived
        await ExecuteDbContextAsync(async context =>
        {
            var dbToken = await context.DeviceTokens
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == addedToken.Id);
            dbToken.Should().NotBeNull();
            dbToken!.IsArchived.Should().BeTrue();
        });
    }

    [Fact]
    public async Task DeleteDeviceToken_WithInvalidId_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new DeleteDeviceTokenCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task DeleteAllUserDeviceTokens_ShouldDeleteAllUserTokens()
    {
        // Arrange
        var user = await CreateTestUserAsync(email: "deletealltokens@example.com");
        await SendAsync(new AddDeviceTokenCommand(user.Id, "token-1", Platform.Android));
        await SendAsync(new AddDeviceTokenCommand(user.Id, "token-2", Platform.iOS));
        await SendAsync(new AddDeviceTokenCommand(user.Id, "token-3", Platform.Web));

        var deleteCommand = new DeleteAllUserDeviceTokensCommand(user.Id);

        // Act
        var result = await SendAsync(deleteCommand);

        // Assert
        result.Should().BeTrue();

        // Verify all tokens are deleted
        var query = new GetUserDeviceTokensQuery(user.Id);
        var tokens = await SendAsync(query);
        tokens.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllUserDeviceTokens_WithInvalidUserId_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new DeleteAllUserDeviceTokensCommand(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task DeleteAllUserDeviceTokens_ShouldNotAffectOtherUsers()
    {
        // Arrange
        var user1 = await CreateTestUserAsync(email: "user1deleteall@example.com");
        var user2 = await CreateTestUserAsync(email: "user2deleteall@example.com");

        await SendAsync(new AddDeviceTokenCommand(user1.Id, "user1-token", Platform.Android));
        await SendAsync(new AddDeviceTokenCommand(user2.Id, "user2-token", Platform.Android));

        var deleteCommand = new DeleteAllUserDeviceTokensCommand(user1.Id);

        // Act
        await SendAsync(deleteCommand);

        // Assert
        var user1Tokens = await SendAsync(new GetUserDeviceTokensQuery(user1.Id));
        user1Tokens.Should().BeEmpty();

        var user2Tokens = await SendAsync(new GetUserDeviceTokensQuery(user2.Id));
        user2Tokens.Should().HaveCount(1);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(0, "token", Platform.Android)] // Invalid UserId
    [InlineData(-1, "token", Platform.Android)] // Negative UserId
    public async Task AddDeviceToken_WithInvalidUserId_ShouldFailValidation(int userId, string token, Platform platform)
    {
        // Arrange
        var command = new AddDeviceTokenCommand(userId, token, platform);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(command));
    }

    [Theory]
    [InlineData("")] // Empty token
    [InlineData(null)] // Null token
    public async Task AddDeviceToken_WithInvalidToken_ShouldFailValidation(string? token)
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var command = new AddDeviceTokenCommand(user.Id, token!, Platform.Android);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task AddDeviceToken_WithTokenTooLong_ShouldFailValidation()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var longToken = new string('a', 501); // Max is 500
        var command = new AddDeviceTokenCommand(user.Id, longToken, Platform.Android);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task AddDeviceToken_WithDeviceIdentifierTooLong_ShouldFailValidation()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var longIdentifier = new string('b', 101); // Max is 100
        var command = new AddDeviceTokenCommand(user.Id, "token", Platform.Android, longIdentifier);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(command));
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public async Task DeviceTokens_ShouldSupportMultiplePlatformsPerUser()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var user = await CreateTestUserAsync(email: $"multiplatform-{testId}@example.com");

        // Act
        var androidToken = await SendAsync(new AddDeviceTokenCommand(user.Id, $"android-token-{testId}", Platform.Android, IsPrimary: true));
        var iosToken = await SendAsync(new AddDeviceTokenCommand(user.Id, $"ios-token-{testId}", Platform.iOS, IsPrimary: true));
        var webToken = await SendAsync(new AddDeviceTokenCommand(user.Id, $"web-token-{testId}", Platform.Web, IsPrimary: true));

        // Assert - Each platform can have its own primary
        androidToken.IsPrimary.Should().BeTrue();
        iosToken.IsPrimary.Should().BeTrue();
        webToken.IsPrimary.Should().BeTrue();

        var allTokens = await SendAsync(new GetUserDeviceTokensQuery(user.Id));
        allTokens.Should().HaveCount(3);
        allTokens.Count(t => t.IsPrimary).Should().Be(3); // Each platform has primary
    }

    [Fact]
    public async Task DeviceTokens_OrderedByPrimaryFirst_ThenByCreatedDesc()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var user = await CreateTestUserAsync(email: $"tokenorder-{testId}@example.com");

        // Add tokens with delays to ensure different Created times
        var token1 = await SendAsync(new AddDeviceTokenCommand(user.Id, $"token-1-{testId}", Platform.Android, IsPrimary: false));
        await Task.Delay(10);
        var token2 = await SendAsync(new AddDeviceTokenCommand(user.Id, $"token-2-{testId}", Platform.Android, IsPrimary: false));
        await Task.Delay(10);
        var token3 = await SendAsync(new AddDeviceTokenCommand(user.Id, $"token-3-{testId}", Platform.Android, IsPrimary: true));

        // Act
        var result = await SendAsync(new GetUserDeviceTokensQuery(user.Id));

        // Assert
        result.Should().HaveCount(3);
        result[0].IsPrimary.Should().BeTrue(); // Primary first
        result[0].Id.Should().Be(token3.Id);
        result[1].Id.Should().Be(token2.Id); // Then by Created desc
        result[2].Id.Should().Be(token1.Id);
    }

    #endregion
}
