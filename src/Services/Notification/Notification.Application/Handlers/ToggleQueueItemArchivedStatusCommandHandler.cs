using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Notification.Application.Commands;
using Notification.Application.DTOs;
using Notification.Domain.Repositories;

namespace Notification.Application.Handlers;

/// <summary>
/// Handler for toggling queue item archived status
/// </summary>
public class ToggleQueueItemArchivedStatusCommandHandler : IRequestHandler<ToggleQueueItemArchivedStatusCommand, QueueItemDto>
{
    private readonly INotificationQueueRepository _queueRepository;
    private readonly ILocalizationService _localizationService;

    public ToggleQueueItemArchivedStatusCommandHandler(
        INotificationQueueRepository queueRepository,
        ILocalizationService localizationService)
    {
        _queueRepository = queueRepository;
        _localizationService = localizationService;
    }

    public async Task<QueueItemDto> Handle(ToggleQueueItemArchivedStatusCommand request, CancellationToken cancellationToken)
    {
        var queueItem = await _queueRepository.GetByIdAsync(request.Id, cancellationToken);
        if (queueItem == null)
        {
            throw new NotFoundException(
                LocalizationKeys.Exceptions.QueueItemNotFound,
                _localizationService);
        }

        queueItem.IsArchived = !queueItem.IsArchived;
        queueItem.LastModified = DateTime.UtcNow;

        await _queueRepository.UpdateAsync(queueItem, cancellationToken);

        return QueueItemDto.MapFrom(queueItem);
    }
}
