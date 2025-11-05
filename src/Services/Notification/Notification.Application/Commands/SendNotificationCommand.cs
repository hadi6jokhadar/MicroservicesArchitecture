using FluentValidation;
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

public class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.Message)
            .MaximumLength(1000).WithMessage("Message cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Message));

        RuleFor(x => x.DeliveryType)
            .NotEmpty().WithMessage("DeliveryType is required")
            .Must(x => x == "SignalR" || x == "Firebase" || x == "Both")
            .WithMessage("DeliveryType must be 'SignalR', 'Firebase', or 'Both'");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required")
            .Must(x => x == "Immediate" || x == "Waitable")
            .WithMessage("Priority must be 'Immediate' or 'Waitable'");
    }
}
