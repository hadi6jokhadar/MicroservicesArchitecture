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
    TenantConfiguration Data
) : IRequest<TenantDto>;

public class CreateTenantCommandValidator : LocalizedValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Tenant ID"))
            .MaximumLength(50).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Tenant ID", "50"))
            .Matches(@"^[a-z0-9-]+$").WithMessage(L(LocalizationKeys.Validation.TenantIdFormat));

        RuleFor(x => x.TenantName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Tenant name"))
            .MaximumLength(200).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Tenant name", "200"));

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "User ID", "0"));

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Start date"));

        RuleFor(x => x.ExpireDate)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Expire date"))
            .GreaterThan(x => x.StartDate).WithMessage(L(LocalizationKeys.Validation.MustBeAfter, "Expire date", "start date"));

        RuleFor(x => x.Data)
            .NotNull().WithMessage(L(LocalizationKeys.Validation.Required, "Configuration data"));
    }
}
