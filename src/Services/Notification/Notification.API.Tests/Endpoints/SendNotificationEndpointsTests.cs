using Notification.API.Tests.Infrastructure;
using Notification.Application.Commands;
using Notification.Domain.Enums;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using FluentValidation;

namespace Notification.API.Tests.Endpoints;

/// <summary>
/// Integration tests for notification sending using MediatR handlers directly
/// This approach bypasses HTTP layer and avoids .NET 9.0 PipeWriter bug
/// 
/// Tests cover:
/// - SendNotification command (creates queue items in global database)
/// - Queue item validation
/// - Priority and delivery type handling
/// </summary>
[Collection("Sequential")]
public class SendNotificationEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    public SendNotificationEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
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

    #region SendNotification Tests

    [Fact]
    public async Task SendNotification_WithValidData_ShouldCreateQueueItem()
    {
        // Arrange
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "Test Notification",
            Message: "This is a test notification",
            Data: null,
            DeliveryType: "Both",
            Priority: "Immediate"
        );

        // Act - Call handler directly via MediatR
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.QueueItemId.Should().BeGreaterThan(0);
        result.Status.Should().Be("Queued");
        result.Priority.Should().Be("Immediate");
        result.DeliveryType.Should().Be("Both");
        DateTime.Parse(result.QueuedAt, null, System.Globalization.DateTimeStyles.RoundtripKind).Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify queue item was created in global database
        var queueItem = await ExecuteGlobalDbContextAsync(async context =>
            await context.NotificationQueue.FirstOrDefaultAsync(q => q.Id == result.QueueItemId)
        );

        queueItem.Should().NotBeNull();
        queueItem!.Title.Should().Be(command.Title);
        queueItem.Message.Should().Be(command.Message);
        queueItem.TenantId.Should().Be(command.TenantId);
        queueItem.UserId.Should().Be(command.UserId);
        queueItem.QueueStatus.Should().Be(QueueStatus.Pending);
    }

    [Fact]
    public async Task SendNotification_WithSignalROnly_ShouldCreateCorrectDeliveryType()
    {
        // Arrange
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "SignalR Notification",
            Message: "Test message",
            Data: null,
            DeliveryType: "SignalR",
            Priority: "Immediate"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryType.Should().Be("SignalR");

        var queueItem = await ExecuteGlobalDbContextAsync(async context =>
            await context.NotificationQueue.FindAsync(result.QueueItemId)
        );

        queueItem.Should().NotBeNull();
        queueItem!.DeliveryType.Should().Be(DeliveryType.SignalR);
    }

    [Fact]
    public async Task SendNotification_WithFirebaseOnly_ShouldCreateCorrectDeliveryType()
    {
        // Arrange
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "Firebase Notification",
            Message: "Test message",
            Data: null,
            DeliveryType: "Firebase",
            Priority: "Immediate"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryType.Should().Be("Firebase");

        var queueItem = await ExecuteGlobalDbContextAsync(async context =>
            await context.NotificationQueue.FindAsync(result.QueueItemId)
        );

        queueItem.Should().NotBeNull();
        queueItem!.DeliveryType.Should().Be(DeliveryType.Firebase);
    }

    [Fact]
    public async Task SendNotification_WithWaitablePriority_ShouldCreateWithCorrectPriority()
    {
        // Arrange
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "Waitable Notification",
            Message: "Low priority message",
            Data: null,
            DeliveryType: "Both",
            Priority: "Waitable"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Priority.Should().Be("Waitable");

        var queueItem = await ExecuteGlobalDbContextAsync(async context =>
            await context.NotificationQueue.FindAsync(result.QueueItemId)
        );

        queueItem.Should().NotBeNull();
        queueItem!.Priority.Should().Be(Priority.Waitable);
    }

    [Fact]
    public async Task SendNotification_WithJsonData_ShouldStoreDataCorrectly()
    {
        // Arrange
        var jsonData = "{\"orderId\": 123, \"status\": \"completed\"}";
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "Order Update",
            Message: "Your order is completed",
            Data: jsonData,
            DeliveryType: "Both",
            Priority: "Immediate"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();

        var queueItem = await ExecuteGlobalDbContextAsync(async context =>
            await context.NotificationQueue.FindAsync(result.QueueItemId)
        );

        queueItem.Should().NotBeNull();
        // Parse and compare JSON objects instead of strings (order doesn't matter)
        queueItem!.Data.Should().NotBeNullOrEmpty();
        var actualJson = System.Text.Json.JsonDocument.Parse(queueItem.Data!);
        
        // Compare individual properties instead of string representation (order doesn't matter in JSON)
        actualJson.RootElement.GetProperty("orderId").GetInt32().Should().Be(123);
        actualJson.RootElement.GetProperty("status").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task SendNotification_WithoutTenantId_ShouldSucceed()
    {
        // Arrange - Some notifications might not be tenant-specific
        var command = new SendNotificationCommand(
            TenantId: null,
            UserId: 1,
            Title: "Global Notification",
            Message: "System-wide notification",
            Data: null,
            DeliveryType: "Both",
            Priority: "Immediate"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.QueueItemId.Should().BeGreaterThan(0);

        var queueItem = await ExecuteGlobalDbContextAsync(async context =>
            await context.NotificationQueue.FindAsync(result.QueueItemId)
        );

        queueItem.Should().NotBeNull();
        queueItem!.TenantId.Should().BeNull();
    }

    [Fact]
    public async Task SendNotification_WithoutUserId_ShouldSucceed()
    {
        // Arrange - Broadcast notifications might not have specific user
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: null,
            Title: "Broadcast Notification",
            Message: "Message to all users",
            Data: null,
            DeliveryType: "Both",
            Priority: "Immediate"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.QueueItemId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SendNotification_WithEmptyTitle_ShouldThrowValidationException()
    {
        // Arrange
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "",
            Message: "Test message",
            Data: null,
            DeliveryType: "Both",
            Priority: "Immediate"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task SendNotification_WithTitleTooLong_ShouldThrowValidationException()
    {
        // Arrange
        var longTitle = new string('A', 201); // Title max length is 200
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: longTitle,
            Message: "Test message",
            Data: null,
            DeliveryType: "Both",
            Priority: "Immediate"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task SendNotification_WithMessageTooLong_ShouldThrowValidationException()
    {
        // Arrange
        var longMessage = new string('A', 1001); // Message max length is 1000
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "Test Title",
            Message: longMessage,
            Data: null,
            DeliveryType: "Both",
            Priority: "Immediate"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task SendNotification_WithInvalidDeliveryType_ShouldThrowValidationException()
    {
        // Arrange
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "Test",
            Message: "Test message",
            Data: null,
            DeliveryType: "Invalid",
            Priority: "Immediate"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task SendNotification_WithInvalidPriority_ShouldThrowValidationException()
    {
        // Arrange
        var command = new SendNotificationCommand(
            TenantId: "tenant1",
            UserId: 1,
            Title: "Test",
            Message: "Test message",
            Data: null,
            DeliveryType: "Both",
            Priority: "Invalid"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task SendNotification_MultipleTimes_ShouldCreateMultipleQueueItems()
    {
        // Arrange & Act
        var results = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            var command = new SendNotificationCommand(
                TenantId: "tenant1",
                UserId: 1,
                Title: $"Notification {i + 1}",
                Message: $"Message {i + 1}",
                Data: null,
                DeliveryType: "Both",
                Priority: "Immediate"
            );

            var result = await SendAsync(command);
            results.Add(result.QueueItemId);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyHaveUniqueItems();

        // Verify all items in database
        var queueItems = await ExecuteGlobalDbContextAsync(async context =>
            await context.NotificationQueue
                .Where(q => results.Contains(q.Id))
                .ToListAsync()
        );

        queueItems.Should().HaveCount(3);
    }

    #endregion
}
