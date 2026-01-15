using FileManager.Application.Commands;
using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;

namespace FileManager.Application.Handlers.DeleteTempFiles;

public class DeleteOldTempFilesCommandValidator : LocalizedValidator<DeleteOldTempFilesCommand>
{
    public DeleteOldTempFilesCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.OlderThanDays)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.OlderThanDays), "0"));
    }
}
