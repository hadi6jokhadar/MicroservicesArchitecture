using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;
public record ToggleUserStatusCommand(
    int UserId
) : IRequest<UserDto>;

public class ToggleUserStatusCommandValidator : LocalizedValidator<ToggleUserStatusCommand>
{
    public ToggleUserStatusCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "User ID", "0"));
    }
}