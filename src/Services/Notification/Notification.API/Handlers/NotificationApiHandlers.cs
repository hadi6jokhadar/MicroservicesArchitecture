using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using MediatR;
using Notification.Application.Commands;
using Notification.Application.DTOs;
using Notification.Domain.Enums;

namespace Notification.API.Handlers;

public static class NotificationApiHandlers
{
    /// <summary>
    /// Handle send notification request
    /// Validates that user-specific notifications include tenant context when multi-tenancy is enabled
    /// </summary>
    public static async Task<IResult> SendNotificationHandler(
        SendNotificationCommand command,
        IMediator mediator,
        IConfiguration configuration,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        // Ensure user-specific notifications include tenant context
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);
        if (multiTenancyEnabled && command.UserId.HasValue && string.IsNullOrEmpty(command.TenantId))
        {
            return Results.BadRequest(new 
            { 
                error = localizationService.GetString(LocalizationKeys.Exceptions.TenantContextRequired),
                message = localizationService.GetString(LocalizationKeys.Exceptions.TenantContextRequired)
            });
        }

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
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var command = new GetQueueItemStatusCommand(QueueItemId: id);
        var result = await mediator.Send(command, ct);

        if (result == null)
        {
            return Results.NotFound(new { message = localizationService.GetString(LocalizationKeys.Exceptions.QueueItemNotFound) });
        }

        return Results.Ok(result);
    }

    /// <summary>
    /// Handle get user notifications request
    /// UserId is extracted from JWT token claims
    /// </summary>
    public static async Task<IResult> GetUserNotificationsHandler(
        ICurrentUserService currentUserService,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrEmpty(currentUserService.UserId))
        {
            return Results.Unauthorized();
        }

        if (!int.TryParse(currentUserService.UserId, out var userId))
        {
            return Results.BadRequest(new { error = "Invalid user ID in token" });
        }

        var command = new GetUserNotificationsCommand(UserId: userId);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle mark notification as read request
    /// UserId is extracted from JWT token claims
    /// </summary>
    public static async Task<IResult> MarkAsReadHandler(
        int id,
        ICurrentUserService currentUserService,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrEmpty(currentUserService.UserId))
        {
            return Results.Unauthorized();
        }

        if (!int.TryParse(currentUserService.UserId, out var userId))
        {
            return Results.BadRequest(new { error = "Invalid user ID in token" });
        }

        var command = new MarkNotificationAsReadCommand(NotificationId: id, UserId: userId);
        var result = await mediator.Send(command, ct);

        if (!result)
        {
            return Results.NotFound(new { message = localizationService.GetString(LocalizationKeys.Exceptions.NotificationNotFound) });
        }

        return Results.Ok(new { success = true, message = localizationService.GetString(LocalizationKeys.Success.NotificationMarkedAsRead) });
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
        bool isArchived = false,
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
            SearchTerm: searchTerm,
            IsArchived: isArchived
        );

        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Toggle queue item archived status (Service/SuperAdmin only)
    /// </summary>
    public static async Task<IResult> ToggleQueueItemArchivedStatusHandler(
        int id,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var command = new ToggleQueueItemArchivedStatusCommand(id);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }
}
