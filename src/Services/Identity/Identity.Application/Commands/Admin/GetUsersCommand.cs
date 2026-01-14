using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

public record GetUsersCommand(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    string? RoleName = null,
    bool? Status = null
) : IRequest<PaginatedList<UserDto>>;

public class GetUsersCommandValidator : LocalizedValidator<GetUsersCommand>
{
    public GetUsersCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "Page number", "0"));

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "Page size", "0"))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.PageSizeExceeded));
    }
}