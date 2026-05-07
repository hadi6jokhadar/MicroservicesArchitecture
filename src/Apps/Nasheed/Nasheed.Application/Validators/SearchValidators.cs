using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Nasheed.Application.Queries;

namespace Nasheed.Application.Validators;

public class SearchSongsQueryValidator : LocalizedValidator<SearchSongsQuery>
{
    public SearchSongsQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Query"));
        RuleFor(x => x.TopN)
            .GreaterThanOrEqualTo(1).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThanOrEqual, "TopN", 1))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.MustBeLessThanOrEqual, "TopN", 100));
    }
}

public class GetSimilarSongsQueryValidator : LocalizedValidator<GetSimilarSongsQuery>
{
    public GetSimilarSongsQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.SongId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "SongId", 0));
        RuleFor(x => x.TopN)
            .GreaterThanOrEqualTo(1).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThanOrEqual, "TopN", 1))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.MustBeLessThanOrEqual, "TopN", 100));
    }
}
