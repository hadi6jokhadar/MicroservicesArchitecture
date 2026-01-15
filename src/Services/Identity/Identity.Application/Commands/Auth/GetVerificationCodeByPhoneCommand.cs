using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record GetVerificationCodeByPhoneCommand(
    string PhoneNumber
) : IRequest<bool>;

public class GetVerificationCodeByPhoneCommandValidator : LocalizedValidator<GetVerificationCodeByPhoneCommand>
{
    public GetVerificationCodeByPhoneCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.PhoneNumber)))
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage(L(LocalizationKeys.Validation.PhoneNumberInvalid));
    }
}
