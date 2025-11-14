using Notification.API.Tests.Infrastructure;
using Notification.Application.Commands;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using FluentValidation;

namespace Notification.API.Tests.Endpoints;

/// <summary>
/// Integration tests for user notification retrieval using MediatR handlers directly
/// 
/// Tests cover:
/// - GetUserNotifications command (reads from tenant-specific database)
/// - Pagination
/// - Filtering (read/unread)
/// - User isolation
/// </summary>
[Collection("Sequential")]
public class UserNotificationsEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    public UserNotificationsEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    public async Task InitializeAsync()
    {
        // Clean database before each test to prevent data accumulation
        await CleanupAllTestDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    #region GetUserNotifications Tests

    [Fact]
    public async Task GetUserNotifications_WithValidUserId_ShouldReturnUserNotifications()
    {
        // Arrange
        var userId = 1;
        
        // Create test notifications in tenant database
        await CreateTestNotificationAsync(userId, "Notification 1", "Message 1");
        await CreateTestNotificationAsync(userId, "Notification 2", "Message 2");
        await CreateTestNotificationAsync(userId, "Notification 3", "Message 3");

        var command = new GetUserNotificationsCommand(
            UserId: userId,
            UnreadOnly: null,
            Skip: 0,
            Take: 20
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().OnlyContain(n => n.UserId == userId);
        result.Should().BeInDescendingOrder(n => n.CreatedAt);
    }

    [Fact]
    public async Task GetUserNotifications_WithUnreadOnlyTrue_ShouldReturnOnlyUnreadNotifications()
    {
        // Arrange
        var userId = 1;
        
        // Create both read and unread notifications
        await CreateTestNotificationAsync(userId, "Unread 1", "Message 1", isRead: false);
        await CreateTestNotificationAsync(userId, "Read 1", "Message 2", isRead: true);
        await CreateTestNotificationAsync(userId, "Unread 2", "Message 3", isRead: false);
        await CreateTestNotificationAsync(userId, "Read 2", "Message 4", isRead: true);

        var command = new GetUserNotificationsCommand(
            UserId: userId,
            UnreadOnly: true,
            Skip: 0,
            Take: 20
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(n => !n.IsRead);
        result.Select(n => n.Title).Should().Contain(new[] { "Unread 1", "Unread 2" });
    }

    [Fact]
    public async Task GetUserNotifications_WithUnreadOnlyFalse_ShouldReturnAllNotifications()
    {
        // Arrange
        var userId = 1;
        
        await CreateTestNotificationAsync(userId, "Unread", "Message 1", isRead: false);
        await CreateTestNotificationAsync(userId, "Read", "Message 2", isRead: true);

        var command = new GetUserNotificationsCommand(
            UserId: userId,
            UnreadOnly: false,
            Skip: 0,
            Take: 20
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserNotifications_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var userId = 1;
        
        // Create 10 notifications
        for (int i = 1; i <= 10; i++)
        {
            await CreateTestNotificationAsync(userId, $"Notification {i}", $"Message {i}");
        }

        var command = new GetUserNotificationsCommand(
            UserId: userId,
            UnreadOnly: null,
            Skip: 5,
            Take: 3
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUserNotifications_WithTakeExceedingLimit_ShouldThrowValidationException()
    {
        // Arrange
        var command = new GetUserNotificationsCommand(
            UserId: 1,
            UnreadOnly: null,
            Skip: 0,
            Take: 101 // Max is 100
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task GetUserNotifications_WithNegativeSkip_ShouldThrowValidationException()
    {
        // Arrange
        var command = new GetUserNotificationsCommand(
            UserId: 1,
            UnreadOnly: null,
            Skip: -1,
            Take: 20
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task GetUserNotifications_WithZeroTake_ShouldThrowValidationException()
    {
        // Arrange
        var command = new GetUserNotificationsCommand(
            UserId: 1,
            UnreadOnly: null,
            Skip: 0,
            Take: 0
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task GetUserNotifications_WithInvalidUserId_ShouldThrowValidationException()
    {
        // Arrange
        var command = new GetUserNotificationsCommand(
            UserId: 0,
            UnreadOnly: null,
            Skip: 0,
            Take: 20
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task GetUserNotifications_ForUserWithNoNotifications_ShouldReturnEmptyList()
    {
        // Arrange
        var userId = 999; // User with no notifications
        var command = new GetUserNotificationsCommand(
            UserId: userId,
            UnreadOnly: null,
            Skip: 0,
            Take: 20
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserNotifications_ShouldIsolateUserNotifications()
    {
        // Arrange
        var user1Id = 1;
        var user2Id = 2;
        
        // Create notifications for different users
        await CreateTestNotificationAsync(user1Id, "User 1 Notification", "Message for user 1");
        await CreateTestNotificationAsync(user2Id, "User 2 Notification", "Message for user 2");

        var command = new GetUserNotificationsCommand(
            UserId: user1Id,
            UnreadOnly: null,
            Skip: 0,
            Take: 20
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().OnlyContain(n => n.UserId == user1Id);
        result.First().Title.Should().Be("User 1 Notification");
    }

    [Fact]
    public async Task GetUserNotifications_WithJsonData_ShouldReturnDataField()
    {
        // Arrange
        var userId = 1;
        var jsonData = "{\"type\": \"order\", \"id\": 123}";
        
        // Create notification with data
        await ExecuteTenantDbContextAsync(async context =>
        {
            var notification = new Domain.Entities.Notification
            {
                UserId = userId,
                Title = "Notification with data",
                Message = "Test message",
                Data = jsonData,
                IsRead = false,
                Created = DateTime.UtcNow
            };
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
        });

        var command = new GetUserNotificationsCommand(
            UserId: userId,
            UnreadOnly: null,
            Skip: 0,
            Take: 20
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        // Parse and compare JSON objects instead of strings (order doesn't matter)
        result.First().Data.Should().NotBeNullOrEmpty();
        var expectedJson = System.Text.Json.JsonDocument.Parse(jsonData);
        var actualJson = System.Text.Json.JsonDocument.Parse(result.First().Data!);
        
        // Compare JSON properties instead of string representation
        actualJson.RootElement.GetProperty("type").GetString().Should().Be("order");
        actualJson.RootElement.GetProperty("id").GetInt32().Should().Be(123);
    }

    [Fact]
    public async Task GetUserNotifications_ShouldOrderByCreatedDateDescending()
    {
        // Arrange
        var userId = 1;
        
        // Note: BaseDbContext.SaveChangesAsync overwrites the Created field with DateTime.UtcNow,
        // so we need to save each notification separately with a delay to ensure different timestamps
        
        // Create oldest notification first
        await ExecuteTenantDbContextAsync(async context =>
        {
            context.Notifications.Add(new Domain.Entities.Notification
            {
                UserId = userId,
                Title = "Old Notification",
                IsRead = false
            });
            await context.SaveChangesAsync();
        });
        
        // Delay to ensure different timestamp
        await Task.Delay(50);
        
        // Create middle notification
        await ExecuteTenantDbContextAsync(async context =>
        {
            context.Notifications.Add(new Domain.Entities.Notification
            {
                UserId = userId,
                Title = "Recent Notification",
                IsRead = false
            });
            await context.SaveChangesAsync();
        });
        
        // Delay to ensure different timestamp
        await Task.Delay(50);
        
        // Create newest notification
        await ExecuteTenantDbContextAsync(async context =>
        {
            context.Notifications.Add(new Domain.Entities.Notification
            {
                UserId = userId,
                Title = "Newest Notification",
                IsRead = false
            });
            await context.SaveChangesAsync();
        });

        var command = new GetUserNotificationsCommand(
            UserId: userId,
            UnreadOnly: null,
            Skip: 0,
            Take: 20
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Newest Notification");
        result[1].Title.Should().Be("Recent Notification");
        result[2].Title.Should().Be("Old Notification");
    }

    #endregion
}
