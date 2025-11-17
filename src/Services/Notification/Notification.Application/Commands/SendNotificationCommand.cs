using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;
using Notification.Application.DTOs;

namespace Notification.Application.Commands;

/// <summary>
/// Command to send a notification
/// </summary>
public record SendNotificationCommand(
    string? TenantId,
    int? UserId,
    string Title,
    string? Message,
    string? Data,
    string DeliveryType = "Both",
    string Priority = "Immediate"
) : IRequest<SendNotificationResponse>;

public class SendNotificationCommandValidator : LocalizedValidator<SendNotificationCommand>
{
    public SendNotificationCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Title"))
            .MaximumLength(200).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Title", "200"));

        RuleFor(x => x.Message)
            .MaximumLength(1000).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Message", "1000"))
            .When(x => !string.IsNullOrEmpty(x.Message));

        RuleFor(x => x.DeliveryType)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "DeliveryType"))
            .Must(x => x == "SignalR" || x == "Firebase" || x == "Both")
            .WithMessage(L(LocalizationKeys.Validation.InvalidDeliveryType));

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Priority"))
            .Must(x => x == "Immediate" || x == "Waitable")
            .WithMessage(L(LocalizationKeys.Validation.InvalidPriority));
    }
}
