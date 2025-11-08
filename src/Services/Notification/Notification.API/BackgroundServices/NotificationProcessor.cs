using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Notification.Infrastructure.Persistence;
using Notification.Domain.Enums;
using Notification.API.Hubs;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Notification.API.BackgroundServices;

/// <summary>
/// Background service to process notification queue
/// Handles immediate and waitable notifications
/// </summary>
public class NotificationProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationProcessor> _logger;
    private readonly TimeSpan _processingInterval;

    public NotificationProcessor(
        IServiceProvider serviceProvider,
        ILogger<NotificationProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Get processing interval from configuration (default 5 seconds)
        using var scope = _serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var intervalSeconds = configuration.GetValue<int>("NotificationProcessing:ProcessingIntervalSeconds", 5);
        _processingInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification queue");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Notification Processor stopped");
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var globalDbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

        // Get pending items that haven't expired
        var pendingItems = await globalDbContext.NotificationQueue
            .Where(q => q.QueueStatus == QueueStatus.Pending && q.ExpiresAt > DateTime.UtcNow)
            .OrderBy(q => q.Priority) // Immediate first, then Waitable
            .ThenBy(q => q.Created)
            .Take(50) // Process in batches
            .ToListAsync(cancellationToken);

        if (!pendingItems.Any())
        {
            return;
        }

        _logger.LogInformation("Processing {Count} pending notifications", pendingItems.Count);

        foreach (var item in pendingItems)
        {
            try
            {
                // Mark as processing
                item.QueueStatus = QueueStatus.Processing;
                item.LastModified = DateTime.UtcNow;
                await globalDbContext.SaveChangesAsync(cancellationToken);

                // Step 1: Persist to tenant database (if tenant-specific)
                int? persistedNotificationId = null;
                if (!string.IsNullOrWhiteSpace(item.TenantId))
                {
                    persistedNotificationId = await PersistToTenantDatabaseAsync(
                        scope.ServiceProvider, 
                        item, 
                        cancellationToken);
                    
                    item.NotificationId = persistedNotificationId;
                }

                // Step 2: Send via SignalR (if enabled)
                if (item.DeliveryType == DeliveryType.SignalR || item.DeliveryType == DeliveryType.Both)
                {
                    await SendViaSignalRAsync(hubContext, item, cancellationToken);
                }

                // Step 3: Send via Firebase (if enabled)
                if (item.DeliveryType == DeliveryType.Firebase || item.DeliveryType == DeliveryType.Both)
                {
                    await SendViaFirebaseAsync(scope.ServiceProvider, item, cancellationToken);
                }
                
                // Mark as sent
                item.QueueStatus = QueueStatus.Sent;
                item.ProcessedAt = DateTime.UtcNow;
                item.LastModified = DateTime.UtcNow;

                _logger.LogInformation(
                    "Notification processed: {QueueItemId} for User: {UserId}, Tenant: {TenantId}, DeliveryType: {DeliveryType}",
                    item.Id,
                    item.UserId ?? 0,
                    item.TenantId ?? "none",
                    item.DeliveryType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification {QueueItemId}", item.Id);
                
                // Mark as failed after max retries
                item.RetryCount++;
                if (item.RetryCount >= 3)
                {
                    item.QueueStatus = QueueStatus.Failed;
                    item.Error = ex.Message;
                    _logger.LogError(
                        "Notification {QueueItemId} failed after {RetryCount} attempts: {Error}",
                        item.Id,
                        item.RetryCount,
                        ex.Message);
                }
                else
                {
                    item.QueueStatus = QueueStatus.Pending; // Retry later
                    _logger.LogWarning(
                        "Notification {QueueItemId} retry {RetryCount}/3",
                        item.Id,
                        item.RetryCount);
                }
                item.LastModified = DateTime.UtcNow;
            }

            await globalDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Persist notification to tenant database
    /// </summary>
    private async Task<int?> PersistToTenantDatabaseAsync(
        IServiceProvider serviceProvider,
        Domain.Entities.NotificationQueueItem queueItem,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantContext = serviceProvider.GetService<ITenantContext>();
            var tenantConfigProvider = serviceProvider.GetService<ITenantConfigurationProvider>();
            
            // Set tenant context if not already set
            if (tenantContext != null && !tenantContext.HasTenant && !string.IsNullOrWhiteSpace(queueItem.TenantId))
            {
                if (tenantConfigProvider == null)
                {
                    throw new InvalidOperationException(
                        "ITenantConfigurationProvider not available. Cannot fetch tenant configuration for background processing.");
                }

                // Fetch full tenant configuration (includes DatabaseSettings)
                // This will use cache if available, otherwise fetches from Tenant Service
                var tenantInfo = await tenantConfigProvider.GetTenantConfigurationAsync(
                    queueItem.TenantId,
                    cancellationToken);

                if (tenantInfo == null)
                {
                    _logger.LogWarning(
                        "Tenant '{TenantId}' not found or inactive. Cannot persist notification to tenant database. " +
                        "This could be due to: 1) Tenant deleted/deactivated, 2) Tenant Service unavailable, 3) Network issues. " +
                        "Notification will be retried.",
                        queueItem.TenantId);
                    
                    throw new InvalidOperationException(
                        $"Tenant '{queueItem.TenantId}' configuration not available. Cannot persist notification to tenant database.");
                }

                tenantContext.SetTenant(tenantInfo);
                
                _logger.LogDebug(
                    "Tenant context set for background processing: TenantId={TenantId}, HasConfiguration={HasConfig}",
                    tenantInfo.TenantId,
                    tenantInfo.Configuration != null);
            }

            var tenantDbContext = serviceProvider.GetRequiredService<TenantNotificationDbContext>();

            var notification = new Domain.Entities.Notification
            {
                UserId = queueItem.UserId,
                Title = queueItem.Title,
                Message = queueItem.Message,
                Data = queueItem.Data,
                IsRead = false,
                QueueItemId = queueItem.Id,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            tenantDbContext.Notifications.Add(notification);
            await tenantDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Notification persisted to tenant DB: NotificationId={NotificationId}, QueueItemId={QueueItemId}",
                notification.Id,
                queueItem.Id);

            return notification.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist notification to tenant DB for QueueItemId={QueueItemId}",
                queueItem.Id);
            throw;
        }
    }

    /// <summary>
    /// Send notification via SignalR based on targeting rules
    /// </summary>
    private async Task SendViaSignalRAsync(
        IHubContext<NotificationHub> hubContext,
        Domain.Entities.NotificationQueueItem queueItem,
        CancellationToken cancellationToken)
    {
        try
        {
            var notificationPayload = new
            {
                queueItemId = queueItem.Id,
                notificationId = queueItem.NotificationId,
                tenantId = queueItem.TenantId,
                userId = queueItem.UserId,
                title = queueItem.Title,
                message = queueItem.Message,
                data = queueItem.Data,
                created = queueItem.Created,
                priority = queueItem.Priority.ToString()
            };

            // Determine which group to send to based on configuration and queue item properties
            var isMultiTenancyEnabled = _serviceProvider.GetRequiredService<IConfiguration>()
                .GetValue<bool>("MultiTenancy:Enabled", true);

            if (queueItem.UserId == null && string.IsNullOrWhiteSpace(queueItem.TenantId))
            {
                // Global notification to ALL clients (authenticated and anonymous)
                await hubContext.Clients.Group("global")
                    .SendAsync("ReceiveNotification", notificationPayload, cancellationToken);

                _logger.LogInformation(
                    "SignalR global notification sent to all clients, QueueItemId={QueueItemId}",
                    queueItem.Id);
            }
            else if (isMultiTenancyEnabled)
            {
                // Multi-tenancy mode
                if (queueItem.UserId.HasValue && !string.IsNullOrWhiteSpace(queueItem.TenantId))
                {
                    // Send to specific user in tenant
                    var groupName = $"tenant:{queueItem.TenantId}:user:{queueItem.UserId}";
                    await hubContext.Clients.Group(groupName)
                        .SendAsync("ReceiveNotification", notificationPayload, cancellationToken);

                    _logger.LogInformation(
                        "SignalR notification sent to user {UserId} in tenant {TenantId}, QueueItemId={QueueItemId}",
                        queueItem.UserId,
                        queueItem.TenantId,
                        queueItem.Id);
                }
                else if (!string.IsNullOrWhiteSpace(queueItem.TenantId))
                {
                    // Broadcast to all users in tenant (authenticated and anonymous)
                    var groupName = $"tenant:{queueItem.TenantId}";
                    await hubContext.Clients.Group(groupName)
                        .SendAsync("ReceiveNotification", notificationPayload, cancellationToken);

                    _logger.LogInformation(
                        "SignalR notification broadcast to all clients in tenant {TenantId}, QueueItemId={QueueItemId}",
                        queueItem.TenantId,
                        queueItem.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Multi-tenancy enabled but no TenantId specified for QueueItemId={QueueItemId}. Sending as global.",
                        queueItem.Id);
                    
                    // Fallback to global
                    await hubContext.Clients.Group("global")
                        .SendAsync("ReceiveNotification", notificationPayload, cancellationToken);
                }
            }
            else
            {
                // Single-tenant mode (MultiTenancy:Enabled = false)
                if (queueItem.UserId.HasValue)
                {
                    // Send to specific user
                    var groupName = $"user:{queueItem.UserId}";
                    await hubContext.Clients.Group(groupName)
                        .SendAsync("ReceiveNotification", notificationPayload, cancellationToken);

                    _logger.LogInformation(
                        "SignalR notification sent to user {UserId} (single-tenant mode), QueueItemId={QueueItemId}",
                        queueItem.UserId,
                        queueItem.Id);
                }
                else
                {
                    // Broadcast to all connected clients
                    await hubContext.Clients.Group("all-clients")
                        .SendAsync("ReceiveNotification", notificationPayload, cancellationToken);

                    _logger.LogInformation(
                        "SignalR notification broadcast to all clients (single-tenant mode), QueueItemId={QueueItemId}",
                        queueItem.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send SignalR notification for QueueItemId={QueueItemId}",
                queueItem.Id);
            throw;
        }
    }

    /// <summary>
    /// Send notification via Firebase Cloud Messaging
    /// </summary>
    private async Task SendViaFirebaseAsync(
        IServiceProvider serviceProvider,
        Domain.Entities.NotificationQueueItem queueItem,
        CancellationToken cancellationToken)
    {
        try
        {
            // Firebase Cloud Messaging implementation pending
            // Required steps:
            // 1. Get device tokens for user from Identity Service
            // 2. Send push notification via Firebase Admin SDK
            // 3. Handle delivery status and token invalidation

            _logger.LogDebug(
                "Firebase notification not implemented - QueueItemId={QueueItemId}",
                queueItem.Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Firebase notification for QueueItemId={QueueItemId}",
                queueItem.Id);
            throw;
        }
    }
}
