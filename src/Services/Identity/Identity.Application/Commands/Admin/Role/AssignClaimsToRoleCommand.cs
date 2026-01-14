using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands.Admin.Role;

public record AssignClaimsToRoleCommand(
    int RoleId,
    List<int> ClaimIds
) : IRequest<bool>;

public class AssignClaimsToRoleCommandValidator : LocalizedValidator<AssignClaimsToRoleCommand>
{
    public AssignClaimsToRoleCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "Role ID", "0"));

        RuleFor(x => x.ClaimIds)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Claims"))
            .Must(claims => claims != null && claims.Any()).WithMessage(L(LocalizationKeys.Validation.Required, "Claims"));
    }
}
