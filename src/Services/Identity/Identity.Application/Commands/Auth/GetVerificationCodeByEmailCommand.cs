using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record GetVerificationCodeByEmailCommand(
    string Email
) : IRequest<bool>;

public class GetVerificationCodeByEmailCommandValidator : LocalizedValidator<GetVerificationCodeByEmailCommand>
{
    public GetVerificationCodeByEmailCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Email"))
            .EmailAddress().WithMessage(L(LocalizationKeys.Validation.EmailInvalid));
    }
}
