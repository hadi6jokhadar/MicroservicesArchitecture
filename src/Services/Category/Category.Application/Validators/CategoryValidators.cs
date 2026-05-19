using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Category.Application.Commands;

namespace Category.Application.Validators;

public class CreateCategoryCommandValidator : LocalizedValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator(ILocalizationService localizationService)
        : base(localizationService)
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Slug"))
            .MaximumLength(200)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "Slug", 200))
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens only.");

        RuleFor(x => x.Uri)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Uri"))
            .MaximumLength(300)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "Uri", 300))
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Uri must be lowercase alphanumeric with hyphens only.");

        RuleFor(x => x.NameTranslations)
            .NotNull()
            .WithMessage(L(LocalizationKeys.Validation.Required, "NameTranslations"))
            .Must(d => d != null && d.Count > 0)
            .WithMessage("At least one name translation is required.");

        RuleFor(x => x.ParentId)
            .GreaterThan(0)
            .When(x => x.ParentId.HasValue)
            .WithMessage("ParentId must be a positive integer when provided.");
    }
}

public class UpdateCategoryCommandValidator : LocalizedValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator(ILocalizationService localizationService)
        : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.Required, "Id"));

        RuleFor(x => x.Slug)
            .MaximumLength(200)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "Slug", 200))
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens only.")
            .When(x => x.Slug != null);

        RuleFor(x => x.Uri)
            .MaximumLength(300)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "Uri", 300))
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Uri must be lowercase alphanumeric with hyphens only.")
            .When(x => x.Uri != null);
    }
}

public class MoveCategoryCommandValidator : LocalizedValidator<MoveCategoryCommand>
{
    public MoveCategoryCommandValidator(ILocalizationService localizationService)
        : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.Required, "Id"));

        RuleFor(x => x.NewParentId)
            .GreaterThan(0)
            .When(x => x.NewParentId.HasValue)
            .WithMessage("NewParentId must be a positive integer when provided.");

        RuleFor(x => x)
            .Must(x => x.Id != x.NewParentId)
            .WithMessage("A category cannot be its own parent.");
    }
}
