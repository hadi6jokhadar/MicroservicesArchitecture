using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Tenant.Application.Commands.Tenant;

/// <summary>
/// Command to delete a tenant
/// </summary>
public record DeleteTenantCommand(string TenantId) : IRequest<bool>;

public class DeleteTenantCommandValidator : LocalizedValidator<DeleteTenantCommand>
{
    public DeleteTenantCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.TenantId)));
    }
}
