using FluentValidation;
using MediatR;
using Notification.Application.DTOs;

namespace Notification.Application.Commands;

/// <summary>
/// Command to get queue item status
/// </summary>
public record GetQueueItemStatusCommand(
    int QueueItemId
) : IRequest<QueueItemStatusResponse?>;

public class GetQueueItemStatusCommandValidator : AbstractValidator<GetQueueItemStatusCommand>
{
    public GetQueueItemStatusCommandValidator()
    {
        RuleFor(x => x.QueueItemId)
            .GreaterThan(0).WithMessage("QueueItemId must be greater than 0");
    }
}
