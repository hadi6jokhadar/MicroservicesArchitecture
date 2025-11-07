using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using Notification.Application.DTOs;
using Notification.Domain.Enums;
using MediatR;

namespace Notification.Application.Commands;

/// <summary>
/// Query to get all queue items with pagination and filters (SuperAdmin only)
/// </summary>
public record GetQueueItemsCommand(
    int PageNumber = 1,
    int PageSize = 10,
    string? TenantId = null,
    int? UserId = null,
    QueueStatus? Status = null,
    Priority? Priority = null,
    DeliveryType? DeliveryType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? SearchTerm = null
) : IRequest<PaginatedList<QueueItemDto>>;

public class GetQueueItemsCommandValidator : AbstractValidator<GetQueueItemsCommand>
{
    public GetQueueItemsCommandValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Page size cannot exceed 100");

        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate ?? DateTime.MaxValue)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("FromDate must be less than or equal to ToDate");
    }
}
