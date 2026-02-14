using FileManager.Application.Commands;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using FluentValidation;

namespace FileManager.Application.Validators;

public class SaveFileCommandValidator : LocalizedValidator<SaveFileCommand>
{
    public SaveFileCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage(L(LocalizationKeys.Validation.Required, "File"));
            
        RuleFor(x => x.Group)
            .IsInEnum()
            .WithMessage(L(LocalizationKeys.Validation.GroupInvalid));
    }
}
