using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using MediatR;
using Tenant.Application.DTOs;

namespace Tenant.Application.Commands.Tenant;

/// <summary>
/// Command to create a new tenant
/// </summary>
public record CreateTenantCommand(
    string TenantId,
    string TenantName,
    int UserId,
    DateTime StartDate,
    DateTime ExpireDate,
    TenantConfiguration? Data,
    bool IsActive
) : IRequest<TenantDto>;

public class CreateTenantCommandValidator : LocalizedValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.TenantId)))
            .MaximumLength(50).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.TenantId), "50"))
            .Matches(@"^[a-z0-9-]+$").WithMessage(L(LocalizationKeys.Validation.TenantIdFormat));

        RuleFor(x => x.TenantName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.TenantName)))
            .MaximumLength(200).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.TenantName), "200"));

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.UserId), "0"));

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.StartDate)));

        RuleFor(x => x.ExpireDate)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.ExpireDate)))
            .GreaterThan(x => x.StartDate).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.ExpireDate), L(LocalizationKeys.Fields.StartDate)));
    }
}
