using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Nasheed.Application.Commands;

namespace Nasheed.Application.Validators;

public class CreateArtistCommandValidator : LocalizedValidator<CreateArtistCommand>
{
    public CreateArtistCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Name"))
            .MaximumLength(200).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Name", 200));
    }
}

public class UpdateArtistCommandValidator : LocalizedValidator<UpdateArtistCommand>
{
    public UpdateArtistCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.Required, "Id"));
    }
}
