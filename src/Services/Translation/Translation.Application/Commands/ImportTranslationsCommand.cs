using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Translation.Application.Commands;

/// <summary>
/// Command to bulk import translations from JSON format
/// </summary>
public record ImportTranslationsCommand(
    Dictionary<string, string> Translations,
    string Language,
    string? TenantId = null,
    string Category = "General"
) : IRequest<ImportTranslationsResult>;

public record ImportTranslationsResult(
    int TotalKeys,
    int CreatedKeys,
    int UpdatedValues,
    string Message
);

public class ImportTranslationsCommandValidator : LocalizedValidator<ImportTranslationsCommand>
{
    public ImportTranslationsCommandValidator(IhsanDev.Shared.Application.Localization.ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Translations)
            .NotNull().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Translations)))
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Translations)))
            .Must(dict => dict.Count <= 1000)
            .WithMessage(L(LocalizationKeys.Validation.MustBeLessThanOrEqual, L(LocalizationKeys.Fields.TranslationCount), "1000"));

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Language)))
            .MaximumLength(10).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Language), "10"));

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Category)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Category), "100"));
    }
}
