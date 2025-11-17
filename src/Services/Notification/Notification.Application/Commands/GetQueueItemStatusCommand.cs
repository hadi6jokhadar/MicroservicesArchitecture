using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;
using Notification.Application.DTOs;

namespace Notification.Application.Commands;

/// <summary>
/// Command to get queue item status
/// </summary>
public record GetQueueItemStatusCommand(
    int QueueItemId
) : IRequest<QueueItemStatusResponse?>;

public class GetQueueItemStatusCommandValidator : LocalizedValidator<GetQueueItemStatusCommand>
{
    public GetQueueItemStatusCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.QueueItemId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "QueueItemId", "0"));
    }
}
