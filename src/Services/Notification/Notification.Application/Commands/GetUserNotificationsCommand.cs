using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
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

public class GetUserNotificationsCommandValidator : LocalizedValidator<GetUserNotificationsCommand>
{
    public GetUserNotificationsCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.UserId), "0"));

        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThanOrEqual, L(LocalizationKeys.Fields.Skip), "0"));

        RuleFor(x => x.Take)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.Take), "0"))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.PageSizeExceeded));
    }
}
