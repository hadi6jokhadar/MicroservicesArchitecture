using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

public record RefreshTokenCommand(
    string RefreshToken
) : IRequest<UserDtoIncludesToken>;

public class RefreshTokenCommandValidator : LocalizedValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Refresh token"));
    }
}