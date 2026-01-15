using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record RegisterWithCodeByPhoneCommand(
    string PhoneNumber,
    string FirstName,
    string LastName,
    string? Data = null
) : IRequest<bool>;

public class RegisterWithCodeByPhoneCommandValidator : LocalizedValidator<RegisterWithCodeByPhoneCommand>
{
    public RegisterWithCodeByPhoneCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.PhoneNumber)))
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage(L(LocalizationKeys.Validation.PhoneNumberInvalid));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.FirstName)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.FirstName), "100"))
            .Matches(@"^[a-zA-Z\s]+$").WithMessage(L(LocalizationKeys.Validation.FirstNameLettersOnly));

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.LastName)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.LastName), "100"))
            .Matches(@"^[a-zA-Z\s]+$").WithMessage(L(LocalizationKeys.Validation.LastNameLettersOnly));
    }
}
