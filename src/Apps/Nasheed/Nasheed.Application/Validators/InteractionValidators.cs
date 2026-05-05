using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Nasheed.Application.Commands;

namespace Nasheed.Application.Validators;

public class AddRatingCommandValidator : LocalizedValidator<AddRatingCommand>
{
    public AddRatingCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.SongId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.Required, "SongId"));

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.Required, "UserId"));

        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(1).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThanOrEqual, "Value", 1))
            .LessThanOrEqualTo(5).WithMessage(L(LocalizationKeys.Validation.MustBeLessThanOrEqual, "Value", 5));
    }
}

public class GenerateLyricsCommandValidator : LocalizedValidator<GenerateLyricsCommand>
{
    public GenerateLyricsCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Theme)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Theme"))
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Theme", 500));
    }
}
