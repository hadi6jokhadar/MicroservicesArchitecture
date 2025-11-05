using MediatR;
using Notification.Application.Commands;
using Notification.Application.Services;

namespace Notification.Application.Handlers;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    private readonly INotificationService _notificationService;

    public MarkNotificationAsReadCommandHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        return await _notificationService.MarkAsReadAsync(request.NotificationId, request.UserId, cancellationToken);
    }
}
