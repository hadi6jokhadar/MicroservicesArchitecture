using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Nasheed.Application.Commands;
using Nasheed.Application.Queries;

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

public class GetArtistListQueryValidator : LocalizedValidator<GetArtistListQuery>
{
    public GetArtistListQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "PageNumber", 0));
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThanOrEqual, "PageSize", 1))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.MustBeLessThanOrEqual, "PageSize", 100));
    }
}
