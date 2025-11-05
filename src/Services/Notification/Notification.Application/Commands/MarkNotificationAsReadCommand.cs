using FluentValidation;
using MediatR;

namespace Notification.Application.Commands;

/// <summary>
/// Command to mark a notification as read
/// </summary>
public record MarkNotificationAsReadCommand(
    int NotificationId,
    int UserId
) : IRequest<bool>;

public class MarkNotificationAsReadCommandValidator : AbstractValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadCommandValidator()
    {
        RuleFor(x => x.NotificationId)
            .GreaterThan(0).WithMessage("NotificationId must be greater than 0");

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("UserId must be greater than 0");
    }
}
