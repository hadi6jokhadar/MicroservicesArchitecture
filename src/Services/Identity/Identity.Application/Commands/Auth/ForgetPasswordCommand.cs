using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

public record ForgetPasswordCommand(
    string Email
) : IRequest<string>;

public class ForgetPasswordCommandValidator : LocalizedValidator<ForgetPasswordCommand>
{
    public ForgetPasswordCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Email)))
            .EmailAddress().WithMessage(L(LocalizationKeys.Validation.EmailInvalid));
    }
}