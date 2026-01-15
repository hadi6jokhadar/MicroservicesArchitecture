using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

public record GetUserProfileCommand(
    int UserId
) : IRequest<UserDto>;

public class GetUserProfileQueryValidator : LocalizedValidator<GetUserProfileCommand>
{
    public GetUserProfileQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.UserId), "0"));
    }
}