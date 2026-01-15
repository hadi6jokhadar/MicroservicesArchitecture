using MediatR;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using FluentValidation;

namespace Identity.Application.Commands;

public record UpdateProfileCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    int? ProfilePictureId,
    int? Id,
    string? Data = null
) : IRequest<UserDto>;

public class UpdateProfileCommandValidator : LocalizedValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.FirstName)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.FirstName), "100"));

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.LastName)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.LastName), "100"));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage(L(LocalizationKeys.Validation.PhoneNumberInvalid))
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
