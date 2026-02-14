using FileManager.Application.Commands;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using FluentValidation;

namespace FileManager.Application.Validators;

public class UpdateFileCommandValidator : LocalizedValidator<UpdateFileCommand>
{
    public UpdateFileCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "0"));

        When(x => x.Group.HasValue, () =>
        {
            RuleFor(x => x.Group!.Value)
                .IsInEnum()
                .WithMessage(L(LocalizationKeys.Validation.GroupInvalid));
        });
    }
}
