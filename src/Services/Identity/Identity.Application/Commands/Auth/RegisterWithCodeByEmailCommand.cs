using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record RegisterWithCodeByEmailCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Data = null
) : IRequest<bool>;

public class RegisterWithCodeByEmailCommandValidator : LocalizedValidator<RegisterWithCodeByEmailCommand>
{
    public RegisterWithCodeByEmailCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Email)))
            .EmailAddress().WithMessage(L(LocalizationKeys.Validation.EmailInvalid))
            .MaximumLength(256).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Email), "256"));

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
