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

        // FIX #1: Batch fetch device tokens for all users in this tenant group
        var firebaseService = scope.ServiceProvider.GetService<Application.Interfaces.IFirebaseService>();
        var identityClient = scope.ServiceProvider.GetService<Application.Interfaces.IIdentityServiceClient>();
        
        Dictionary<int, List<Application.DTOs.DeviceTokenDto>>? deviceTokensCache = null;
        
        if (firebaseService?.IsEnabled == true && identityClient != null)
        {
            var userIds = items
                .Where(x => x.UserId.HasValue && x.DeliveryType != DeliveryType.SignalR)
                .Select(x => x.UserId!.Value)
                .Distinct()
                .ToList();

            if (userIds.Any())
            {
                deviceTokensCache = await identityClient.GetBatchDeviceTokensAsync(
                    userIds,
                    tenantId,
                    cancellationToken);

                _logger.LogInformation(
                    "Batch fetched device tokens for {UserCount} users in tenant {TenantId}",
                    deviceTokensCache.Count,
                    tenantId);
            }
        }

        foreach (var item in items)
        {
            try
            {
                // Mark as processing
                item.QueueStatus = QueueStatus.Processing;
                item.LastModified = DateTime.UtcNow;

                // Step 1: Persist to tenant database
                int? persistedNotificationId = null;
                
                // For tenant-specific notifications (has tenantId)
                if (!string.IsNullOrWhiteSpace(item.TenantId))
                {
                    persistedNotificationId = await PersistToTenantDatabaseAsync(
                        scope.ServiceProvider, 
                        item, 
                        cancellationToken);
                    
                    item.NotificationId = persistedNotificationId;
                }
                // For global notifications (no tenantId, no userId) - save to all tenant databases
                else if (!item.UserId.HasValue && string.IsNullOrWhiteSpace(item.TenantId))
                {
                    await PersistGlobalNotificationToAllTenantsAsync(
                        scope.ServiceProvider,
                        item,
                        cancellationToken);
                    
                    _logger.LogDebug(
                        "Global notification persisted to all tenant databases - QueueItemId={QueueItemId}",
                        item.Id);
                }

                // Step 2: Send via SignalR (if enabled)
                if (item.DeliveryType == DeliveryType.SignalR || item.DeliveryType == DeliveryType.Both)
                {
                    await SendViaSignalRAsync(hubContext, item, cancellationToken);
                }

                // Step 3: Send via Firebase (if enabled) - using cached tokens
                if (item.DeliveryType == DeliveryType.Firebase || item.DeliveryType == DeliveryType.Both)
                {
                    await SendViaFirebaseAsync(
                        scope.ServiceProvider, 
                        item, 
                        deviceTokensCache, 
                        cancellationToken);
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
    /// Persist global notification to all tenant databases
    /// Used for global broadcasts (no tenantId, no userId)
    /// </summary>
    private async Task PersistGlobalNotificationToAllTenantsAsync(
        IServiceProvider serviceProvider,
        Domain.Entities.NotificationQueueItem queueItem,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantClient = serviceProvider.GetService<Application.Interfaces.ITenantServiceClient>();
            
            if (tenantClient == null)
            {
                _logger.LogWarning(
                    "Tenant Service client not available. Cannot persist global notification to tenant databases - QueueItemId={QueueItemId}",
                    queueItem.Id);
                return;
            }

            // Get all active tenants
            var tenantIds = await tenantClient.GetAllActiveTenantIdsAsync(cancellationToken);

            if (!tenantIds.Any())
            {
                _logger.LogWarning(
                    "No active tenants found. Global notification not persisted to any tenant database - QueueItemId={QueueItemId}",
                    queueItem.Id);
                return;
            }

            _logger.LogInformation(
                "Persisting global notification to {TenantCount} tenant databases - QueueItemId={QueueItemId}",
                tenantIds.Count,
                queueItem.Id);

            // OPTIMIZATION: Parallel processing for global notifications (10-50x faster with many tenants)
            var persistTasks = tenantIds.Select(async tenantId =>
            {
                try
                {
                    // Create a temporary queue item with tenantId for persistence
                    var tenantQueueItem = new Domain.Entities.NotificationQueueItem
                    {
                        Id = queueItem.Id,
                        TenantId = tenantId, // Override with specific tenantId
                        UserId = null, // Global notification has no specific user
                        Title = queueItem.Title,
                        Message = queueItem.Message,
                        Data = queueItem.Data,
                        DeliveryType = queueItem.DeliveryType,
                        Priority = queueItem.Priority,
                        QueueStatus = queueItem.QueueStatus,
                        NotificationId = queueItem.NotificationId,
                        Created = queueItem.Created,
                        LastModified = queueItem.LastModified
                    };

                    await PersistToTenantDatabaseAsync(
                        serviceProvider,
                        tenantQueueItem,
                        cancellationToken);

                    return (tenantId, success: true, error: (string?)null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to persist global notification to tenant {TenantId} database - QueueItemId={QueueItemId}",
                        tenantId,
                        queueItem.Id);
                    return (tenantId, success: false, error: ex.Message);
                }
            });

            // Wait for all parallel operations to complete
            var results = await Task.WhenAll(persistTasks);
            var successCount = results.Count(r => r.success);
            var failureCount = results.Count(r => !r.success);

            _logger.LogInformation(
                "Global notification persistence completed - Success: {SuccessCount}, Failed: {FailureCount}, QueueItemId={QueueItemId}",
                successCount,
                failureCount,
                queueItem.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error persisting global notification to tenant databases - QueueItemId={QueueItemId}",
                queueItem.Id);
            // Don't throw - global notification should still be sent even if persistence fails
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
    /// Handles 3 scenarios:
    /// 1. Global notification (no userId, no tenantId) - loops through ALL tenants
    /// 2. Tenant notification (tenantId, no userId) - sends to all devices in tenant
    /// 3. User notification (userId + tenantId) - sends to specific user's devices
    /// </summary>
    private async Task SendViaFirebaseAsync(
        IServiceProvider serviceProvider,
        Domain.Entities.NotificationQueueItem queueItem,
        Dictionary<int, List<Application.DTOs.DeviceTokenDto>>? deviceTokensCache,
        CancellationToken cancellationToken)
    {
        try
        {
            var firebaseService = serviceProvider.GetService<Application.Interfaces.IFirebaseService>();
            var identityClient = serviceProvider.GetService<Application.Interfaces.IIdentityServiceClient>();
            var tenantClient = serviceProvider.GetService<Application.Interfaces.ITenantServiceClient>();

            // Check if Firebase is enabled
            if (firebaseService == null || !firebaseService.IsEnabled)
            {
                _logger.LogDebug(
                    "Firebase is not enabled or service not registered - QueueItemId={QueueItemId}",
                    queueItem.Id);
                return;
            }

            if (identityClient == null)
            {
                _logger.LogWarning(
                    "Identity Service client not registered. Cannot retrieve device tokens - QueueItemId={QueueItemId}",
                    queueItem.Id);
                return;
            }

            List<Application.DTOs.DeviceTokenDto> deviceTokens;

            // Determine notification type and fetch appropriate device tokens
            if (!queueItem.UserId.HasValue && string.IsNullOrWhiteSpace(queueItem.TenantId))
            {
                // Scenario 1: Global notification - loop through ALL tenants
                _logger.LogInformation(
                    "Processing GLOBAL notification - QueueItemId={QueueItemId}",
                    queueItem.Id);

                if (tenantClient == null)
                {
                    _logger.LogWarning(
                        "Tenant Service client not registered. Cannot retrieve tenants for global notification - QueueItemId={QueueItemId}",
                        queueItem.Id);
                    return;
                }

                // Get all active tenant IDs
                var tenantIds = await tenantClient.GetAllActiveTenantIdsAsync(cancellationToken);
                
                if (!tenantIds.Any())
                {
                    _logger.LogWarning(
                        "No active tenants found for global notification - QueueItemId={QueueItemId}",
                        queueItem.Id);
                    return;
                }

                _logger.LogInformation(
                    "Found {TenantCount} active tenants for global notification - QueueItemId={QueueItemId}",
                    tenantIds.Count,
                    queueItem.Id);

                // OPTIMIZATION: Parallel processing for global Firebase notifications (5-10x faster)
                deviceTokens = new List<Application.DTOs.DeviceTokenDto>();
                
                var firebaseTasks = tenantIds.Select(async tenantId =>
                {
                    try
                    {
                        var tenantDeviceTokens = await identityClient.GetTenantDeviceTokensAsync(
                            tenantId,
                            cancellationToken);

                        if (!tenantDeviceTokens.Any())
                        {
                            _logger.LogDebug(
                                "No device tokens found for tenant {TenantId} - QueueItemId={QueueItemId}",
                                tenantId,
                                queueItem.Id);
                            return (tenantId, successCount: 0, failureCount: 0);
                        }

                        _logger.LogInformation(
                            "Sending global notification to {TokenCount} devices in tenant {TenantId} - QueueItemId={QueueItemId}",
                            tenantDeviceTokens.Count,
                            tenantId,
                            queueItem.Id);

                        // Prepare data payload
                        var tenantData = new Dictionary<string, string>
                        {
                            { "queueItemId", queueItem.Id.ToString() },
                            { "notificationId", queueItem.NotificationId?.ToString() ?? "0" },
                            { "tenantId", tenantId },
                            { "userId", "0" },
                            { "priority", queueItem.Priority.ToString() }
                        };

                        if (!string.IsNullOrWhiteSpace(queueItem.Data))
                        {
                            tenantData["customData"] = queueItem.Data;
                        }

                        var tenantTokens = tenantDeviceTokens.Select(dt => dt.Token).ToList();

                        // Send to tenant's devices
                        var tenantResults = await firebaseService.SendToMultipleDevicesAsync(
                            tenantTokens,
                            queueItem.Title,
                            queueItem.Message ?? string.Empty,
                            tenantData,
                            cancellationToken);

                        // Handle invalid tokens
                        if (tenantResults.InvalidTokenIds.Any())
                        {
                            var invalidTokenIdsToDelete = tenantDeviceTokens
                                .Where(dt => tenantResults.InvalidTokenIds.Contains(dt.Token))
                                .Select(dt => dt.Id)
                                .ToList();

                            if (invalidTokenIdsToDelete.Any())
                            {
                                await identityClient.DeleteBatchDeviceTokensAsync(
                                    invalidTokenIdsToDelete,
                                    tenantId,
                                    cancellationToken);

                                _logger.LogInformation(
                                    "Deleted {Count} invalid device tokens for tenant {TenantId}",
                                    invalidTokenIdsToDelete.Count,
                                    tenantId);
                            }
                        }

                        _logger.LogInformation(
                            "Global notification sent to tenant {TenantId}: Success={Success}, Failed={Failed} - QueueItemId={QueueItemId}",
                            tenantId,
                            tenantResults.SuccessCount,
                            tenantResults.FailureCount,
                            queueItem.Id);

                        return (tenantId, successCount: tenantResults.SuccessCount, failureCount: tenantResults.FailureCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error sending global notification to tenant {TenantId} - QueueItemId={QueueItemId}",
                            tenantId,
                            queueItem.Id);
                        return (tenantId, successCount: 0, failureCount: 0);
                    }
                });

                // Wait for all parallel Firebase operations to complete
                var firebaseResults = await Task.WhenAll(firebaseTasks);
                int totalSuccessCount = firebaseResults.Sum(r => r.successCount);
                int totalFailureCount = firebaseResults.Sum(r => r.failureCount);

                _logger.LogInformation(
                    "Global notification completed across {TenantCount} tenants: Total Success={TotalSuccess}, Total Failed={TotalFailed} - QueueItemId={QueueItemId}",
                    tenantIds.Count,
                    totalSuccessCount,
                    totalFailureCount,
                    queueItem.Id);

                return; // Exit early as we handled everything
            }
            else if (!queueItem.UserId.HasValue && !string.IsNullOrWhiteSpace(queueItem.TenantId))
            {
                // Scenario 2: Tenant notification - get all device tokens for tenant
                _logger.LogInformation(
                    "Processing TENANT notification for tenant {TenantId} - QueueItemId={QueueItemId}",
                    queueItem.TenantId,
                    queueItem.Id);

                deviceTokens = await identityClient.GetTenantDeviceTokensAsync(
                    queueItem.TenantId,
                    cancellationToken);
            }
            else if (queueItem.UserId.HasValue)
            {
                // Scenario 3: User notification - get device tokens for specific user
                _logger.LogInformation(
                    "Processing USER notification for user {UserId} - QueueItemId={QueueItemId}",
                    queueItem.UserId,
                    queueItem.Id);

                // Try cache first for user notifications
                if (deviceTokensCache != null && deviceTokensCache.TryGetValue(queueItem.UserId.Value, out var cachedTokens))
                {
                    deviceTokens = cachedTokens;
                    _logger.LogDebug(
                        "Using cached device tokens for user {UserId} - QueueItemId={QueueItemId}",
                        queueItem.UserId,
                        queueItem.Id);
                }
                else
                {
                    // Fallback to individual API call (cache miss)
                    deviceTokens = await identityClient.GetUserDeviceTokensAsync(
                        queueItem.UserId.Value,
                        queueItem.TenantId,
                        cancellationToken);

                    _logger.LogDebug(
                        "Fetched device tokens from API for user {UserId} (cache miss) - QueueItemId={QueueItemId}",
                        queueItem.UserId,
                        queueItem.Id);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Invalid notification configuration - QueueItemId={QueueItemId}",
                    queueItem.Id);
                return;
            }

            if (!deviceTokens.Any())
            {
                var notificationType = !queueItem.UserId.HasValue && string.IsNullOrWhiteSpace(queueItem.TenantId) 
                    ? "GLOBAL" 
                    : !queueItem.UserId.HasValue 
                        ? "TENANT" 
                        : "USER";

                _logger.LogDebug(
                    "No device tokens found for {NotificationType} notification - QueueItemId={QueueItemId}",
                    notificationType,
                    queueItem.Id);
                return;
            }

            var notificationScope = !queueItem.UserId.HasValue && string.IsNullOrWhiteSpace(queueItem.TenantId)
                ? "globally"
                : !queueItem.UserId.HasValue
                    ? $"for tenant {queueItem.TenantId}"
                    : $"for user {queueItem.UserId}";

            _logger.LogInformation(
                "Sending Firebase notification to {TokenCount} device(s) {Scope} - QueueItemId={QueueItemId}",
                deviceTokens.Count,
                notificationScope,
                queueItem.Id);

            // Prepare data payload
            var data = new Dictionary<string, string>
            {
                { "queueItemId", queueItem.Id.ToString() },
                { "notificationId", queueItem.NotificationId?.ToString() ?? "0" },
                { "tenantId", queueItem.TenantId ?? "" },
                { "userId", queueItem.UserId?.ToString() ?? "0" },
                { "priority", queueItem.Priority.ToString() }
            };

            // Add custom data if available
            if (!string.IsNullOrWhiteSpace(queueItem.Data))
            {
                data["customData"] = queueItem.Data;
            }

            // Extract just the tokens
            var tokens = deviceTokens.Select(dt => dt.Token).ToList();

            // Send to multiple devices
            var results = await firebaseService.SendToMultipleDevicesAsync(
                tokens,
                queueItem.Title,
                queueItem.Message ?? string.Empty,
                data,
                cancellationToken);

            // Process results and remove invalid tokens
            var invalidTokenIds = new List<int>();
            var successCount = 0;
            var failureCount = 0;

            foreach (var deviceToken in deviceTokens)
            {
                if (results.TokenResults.TryGetValue(deviceToken.Token, out var success))
                {
                    if (success)
                    {
                        successCount++;
                        _logger.LogDebug(
                            "Firebase notification sent successfully to device {DeviceId} (TokenId: {TokenId}) for user {UserId}",
                            deviceToken.DeviceIdentifier ?? "unknown",
                            deviceToken.Id,
                            deviceToken.UserId);
                    }
                    else
                    {
                        failureCount++;
                        invalidTokenIds.Add(deviceToken.Id);
                        _logger.LogWarning(
                            "Firebase notification failed for device {DeviceId} (TokenId: {TokenId}) - token may be invalid or expired",
                            deviceToken.DeviceIdentifier ?? "unknown",
                            deviceToken.Id);
                    }
                }
            }

            _logger.LogInformation(
                "Firebase notification completed - Success: {SuccessCount}, Failed: {FailureCount}, QueueItemId={QueueItemId}",
                successCount,
                failureCount,
                queueItem.Id);

            // Remove invalid tokens from database using batch delete
            if (invalidTokenIds.Any() && identityClient != null)
            {
                _logger.LogInformation(
                    "Removing {Count} invalid/expired device tokens for user {UserId} using batch delete",
                    invalidTokenIds.Count,
                    queueItem.UserId);

                try
                {
                    var deletedCount = await identityClient.DeleteBatchDeviceTokensAsync(
                        invalidTokenIds,
                        queueItem.TenantId,
                        cancellationToken);

                    _logger.LogInformation(
                        "Successfully deleted {DeletedCount} of {TotalCount} invalid device tokens for user {UserId}",
                        deletedCount,
                        invalidTokenIds.Count,
                        queueItem.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to batch delete {Count} invalid device tokens for user {UserId}",
                        invalidTokenIds.Count,
                        queueItem.UserId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Firebase notification for QueueItemId={QueueItemId}",
                queueItem.Id);
        }
    }
}
