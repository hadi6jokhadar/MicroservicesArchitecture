using MediatR;
using Notification.Application.Commands;
using Notification.Application.DTOs;
using Notification.Application.Services;

namespace Notification.Application.Handlers;

public class GetQueueItemStatusCommandHandler : IRequestHandler<GetQueueItemStatusCommand, QueueItemStatusResponse?>
{
    private readonly INotificationService _notificationService;

    public GetQueueItemStatusCommandHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<QueueItemStatusResponse?> Handle(GetQueueItemStatusCommand request, CancellationToken cancellationToken)
    {
        return await _notificationService.GetQueueItemStatusAsync(request.QueueItemId, cancellationToken);
    }
}
