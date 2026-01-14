using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands.Admin.Role;

public record CreateRoleCommand(
    string Name,
    string? Description = null
) : IRequest<RoleDto>;

public class CreateRoleCommandValidator : LocalizedValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Role name"))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Role name", "100"));

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Description", "500"))
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
