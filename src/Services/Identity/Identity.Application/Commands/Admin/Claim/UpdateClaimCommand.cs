using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands.Admin.Claim;

public record UpdateClaimCommand(
    int Id,
    string Name,
    string ClaimType,
    string ClaimValue,
    bool IsSuperAdminOnly = false,
    string? Description = null
) : IRequest<ClaimDto>;

public class UpdateClaimCommandValidator : LocalizedValidator<UpdateClaimCommand>
{
    public UpdateClaimCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.ClaimId), "0"));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.ClaimName)))
            .MaximumLength(256).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.ClaimName), "256"));

        RuleFor(x => x.ClaimType)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.ClaimType)))
            .MaximumLength(256).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.ClaimType), "256"));

        RuleFor(x => x.ClaimValue)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.ClaimValue)))
            .MaximumLength(256).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.ClaimValue), "256"));

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Description), "500"))
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
