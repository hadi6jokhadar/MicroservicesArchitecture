using FluentValidation;
using MediatR;
using Notification.Application.DTOs;

namespace Notification.Application.Commands;

/// <summary>
/// Command to get user notifications
/// </summary>
public record GetUserNotificationsCommand(
    int UserId,
    bool? UnreadOnly = null,
    int Skip = 0,
    int Take = 20
) : IRequest<List<NotificationResponse>>;

public class GetUserNotificationsCommandValidator : AbstractValidator<GetUserNotificationsCommand>
{
    public GetUserNotificationsCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("UserId must be greater than 0");

        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0).WithMessage("Skip must be greater than or equal to 0");

        RuleFor(x => x.Take)
            .GreaterThan(0).WithMessage("Take must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Take cannot exceed 100");
    }
}
