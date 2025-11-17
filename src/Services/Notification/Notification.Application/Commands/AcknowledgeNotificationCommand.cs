using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Notification.Application.Commands;

/// <summary>
/// Command to acknowledge notification delivery
/// </summary>
public record AcknowledgeNotificationCommand(
    int QueueItemId,
    string? ConnectionId = null,
    DateTime? ReceivedAt = null
) : IRequest<bool>;

public class AcknowledgeNotificationCommandValidator : LocalizedValidator<AcknowledgeNotificationCommand>
{
    public AcknowledgeNotificationCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.QueueItemId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "QueueItemId", "0"));
    }
}
