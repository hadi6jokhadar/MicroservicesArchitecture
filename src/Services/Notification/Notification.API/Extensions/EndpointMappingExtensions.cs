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
        // =============================================================================
        // GROUP 1: Service/SuperAdmin Endpoints (Global Access)
        // =============================================================================
        // - Accessible by: Service role, SuperAdmin role
        // - Authentication: Global JWT from appsettings.json
        // - Tenant Context: NOT required (bypasses tenant middleware)
        // - Use Case: System services and administrators managing notifications across all tenants
        // =============================================================================
        var serviceAdminGroup = app.MapGroup("/api/notifications")
            .WithTags("Notifications - Service/Admin")
            .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
            .WithOpenApi();

        // Send notification (accessible by services and SuperAdmin)
        // Note: Bypasses tenant middleware - tenantId comes from request body instead of x-tenant-id header
        // Rate limited per-tenant to prevent DoS attacks
        serviceAdminGroup.MapPost("/send", NotificationApiHandlers.SendNotificationHandler)
            .WithName("SendNotification")
            .WithSummary("Send a notification")
            .WithDescription("Queue a notification for delivery to a user via SignalR or Firebase. TenantId should be provided in the request body. Accessible by internal services and SuperAdmin. Rate limited to 1000 requests/minute per tenant.")
            .WithMetadata(new BypassTenantAttribute())
            .RequireRateLimiting("PerTenant")
            .Produces<SendNotificationResponse>(200)
            .ProducesValidationProblem()
            .Produces(429); // Too Many Requests

        // Get queue item status
        serviceAdminGroup.MapGet("/status/{id:int}", NotificationApiHandlers.GetQueueStatusHandler)
            .WithName("GetQueueItemStatus")
            .WithSummary("Get queue item status")
            .WithDescription("Retrieve the current status of a queued notification. Accessible by services and SuperAdmin.")
            .WithMetadata(new BypassTenantAttribute())
            .Produces<QueueItemStatusResponse>(200)
            .Produces(404);

        // Get all queue items with filters and pagination (SuperAdmin only)
        serviceAdminGroup.MapGet("/queue", NotificationApiHandlers.GetQueueItemsHandler)
            .WithName("GetQueueItems")
            .WithSummary("Get all queue items (Service/SuperAdmin only)")
            .WithDescription("Retrieve paginated list of all notification queue items with filtering support. Requires Service or SuperAdmin role with global JWT authentication.")
            .WithMetadata(new BypassTenantAttribute())
            .Produces<PaginatedList<QueueItemDto>>(200)
            .ProducesValidationProblem();

        // =============================================================================
        // GROUP 2: User Endpoints (Tenant-Specific Access)
        // =============================================================================
        // - Accessible by: User role, Admin role (tenant users)
        // - Authentication: Tenant-specific JWT (when JwtMode=PerTenant) or Global JWT (when JwtMode=Shared)
        // - Tenant Context: REQUIRED (must provide x-tenant-id header)
        // - Use Case: Regular users accessing their own notifications within their tenant
        // =============================================================================
        var userGroup = app.MapGroup("/api/notifications")
            .WithTags("Notifications - User")
            .RequireAuthorization(policy => policy.RequireRole("User", "Admin", "SuperAdmin"))
            .WithOpenApi();

        // Get user notifications (tenant-specific)
        // Requires x-tenant-id header and tenant-specific JWT token (when JwtMode=PerTenant)
        // UserId is automatically extracted from JWT token
        userGroup.MapGet("/user", NotificationApiHandlers.GetUserNotificationsHandler)
            .WithName("GetUserNotifications")
            .WithSummary("Get my notifications (tenant users)")
            .WithDescription("Retrieve all notifications for the authenticated user within their tenant. UserId is extracted from JWT token. Requires x-tenant-id header and tenant-specific JWT token (when JwtMode=PerTenant).")
            .Produces<List<NotificationResponse>>(200)
            .Produces(401);

        // Mark notification as read (tenant-specific)
        // Requires x-tenant-id header and tenant-specific JWT token (when JwtMode=PerTenant)
        userGroup.MapPut("/{id:int}/read", NotificationApiHandlers.MarkAsReadHandler)
            .WithName("MarkNotificationAsRead")
            .WithSummary("Mark notification as read (tenant users)")
            .WithDescription("Update notification read status for a notification within the user's tenant. Requires x-tenant-id header and tenant-specific JWT token (when JwtMode=PerTenant).")
            .Produces<object>(200)
            .Produces(404);

        return app;
    }
}
