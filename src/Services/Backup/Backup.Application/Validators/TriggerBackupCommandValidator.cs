using Backup.Application.Commands;
using Backup.Domain.Enums;
using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;

namespace Backup.Application.Validators;

public class TriggerBackupCommandValidator : LocalizedValidator<TriggerBackupCommand>
{
    public TriggerBackupCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "ServiceName"))
            .When(x => x.Scope == BackupScope.GlobalService);

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "TenantId"))
            .When(x => x.Scope == BackupScope.Tenant);
    }
}
