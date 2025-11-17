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
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Phone number"))
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage(L(LocalizationKeys.Validation.PhoneNumberInvalid));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "First name"))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, "First name", "100"))
            .Matches(@"^[a-zA-Z\s]+$").WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "First name (letters only)"));

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Last name"))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Last name", "100"))
            .Matches(@"^[a-zA-Z\s]+$").WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "Last name (letters only)"));
    }
}
