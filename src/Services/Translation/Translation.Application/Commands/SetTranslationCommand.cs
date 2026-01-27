using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;
using Translation.Application.DTOs;

namespace Translation.Application.Commands;

/// <summary>
/// Command to set/update a translation value
/// If TenantId is null, it's a global translation
/// If TenantId is provided, it's a tenant-specific override
/// </summary>
public record SetTranslationCommand(
    string Key,
    string Language,
    string Value,
    string? TenantId = null,
    string Category = "General"
) : IRequest<TranslationValueDto>;

public class SetTranslationCommandValidator : LocalizedValidator<SetTranslationCommand>
{
    public SetTranslationCommandValidator(IhsanDev.Shared.Application.Localization.ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Key)))
            .MaximumLength(200).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Key), "200"));

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Language)))
            .MaximumLength(10).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Language), "10"));

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Value)))
            .MaximumLength(2000).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Value), "2000"));

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Category)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Category), "100"));
    }
}
