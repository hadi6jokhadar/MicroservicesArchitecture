using MediatR;
using Notification.Application.Commands;
using Notification.Application.Services;

namespace Notification.Application.Handlers;

public class AcknowledgeNotificationCommandHandler : IRequestHandler<AcknowledgeNotificationCommand, bool>
{
    private readonly INotificationService _notificationService;

    public AcknowledgeNotificationCommandHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<bool> Handle(AcknowledgeNotificationCommand request, CancellationToken cancellationToken)
    {
        return await _notificationService.AcknowledgeDeliveryAsync(request.QueueItemId, cancellationToken);
    }
}
