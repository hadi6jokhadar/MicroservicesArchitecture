using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Nasheed.Application.Commands;

namespace Nasheed.Application.Validators;

public class CreateSongCommandValidator : LocalizedValidator<CreateSongCommand>
{
    public CreateSongCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.ArtistId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.Required, "ArtistId"));

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Title"))
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Title", 500));

        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "FileId"));
    }
}

public class UpdateSongCommandValidator : LocalizedValidator<UpdateSongCommand>
{
    public UpdateSongCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.Required, "Id"));

        RuleFor(x => x.Title)
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Title", 500))
            .When(x => x.Title != null);
    }
}
