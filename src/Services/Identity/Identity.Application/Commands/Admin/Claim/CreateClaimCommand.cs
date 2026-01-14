using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands.Admin.Claim;

public record CreateClaimCommand(
    string Name,
    string ClaimType,
    string ClaimValue,
    bool IsSuperAdminOnly = false,
    string? Description = null
) : IRequest<ClaimDto>;

public class CreateClaimCommandValidator : LocalizedValidator<CreateClaimCommand>
{
    public CreateClaimCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Claim name"))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Claim name", "100"));

        RuleFor(x => x.ClaimType)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Claim type"))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Claim type", "100"));

        RuleFor(x => x.ClaimValue)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Claim value"))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Claim value", "100"));

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Description", "500"))
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
