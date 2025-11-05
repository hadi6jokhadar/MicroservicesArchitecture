using MediatR;
using Notification.Application.Commands;
using Notification.Application.DTOs;
using Notification.Application.Services;

namespace Notification.Application.Handlers;

public class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand, SendNotificationResponse>
{
    private readonly INotificationService _notificationService;

    public SendNotificationCommandHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<SendNotificationResponse> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        return await _notificationService.SendNotificationAsync(request, cancellationToken);
    }
}
