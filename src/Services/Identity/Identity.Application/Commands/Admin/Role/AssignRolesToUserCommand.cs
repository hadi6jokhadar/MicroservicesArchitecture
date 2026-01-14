using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands.Admin.Role;

public record AssignRolesToUserCommand(
    int UserId,
    List<int> RoleIds
) : IRequest<bool>;

public class AssignRolesToUserCommandValidator : LocalizedValidator<AssignRolesToUserCommand>
{
    public AssignRolesToUserCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "User ID", "0"));

        RuleFor(x => x.RoleIds)
            .NotNull().WithMessage(L(LocalizationKeys.Validation.Required, "Roles"));
    }
}
