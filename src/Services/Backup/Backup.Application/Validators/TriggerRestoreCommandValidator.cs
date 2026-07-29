using Backup.Application.Commands;
using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;

namespace Backup.Application.Validators;

public class TriggerRestoreCommandValidator : LocalizedValidator<TriggerRestoreCommand>
{
    public TriggerRestoreCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Confirm)
            .Equal(true)
            .WithMessage(L(LocalizationKeys.Validation.ConfirmationRequired));
    }
}
