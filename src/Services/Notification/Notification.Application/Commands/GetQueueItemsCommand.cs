using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
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
    string? SearchTerm = null,
    bool IsArchived = false
) : IRequest<PaginatedList<QueueItemDto>>;

public class GetQueueItemsCommandValidator : LocalizedValidator<GetQueueItemsCommand>
{
    public GetQueueItemsCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Validation.PageNumber), "0"));

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Validation.PageSize), "0"))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.PageSizeExceeded));

        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate ?? DateTime.MaxValue)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage(L(LocalizationKeys.Validation.DateRangeInvalid));
    }
}
