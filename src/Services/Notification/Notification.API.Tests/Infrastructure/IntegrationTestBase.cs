using System.Net.Http.Headers;
using Notification.Application.Commands;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Notification.API.Tests.Infrastructure;

/// <summary>
/// Base class for Notification API integration tests
/// Inherits from shared testing base and adds Notification-specific helpers
/// 
/// IMPORTANT: Notification Service uses TWO databases:
/// 1. NotificationDbContext - Global queue database (for queue items)
/// 2. TenantNotificationDbContext - Tenant-specific database (for notification history)
///
/// Database is created fresh by CustomWebApplicationFactory.ConfigureWebHost using EnsureCreated().
/// Tests should be independent and not rely on database cleanup between tests.
/// </summary>
public abstract class IntegrationTestBase : 
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<NotificationDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ============================================
    // Global Queue Database Operations
    // ============================================
    
    /// <summary>
    /// Execute operations on the global queue database (NotificationDbContext)
    /// This database stores NotificationQueueItem entities shared across all tenants
    /// </summary>
    protected async Task ExecuteGlobalDbContextAsync(Func<NotificationDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await action(context);
    }

    /// <summary>
    /// Execute operations on the global queue database with return value
    /// </summary>
    protected async Task<T> ExecuteGlobalDbContextAsync<T>(Func<NotificationDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        return await action(context);
    }

    // ============================================
    // Tenant-Specific Database Operations
    // ============================================
    
    /// <summary>
    /// Execute operations on the tenant-specific database (TenantNotificationDbContext)
    /// This database stores Notification entities specific to each tenant
    /// In testing mode (multi-tenancy disabled), this uses the same test database
    /// </summary>
    protected async Task ExecuteTenantDbContextAsync(Func<TenantNotificationDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TenantNotificationDbContext>();
        await action(context);
    }

    /// <summary>
    /// Execute operations on the tenant-specific database with return value
    /// </summary>
    protected async Task<T> ExecuteTenantDbContextAsync<T>(Func<TenantNotificationDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TenantNotificationDbContext>();
        return await action(context);
    }

    // ============================================
    // Test Data Creation Helpers
    // ============================================
    
    /// <summary>
    /// Create a test notification queue item in the global database
    /// </summary>
    protected async Task<NotificationQueueItem> CreateTestQueueItemAsync(
        string? tenantId = null,
        int? userId = null,
        string title = "Test Notification",
        string? message = "Test message",
        DeliveryType deliveryType = DeliveryType.Both,
        Priority priority = Priority.Immediate,
        QueueStatus status = QueueStatus.Pending)
    {
        return await ExecuteGlobalDbContextAsync(async context =>
        {
            var queueItem = new NotificationQueueItem
            {
                TenantId = tenantId,
                UserId = userId ?? 1, // Default test user ID
                Title = title,
                Message = message,
                Data = null,
                DeliveryType = deliveryType,
                Priority = priority,
                QueueStatus = status,
                RetryCount = 0,
                Created = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };

            context.NotificationQueue.Add(queueItem);
            await context.SaveChangesAsync();
            return queueItem;
        });
    }

    /// <summary>
    /// Create a test notification in the tenant-specific database
    /// </summary>
    protected async Task<Domain.Entities.Notification> CreateTestNotificationAsync(
        int userId,
        string title = "Test Notification",
        string? message = "Test message",
        bool isRead = false)
    {
        return await ExecuteTenantDbContextAsync(async context =>
        {
            var notification = new Domain.Entities.Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Data = null,
                IsRead = isRead,
                Created = DateTime.UtcNow
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
            return notification;
        });
    }

    /// <summary>
    /// Create multiple test queue items for batch testing
    /// </summary>
    protected async Task<List<NotificationQueueItem>> CreateTestQueueItemsAsync(
        int count,
        string? tenantId = null,
        int? userId = null,
        Priority priority = Priority.Immediate)
    {
        var items = new List<NotificationQueueItem>();
        
        for (int i = 0; i < count; i++)
        {
            var item = await CreateTestQueueItemAsync(
                tenantId: tenantId,
                userId: userId,
                title: $"Test Notification {i + 1}",
                message: $"Test message {i + 1}",
                priority: priority
            );
            items.Add(item);
        }
        
        return items;
    }

    // ============================================
    // Authentication Helpers
    // ============================================
    
    /// <summary>
    /// Set authentication token for API requests
    /// Note: For Notification service, tokens are typically issued by Identity service
    /// In tests, we can mock the token or create test tokens
    /// </summary>
    protected void SetTestAuthToken(int userId = 1, string? tenantId = null)
    {
        // In a real scenario, you'd generate a proper JWT token
        // For now, we can use SendAsync to bypass HTTP layer and test handlers directly
        // If HTTP testing is needed, implement JWT token generation here
        
        // Example placeholder:
        // var token = GenerateTestJwtToken(userId, tenantId);
        // SetAuthorizationHeader(token);
    }

    // ============================================
    // Cleanup Helpers (Optional - Not Required for Tests)
    // ============================================
    
    /// <summary>
    /// Clean up all queue items from global database
    /// NOTE: Not typically needed - database is recreated fresh for each test run
    /// </summary>
    protected async Task CleanupGlobalQueueAsync()
    {
        await ExecuteGlobalDbContextAsync(async context =>
        {
            context.NotificationQueue.RemoveRange(context.NotificationQueue);
            await context.SaveChangesAsync();
        });
    }

    /// <summary>
    /// Clean up all notifications from tenant database
    /// NOTE: Not typically needed - database is recreated fresh for each test run
    /// </summary>
    protected async Task CleanupTenantNotificationsAsync()
    {
        await ExecuteTenantDbContextAsync(async context =>
        {
            context.Notifications.RemoveRange(context.Notifications);
            await context.SaveChangesAsync();
        });
    }

    /// <summary>
    /// Clean up all test data from both databases
    /// NOTE: Not typically needed - database is recreated fresh for each test run
    /// </summary>
    protected async Task CleanupAllTestDataAsync()
    {
        await CleanupGlobalQueueAsync();
        await CleanupTenantNotificationsAsync();
    }
}
