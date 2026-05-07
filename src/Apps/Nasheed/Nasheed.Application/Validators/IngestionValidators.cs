using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Nasheed.Application.Queries;

namespace Nasheed.Application.Validators;

public class GetIngestionJobListQueryValidator : LocalizedValidator<GetIngestionJobListQuery>
{
    public GetIngestionJobListQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "PageNumber", 0));
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThanOrEqual, "PageSize", 1))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.MustBeLessThanOrEqual, "PageSize", 100));
    }
}
