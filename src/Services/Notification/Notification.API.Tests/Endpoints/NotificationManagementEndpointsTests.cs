using Notification.API.Tests.Infrastructure;
using Notification.Application.Commands;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using FluentValidation;

namespace Notification.API.Tests.Endpoints;

/// <summary>
/// Integration tests for notification management operations
/// 
/// Tests cover:
/// - MarkNotificationAsRead command (updates in tenant-specific database)
/// - AcknowledgeNotification command (updates in global queue database)
/// - User authorization checks
/// </summary>
[Collection("Sequential")]
public class NotificationManagementEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    public NotificationManagementEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
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

    #region MarkNotificationAsRead Tests

    [Fact]
    public async Task MarkNotificationAsRead_WithValidData_ShouldMarkAsRead()
    {
        // Arrange
        var userId = 1;
        var notification = await CreateTestNotificationAsync(userId, "Test", "Message", isRead: false);

        var command = new MarkNotificationAsReadCommand(
            NotificationId: notification.Id,
            UserId: userId
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeTrue();

        // Verify notification is marked as read in tenant database
        var updatedNotification = await ExecuteTenantDbContextAsync(async context =>
            await context.Notifications.FindAsync(notification.Id)
        );

        updatedNotification.Should().NotBeNull();
        updatedNotification!.IsRead.Should().BeTrue();
        updatedNotification.ReadAt.Should().NotBeNull();
        updatedNotification.ReadAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkNotificationAsRead_AlreadyRead_ShouldStillReturnTrue()
    {
        // Arrange
        var userId = 1;
        var notification = await CreateTestNotificationAsync(userId, "Test", "Message", isRead: true);

        var command = new MarkNotificationAsReadCommand(
            NotificationId: notification.Id,
            UserId: userId
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task MarkNotificationAsRead_WrongUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var ownerId = 1;
        var otherUserId = 2;
        var notification = await CreateTestNotificationAsync(ownerId, "Test", "Message");

        var command = new MarkNotificationAsReadCommand(
            NotificationId: notification.Id,
            UserId: otherUserId // Different user
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task MarkNotificationAsRead_NonExistentNotification_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new MarkNotificationAsReadCommand(
            NotificationId: 99999, // Non-existent
            UserId: 1
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task MarkNotificationAsRead_WithInvalidNotificationId_ShouldThrowValidationException()
    {
        // Arrange
        var command = new MarkNotificationAsReadCommand(
            NotificationId: 0,
            UserId: 1
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task MarkNotificationAsRead_WithInvalidUserId_ShouldThrowValidationException()
    {
        // Arrange
        var command = new MarkNotificationAsReadCommand(
            NotificationId: 1,
            UserId: 0
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task MarkNotificationAsRead_MultipleNotifications_ShouldMarkEachIndependently()
    {
        // Arrange
        var userId = 1;
        var notification1 = await CreateTestNotificationAsync(userId, "Test 1", "Message 1", isRead: false);
        var notification2 = await CreateTestNotificationAsync(userId, "Test 2", "Message 2", isRead: false);

        // Act - Mark first notification as read
        var command1 = new MarkNotificationAsReadCommand(notification1.Id, userId);
        var result1 = await SendAsync(command1);

        // Assert - Check first is read, second is still unread
        result1.Should().BeTrue();

        var notifications = await ExecuteTenantDbContextAsync(async context =>
            await context.Notifications
                .Where(n => n.UserId == userId)
                .ToListAsync()
        );

        notifications.Should().HaveCount(2);
        notifications.First(n => n.Id == notification1.Id).IsRead.Should().BeTrue();
        notifications.First(n => n.Id == notification2.Id).IsRead.Should().BeFalse();
    }

    #endregion

    #region AcknowledgeNotification Tests

    [Fact]
    public async Task AcknowledgeNotification_WithValidQueueItemId_ShouldReturnTrue()
    {
        // Arrange
        var queueItem = await CreateTestQueueItemAsync(
            tenantId: "tenant1",
            userId: 1,
            title: "Test Notification"
        );

        var command = new AcknowledgeNotificationCommand(
            QueueItemId: queueItem.Id,
            ConnectionId: "test-connection-id",
            ReceivedAt: DateTime.UtcNow
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AcknowledgeNotification_WithoutOptionalFields_ShouldSucceed()
    {
        // Arrange
        var queueItem = await CreateTestQueueItemAsync(
            tenantId: "tenant1",
            userId: 1,
            title: "Test Notification"
        );

        var command = new AcknowledgeNotificationCommand(
            QueueItemId: queueItem.Id,
            ConnectionId: null,
            ReceivedAt: null
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AcknowledgeNotification_WithInvalidQueueItemId_ShouldThrowValidationException()
    {
        // Arrange
        var command = new AcknowledgeNotificationCommand(
            QueueItemId: 0,
            ConnectionId: "test-connection-id",
            ReceivedAt: DateTime.UtcNow
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task AcknowledgeNotification_NonExistentQueueItem_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new AcknowledgeNotificationCommand(
            QueueItemId: 99999, // Non-existent
            ConnectionId: "test-connection-id",
            ReceivedAt: DateTime.UtcNow
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command)
        );
    }

    #endregion

    #region GetQueueItemStatus Tests

    [Fact]
    public async Task GetQueueItemStatus_WithValidId_ShouldReturnStatus()
    {
        // Arrange
        var queueItem = await CreateTestQueueItemAsync(
            tenantId: "tenant1",
            userId: 1,
            title: "Test Notification",
            status: Domain.Enums.QueueStatus.Processing
        );

        var command = new GetQueueItemStatusCommand(QueueItemId: queueItem.Id);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.QueueItemId.Should().Be(queueItem.Id);
        result.Status.Should().Be("Processing");
    }

    [Fact]
    public async Task GetQueueItemStatus_WithSentItem_ShouldShowSentStatus()
    {
        // Arrange
        var queueItem = await CreateTestQueueItemAsync(
            tenantId: "tenant1",
            userId: 1,
            title: "Test Notification",
            status: Domain.Enums.QueueStatus.Sent
        );

        var command = new GetQueueItemStatusCommand(QueueItemId: queueItem.Id);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task GetQueueItemStatus_WithFailedItem_ShouldShowFailedStatus()
    {
        // Arrange
        var queueItem = await CreateTestQueueItemAsync(
            tenantId: "tenant1",
            userId: 1,
            title: "Test Notification",
            status: Domain.Enums.QueueStatus.Failed
        );

        var command = new GetQueueItemStatusCommand(QueueItemId: queueItem.Id);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task GetQueueItemStatus_NonExistentItem_ShouldReturnNull()
    {
        // Arrange
        var command = new GetQueueItemStatusCommand(QueueItemId: 99999);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetQueueItemStatus_WithInvalidId_ShouldThrowValidationException()
    {
        // Arrange
        var command = new GetQueueItemStatusCommand(QueueItemId: 0);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    #endregion
}
