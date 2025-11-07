using Notification.API.Handlers;
using Notification.Application.DTOs;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Infrastructure.Attributes;

namespace Notification.API.Extensions;

public static class EndpointMappingExtensions
{
    /// <summary>
    /// Map all notification-related API endpoints
    /// </summary>
    public static WebApplication MapNotificationEndpoints(this WebApplication app)
    {
        var notificationGroup = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization(policy => policy.RequireRole("User", "Service", "SuperAdmin"))
            .WithOpenApi();

        // Send notification (accessible by users, services, and SuperAdmin)
        notificationGroup.MapPost("/send", NotificationApiHandlers.SendNotificationHandler)
            .WithName("SendNotification")
            .WithSummary("Send a notification")
            .WithDescription("Queue a notification for delivery to a user via SignalR or Firebase. Accessible by authenticated users, internal services, and SuperAdmin.")
            .Produces<SendNotificationResponse>(200)
            .ProducesValidationProblem();

        // Note: Acknowledgment is handled via SignalR Hub.AcknowledgeDelivery() method
        // No need for duplicate REST API endpoint

        // Get queue item status
        notificationGroup.MapGet("/status/{id:int}", NotificationApiHandlers.GetQueueStatusHandler)
            .WithName("GetQueueItemStatus")
            .WithSummary("Get queue item status")
            .WithDescription("Retrieve the current status of a queued notification")
            .Produces<QueueItemStatusResponse>(200)
            .Produces(404);

        // Get user notifications
        notificationGroup.MapGet("/user/{userId:int}", NotificationApiHandlers.GetUserNotificationsHandler)
            .WithName("GetUserNotifications")
            .WithSummary("Get user notifications")
            .WithDescription("Retrieve all notifications for a specific user")
            .Produces<List<NotificationResponse>>(200);

        // Mark notification as read
        notificationGroup.MapPut("/{id:int}/read", NotificationApiHandlers.MarkAsReadHandler)
            .WithName("MarkNotificationAsRead")
            .WithSummary("Mark notification as read")
            .WithDescription("Update notification read status")
            .Produces<object>(200)
            .Produces(404);

        // SuperAdmin endpoint - Get all queue items with filters and pagination
        // Uses global JWT from appsettings.json (not tenant-specific)
        var adminGroup = app.MapGroup("/api/notifications/admin")
            .WithTags("Notifications - Admin")
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"))
            .WithOpenApi();

        adminGroup.MapGet("/queue", NotificationApiHandlers.GetQueueItemsHandler)
            .WithName("GetQueueItems")
            .WithSummary("Get all queue items (SuperAdmin only)")
            .WithDescription("Retrieve paginated list of all notification queue items with filtering support. Requires SuperAdmin role with global JWT authentication.")
            .WithMetadata(new BypassTenantAttribute()) // Bypass tenant requirement for cross-tenant admin view
            .Produces<PaginatedList<QueueItemDto>>(200)
            .ProducesValidationProblem();

        return app;
    }
}
