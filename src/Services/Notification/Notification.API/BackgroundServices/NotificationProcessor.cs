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
    private readonly int _minBatchSize;
    private readonly int _maxBatchSize;
    private readonly bool _dynamicBatchSizing;
    private readonly int _maxRetryAttempts;
    private readonly int _baseRetryDelaySeconds;
    private readonly bool _priorityQueueEnabled;
    private readonly int _immediatePriorityPercentage;
    private readonly int _waitablePriorityPercentage;
    private readonly int _waitableAgingThresholdMinutes;

    public NotificationProcessor(
        IServiceProvider serviceProvider,
        ILogger<NotificationProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Get processing interval from configuration (default 2 seconds for 100k scale)
        using var scope = _serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var intervalSeconds = configuration.GetValue<int>("NotificationProcessing:ProcessingIntervalSeconds", 2);
        _processingInterval = TimeSpan.FromSeconds(intervalSeconds);
        
        // Dynamic batch sizing configuration
        _minBatchSize = configuration.GetValue<int>("NotificationProcessing:MinBatchSize", 50);
        _maxBatchSize = configuration.GetValue<int>("NotificationProcessing:MaxBatchSize", 500);
        _dynamicBatchSizing = configuration.GetValue<bool>("NotificationProcessing:DynamicBatchSizing", true);
        
        // Retry configuration with exponential backoff
        _maxRetryAttempts = configuration.GetValue<int>("NotificationProcessing:MaxRetryAttempts", 3);
        _baseRetryDelaySeconds = configuration.GetValue<int>("NotificationProcessing:RetryDelaySeconds", 30);
        
        // Priority queue configuration
        _priorityQueueEnabled = configuration.GetValue<bool>("NotificationProcessing:PriorityQueueEnabled", true);
        _immediatePriorityPercentage = configuration.GetValue<int>("NotificationProcessing:ImmediatePriorityPercentage", 80);
        _waitablePriorityPercentage = configuration.GetValue<int>("NotificationProcessing:WaitablePriorityPercentage", 20);
        _waitableAgingThresholdMinutes = configuration.GetValue<int>("NotificationProcessing:WaitableAgingThresholdMinutes", 60);
        
        _logger.LogInformation(
            "Notification Processor configured - Interval: {Interval}s, Dynamic Batching: {Dynamic}, Range: {Min}-{Max}, MaxRetries: {Retries}, BaseRetryDelay: {RetryDelay}s, PriorityQueue: {PriorityQueue} (Immediate: {Immediate}%, Waitable: {Waitable}%, Aging: {Aging}min)",
            intervalSeconds, _dynamicBatchSizing, _minBatchSize, _maxBatchSize, _maxRetryAttempts, _baseRetryDelaySeconds, 
            _priorityQueueEnabled, _immediatePriorityPercentage, _waitablePriorityPercentage, _waitableAgingThresholdMinutes);
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

        // Calculate dynamic batch size based on queue depth
        var batchSize = await CalculateBatchSizeAsync(globalDbContext, cancellationToken);

        List<Domain.Entities.NotificationQueueItem> pendingItems;

        if (_priorityQueueEnabled)
        {
            // Weighted priority batching to prevent starvation
            pendingItems = await GetWeightedPriorityBatchAsync(globalDbContext, batchSize, cancellationToken);
        }
        else
        {
            // Simple priority-based query (old behavior)
            pendingItems = await globalDbContext.NotificationQueue
                .Where(q => q.QueueStatus == QueueStatus.Pending 
                    && q.ExpiresAt > DateTime.UtcNow
                    && (q.NextRetryAt == null || q.NextRetryAt <= DateTime.UtcNow))
                .OrderBy(q => q.Priority)
                .ThenBy(q => q.Created)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        if (!pendingItems.Any())
        {
            return;
        }

        _logger.LogInformation(
            "Processing {Count} pending notifications (batch size: {BatchSize}, Immediate: {ImmediateCount}, Waitable: {WaitableCount})", 
            pendingItems.Count, 
            batchSize,
            pendingItems.Count(x => x.Priority == Priority.Immediate),
            pendingItems.Count(x => x.Priority == Priority.Waitable));

        // Group notifications by tenant for parallel processing
        var groupedByTenant = pendingItems
            .GroupBy(item => item.TenantId ?? "global")
            .ToList();

        _logger.LogDebug(
            "Grouped {TotalNotifications} notifications into {GroupCount} tenant groups",
            pendingItems.Count,
            groupedByTenant.Count);

        // Process each tenant group in parallel
        var processingTasks = groupedByTenant.Select(async tenantGroup =>
        {
            var tenantId = tenantGroup.Key;
            var items = tenantGroup.ToList();

            try
            {
                await ProcessTenantGroupAsync(scope, globalDbContext, hubContext, tenantId, items, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tenant group {TenantId} with {Count} items", tenantId, items.Count);
            }
        });

        // Wait for all tenant groups to complete
        await Task.WhenAll(processingTasks);
    }

    /// <summary>
    /// Process a group of notifications for a single tenant in batch
    /// Optimizes database operations by batching SaveChanges calls
    /// </summary>
    private async Task ProcessTenantGroupAsync(
        IServiceScope scope,
        NotificationDbContext globalDbContext,
        IHubContext<NotificationHub> hubContext,
        string tenantId,
        List<Domain.Entities.NotificationQueueItem> items,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing {Count} notifications for tenant {TenantId}", items.Count, tenantId);

        foreach (var item in items)
        {
            try
            {
                // Mark as processing
                item.QueueStatus = QueueStatus.Processing;
                item.LastModified = DateTime.UtcNow;

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

                _logger.LogDebug(
                    "Notification processed: {QueueItemId} for User: {UserId}, Tenant: {TenantId}",
                    item.Id,
                    item.UserId ?? 0,
                    item.TenantId ?? "none");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification {QueueItemId}", item.Id);
                
                // Increment retry count
                item.RetryCount++;
                
                // Check if max retries exceeded
                if (item.RetryCount >= _maxRetryAttempts)
                {
                    item.QueueStatus = QueueStatus.Failed;
                    item.Error = ex.Message;
                    item.NextRetryAt = null; // No more retries
                    _logger.LogError(
                        "Notification {QueueItemId} failed after {RetryCount} attempts: {Error}",
                        item.Id,
                        item.RetryCount,
                        ex.Message);
                }
                else
                {
                    // Exponential backoff: delay = baseDelay * 2^(retryCount - 1)
                    // Retry 1: 30s, Retry 2: 60s, Retry 3: 120s
                    var delaySeconds = _baseRetryDelaySeconds * Math.Pow(2, item.RetryCount - 1);
                    item.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                    item.QueueStatus = QueueStatus.Pending; // Will retry after NextRetryAt
                    
                    _logger.LogWarning(
                        "Notification {QueueItemId} retry {RetryCount}/{MaxRetries} scheduled for {NextRetryAt} (delay: {DelaySeconds}s)",
                        item.Id,
                        item.RetryCount,
                        _maxRetryAttempts,
                        item.NextRetryAt,
                        delaySeconds);
                }
                item.LastModified = DateTime.UtcNow;
            }
        }

        // Batch save all changes for this tenant group
        try
        {
            await globalDbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Completed processing {Count} notifications for tenant {TenantId}",
                items.Count,
                tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save changes for tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// Calculate dynamic batch size based on queue depth
    /// Scales from MinBatchSize (50) to MaxBatchSize (500) based on pending items
    /// Critical for handling 100k+ concurrent users
    /// </summary>
    private async Task<int> CalculateBatchSizeAsync(
        NotificationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!_dynamicBatchSizing)
        {
            // Use max batch size if dynamic sizing is disabled
            return _maxBatchSize;
        }

        // Count pending items in queue
        var pendingCount = await dbContext.NotificationQueue
            .CountAsync(q => q.QueueStatus == QueueStatus.Pending && q.ExpiresAt > DateTime.UtcNow, 
                cancellationToken);

        // Calculate batch size based on queue depth
        // Low load (< 100): Use minimum batch size (50)
        // Medium load (100-1000): Scale linearly
        // High load (> 1000): Use maximum batch size (500)
        int batchSize;
        
        if (pendingCount < 100)
        {
            batchSize = _minBatchSize;
        }
        else if (pendingCount > 1000)
        {
            batchSize = _maxBatchSize;
        }
        else
        {
            // Linear scaling between min and max
            var scaleFactor = (pendingCount - 100) / 900.0; // 0.0 to 1.0
            batchSize = _minBatchSize + (int)((_maxBatchSize - _minBatchSize) * scaleFactor);
        }

        _logger.LogDebug(
            "Dynamic batch sizing: {PendingCount} pending items → batch size {BatchSize}",
            pendingCount,
            batchSize);

        return batchSize;
    }

    /// <summary>
    /// Get weighted priority batch to prevent starvation of low-priority notifications
    /// Implements weighted batching: 80% Immediate, 20% Waitable
    /// Age-based boost: Waitable items older than threshold become Immediate priority
    /// </summary>
    private async Task<List<Domain.Entities.NotificationQueueItem>> GetWeightedPriorityBatchAsync(
        NotificationDbContext dbContext,
        int totalBatchSize,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var agingThreshold = now.AddMinutes(-_waitableAgingThresholdMinutes);

        // Calculate allocation per priority
        var immediateCount = (int)(totalBatchSize * (_immediatePriorityPercentage / 100.0));
        var waitableCount = totalBatchSize - immediateCount;

        _logger.LogDebug(
            "Weighted priority batching - Total: {Total}, Immediate: {Immediate} ({ImmediatePercent}%), Waitable: {Waitable} ({WaitablePercent}%)",
            totalBatchSize, immediateCount, _immediatePriorityPercentage, waitableCount, _waitablePriorityPercentage);

        // Fetch Immediate priority items (includes aged Waitable items)
        var immediateItems = await dbContext.NotificationQueue
            .Where(q => q.QueueStatus == QueueStatus.Pending 
                && q.ExpiresAt > now
                && (q.NextRetryAt == null || q.NextRetryAt <= now)
                && (q.Priority == Priority.Immediate || q.Created < agingThreshold)) // Age boost
            .OrderBy(q => q.Created) // FIFO for fairness
            .Take(immediateCount)
            .ToListAsync(cancellationToken);

        // Fetch Waitable priority items (not yet aged)
        var waitableItems = await dbContext.NotificationQueue
            .Where(q => q.QueueStatus == QueueStatus.Pending 
                && q.ExpiresAt > now
                && (q.NextRetryAt == null || q.NextRetryAt <= now)
                && q.Priority == Priority.Waitable
                && q.Created >= agingThreshold) // Not aged yet
            .OrderBy(q => q.Created) // FIFO for fairness
            .Take(waitableCount)
            .ToListAsync(cancellationToken);

        var agedWaitableCount = immediateItems.Count(x => x.Priority == Priority.Waitable && x.Created < agingThreshold);

        _logger.LogDebug(
            "Fetched weighted batch - Immediate: {ImmediateCount} (aged Waitable: {AgedCount}), Waitable: {WaitableCount}",
            immediateItems.Count,
            agedWaitableCount,
            waitableItems.Count);

        // Combine both lists
        var result = new List<Domain.Entities.NotificationQueueItem>(immediateItems.Count + waitableItems.Count);
        result.AddRange(immediateItems);
        result.AddRange(waitableItems);

        return result;
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
