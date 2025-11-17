using FluentValidation;
using Identity.Application.DTOs;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<UserDtoIncludesToken>;

public class LoginCommandValidator : LocalizedValidator<LoginCommand>
{
    public LoginCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Email"))
            .EmailAddress().WithMessage(L(LocalizationKeys.Validation.EmailInvalid));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Password"))
            .MinimumLength(6).WithMessage(L(LocalizationKeys.Validation.MinLength, "Password", "6"));
    }
}
