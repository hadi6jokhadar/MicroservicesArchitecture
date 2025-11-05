using MediatR;
using Notification.Application.Commands;
using Notification.Application.DTOs;
using Notification.Application.Services;

namespace Notification.Application.Handlers;

public class GetUserNotificationsCommandHandler : IRequestHandler<GetUserNotificationsCommand, List<NotificationResponse>>
{
    private readonly INotificationService _notificationService;

    public GetUserNotificationsCommandHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<List<NotificationResponse>> Handle(GetUserNotificationsCommand request, CancellationToken cancellationToken)
    {
        return await _notificationService.GetUserNotificationsAsync(
            request.UserId, 
            request.UnreadOnly,
            request.Skip,
            request.Take,
            cancellationToken);
    }
}
