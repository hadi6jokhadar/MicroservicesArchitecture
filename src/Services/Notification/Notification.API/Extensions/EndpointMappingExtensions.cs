using Notification.API.Handlers;
using Notification.Application.DTOs;

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
            .RequireAuthorization(policy => policy.RequireRole("User", "Service"))
            .WithOpenApi();

        // Send notification (accessible by users and services)
        notificationGroup.MapPost("/send", NotificationApiHandlers.SendNotificationHandler)
            .WithName("SendNotification")
            .WithSummary("Send a notification")
            .WithDescription("Queue a notification for delivery to a user via SignalR or Firebase. Accessible by authenticated users and internal services.")
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

        return app;
    }
}
