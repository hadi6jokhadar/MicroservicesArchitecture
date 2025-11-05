using FluentValidation;
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

public class AcknowledgeNotificationCommandValidator : AbstractValidator<AcknowledgeNotificationCommand>
{
    public AcknowledgeNotificationCommandValidator()
    {
        RuleFor(x => x.QueueItemId)
            .GreaterThan(0).WithMessage("QueueItemId must be greater than 0");
    }
}
