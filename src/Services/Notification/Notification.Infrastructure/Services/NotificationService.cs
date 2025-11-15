using Microsoft.Extensions.Logging;
using Notification.Application.Commands;
using Notification.Application.DTOs;
using Notification.Application.Services;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Domain.Repositories;

namespace Notification.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationQueueRepository _queueRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationQueueRepository queueRepository,
        INotificationRepository notificationRepository,
        ILogger<NotificationService> logger)
    {
        _queueRepository = queueRepository;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    public async Task<SendNotificationResponse> SendNotificationAsync(SendNotificationCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse enums from string
            var deliveryType = Enum.TryParse<DeliveryType>(command.DeliveryType, ignoreCase: true, out var dt) ? dt : DeliveryType.Both;
            var priority = Enum.TryParse<Priority>(command.Priority, ignoreCase: true, out var p) ? p : Priority.Immediate;

            // Create queue item
            var queueItem = new NotificationQueueItem
            {
                TenantId = command.TenantId,
                UserId = command.UserId,
                Title = command.Title,
                Message = command.Message,
                Data = command.Data,
                DeliveryType = deliveryType,
                Priority = priority,
                QueueStatus = QueueStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            await _queueRepository.AddAsync(queueItem, cancellationToken);

            _logger.LogInformation("Notification queued with ID: {QueueItemId} for user {UserId}", queueItem.Id, command.UserId);

            return new SendNotificationResponse
            {
                QueueItemId = queueItem.Id,
                Status = "Queued",
                QueuedAt = queueItem.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                Priority = queueItem.Priority.ToString(),
                DeliveryType = queueItem.DeliveryType.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing notification for user {UserId}", command.UserId);
            throw;
        }
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // First, verify the notification exists and belongs to the user
            var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
            
            if (notification == null || notification.UserId != userId)
            {
                _logger.LogWarning("Notification {NotificationId} not found or does not belong to user {UserId}", notificationId, userId);
                throw new IhsanDev.Shared.Application.Exceptions.NotFoundException($"Notification with ID {notificationId} was not found.");
            }
            
            var result = await _notificationRepository.MarkAsReadAsync(notificationId, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Failed to mark notification {NotificationId} as read", notificationId);
                throw new IhsanDev.Shared.Application.Exceptions.NotFoundException($"Notification with ID {notificationId} was not found.");
            }

            _logger.LogInformation("Notification {NotificationId} marked as read", notificationId);
            return true;
        }
        catch (IhsanDev.Shared.Application.Exceptions.NotFoundException)
        {
            throw; // Re-throw NotFoundException
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
            throw;
        }
    }

    public async Task<bool> AcknowledgeDeliveryAsync(int queueItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _queueRepository.UpdateStatusAsync(queueItemId, QueueStatus.Sent, null, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Queue item {QueueItemId} not found", queueItemId);
                throw new IhsanDev.Shared.Application.Exceptions.NotFoundException($"Queue item with ID {queueItemId} was not found.");
            }

            _logger.LogInformation("Queue item {QueueItemId} acknowledged", queueItemId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging queue item {QueueItemId}", queueItemId);
            throw;
        }
    }

    public async Task<QueueItemStatusResponse?> GetQueueItemStatusAsync(int queueItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var queueItem = await _queueRepository.GetByIdAsync(queueItemId, cancellationToken);

            if (queueItem == null)
            {
                return null;
            }

            return QueueItemStatusResponse.MapFrom(queueItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue item status for {QueueItemId}", queueItemId);
            throw;
        }
    }

    public async Task<List<NotificationResponse>> GetUserNotificationsAsync(
        int userId, 
        bool? unreadOnly = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notifications = await _notificationRepository.GetByUserIdAsync(userId, cancellationToken);
            
            // Apply unread filter if specified
            if (unreadOnly.HasValue && unreadOnly.Value)
            {
                notifications = notifications.Where(n => !n.IsRead).ToList();
            }
            
            // Apply pagination
            var paginatedNotifications = notifications
                .Skip(skip)
                .Take(take)
                .ToList();

            return paginatedNotifications.Select(NotificationResponse.MapFrom).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications for user {UserId}", userId);
            throw;
        }
    }
}
