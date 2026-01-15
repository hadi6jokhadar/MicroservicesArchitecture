using FileManager.Application.Commands;
using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;

namespace FileManager.Application.Handlers.UpdateFile;

public class UpdateFileCommandValidator : LocalizedValidator<UpdateFileCommand>
{
    public UpdateFileCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.FileId), "0"));

        RuleFor(x => x.Group)
            .IsInEnum()
            .When(x => x.Group.HasValue)
            .WithMessage(L(LocalizationKeys.Validation.GroupInvalid));

        RuleFor(x => x.Name)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.Name))
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.FileName), "255"));
    }
}
