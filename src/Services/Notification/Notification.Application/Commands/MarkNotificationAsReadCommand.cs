using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Notification.Application.Commands;

/// <summary>
/// Command to mark a notification as read
/// </summary>
public record MarkNotificationAsReadCommand(
    int NotificationId,
    int UserId
) : IRequest<bool>;

public class MarkNotificationAsReadCommandValidator : LocalizedValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.NotificationId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.NotificationId), "0"));

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.UserId), "0"));
    }
}
