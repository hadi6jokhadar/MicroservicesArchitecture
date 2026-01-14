using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands.Admin.Role;

public record DeleteRoleCommand(int Id) : IRequest<bool>;

public class DeleteRoleCommandValidator : LocalizedValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "Role ID", "0"));
    }
}
