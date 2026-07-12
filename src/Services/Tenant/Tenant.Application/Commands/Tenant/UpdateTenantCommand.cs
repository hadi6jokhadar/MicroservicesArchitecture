using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Utilities;
using MediatR;
using Tenant.Application.DTOs;

namespace Tenant.Application.Commands.Tenant;

/// <summary>
/// Command to update tenant settings
/// </summary>
public record UpdateTenantCommand(
    string TenantId,
    string TenantName,
    DateTime StartDate,
    DateTime ExpireDate,
    TenantConfiguration Data,
    bool IsActive
) : IRequest<TenantDto>;

public class UpdateTenantCommandValidator : LocalizedValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.TenantId)))
            .MaximumLength(50).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.TenantId), "50"));

        RuleFor(x => x.TenantName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.TenantName)))
            .MaximumLength(200).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.TenantName), "200"));

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.StartDate)));

        RuleFor(x => x.ExpireDate)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.ExpireDate)))
            .GreaterThan(x => x.StartDate).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.ExpireDate), L(LocalizationKeys.Fields.StartDate)));

        RuleFor(x => x.Data)
            .NotNull().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.ConfigurationData)));

        RuleFor(x => x.Data.TimeZoneId)
            .Must(id => string.IsNullOrWhiteSpace(id) || TenantTimeZoneResolver.IsValidTimeZoneId(id))
            .WithMessage(L(LocalizationKeys.Validation.InvalidTimeZone, L(LocalizationKeys.Fields.TimeZoneId)))
            .When(x => x.Data != null);
    }
}
