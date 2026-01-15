using FileManager.Application.Commands;
using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;

namespace FileManager.Application.Handlers.SaveFile;

public class SaveFileCommandValidator : LocalizedValidator<SaveFileCommand>
{
    public SaveFileCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.File)));

        RuleFor(x => x.Group)
            .IsInEnum()
            .WithMessage(L(LocalizationKeys.Validation.GroupInvalid));
    }
}
