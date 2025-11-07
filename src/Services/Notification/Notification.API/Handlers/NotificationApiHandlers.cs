using MediatR;
using Notification.Application.Commands;
using Notification.Application.DTOs;
using Notification.Domain.Enums;

namespace Notification.API.Handlers;

public static class NotificationApiHandlers
{
    /// <summary>
    /// Handle send notification request
    /// </summary>
    public static async Task<IResult> SendNotificationHandler(
        SendNotificationCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    // Note: Acknowledgment is handled via SignalR Hub.AcknowledgeDelivery() method

    /// <summary>
    /// Handle get queue item status request
    /// </summary>
    public static async Task<IResult> GetQueueStatusHandler(
        int id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new GetQueueItemStatusCommand(QueueItemId: id);
        var result = await mediator.Send(command, ct);

        if (result == null)
        {
            return Results.NotFound(new { message = "Queue item not found" });
        }

        return Results.Ok(result);
    }

    /// <summary>
    /// Handle get user notifications request
    /// </summary>
    public static async Task<IResult> GetUserNotificationsHandler(
        int userId,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new GetUserNotificationsCommand(UserId: userId);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle mark notification as read request
    /// NOTE: UserId should be extracted from JWT claims in production
    /// </summary>
    public static async Task<IResult> MarkAsReadHandler(
        int id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        // Using placeholder userId=0 - should be extracted from authenticated user context
        var command = new MarkNotificationAsReadCommand(NotificationId: id, UserId: 0);
        var result = await mediator.Send(command, ct);

        if (!result)
        {
            return Results.NotFound(new { message = "Notification not found" });
        }

        return Results.Ok(new { success = true, message = "Notification marked as read" });
    }

    /// <summary>
    /// Handle get all queue items request with filters and pagination (SuperAdmin only)
    /// </summary>
    public static async Task<IResult> GetQueueItemsHandler(
        IMediator mediator,
        int pageNumber = 1,
        int pageSize = 10,
        string? tenantId = null,
        int? userId = null,
        QueueStatus? status = null,
        Priority? priority = null,
        DeliveryType? deliveryType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchTerm = null,
        CancellationToken ct = default)
    {
        var command = new GetQueueItemsCommand(
            PageNumber: pageNumber,
            PageSize: pageSize,
            TenantId: tenantId,
            UserId: userId,
            Status: status,
            Priority: priority,
            DeliveryType: deliveryType,
            FromDate: fromDate,
            ToDate: toDate,
            SearchTerm: searchTerm
        );

        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }
}
