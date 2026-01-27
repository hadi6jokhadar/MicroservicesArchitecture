using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;
using Translation.Application.DTOs;

namespace Translation.Application.Commands;

/// <summary>
/// Command to create a new translation key
/// </summary>
public record CreateTranslationKeyCommand(
    string Key,
    string Category,
    string? Description = null
) : IRequest<TranslationKeyDto>;

public class CreateTranslationKeyCommandValidator : LocalizedValidator<CreateTranslationKeyCommand>
{
    public CreateTranslationKeyCommandValidator(IhsanDev.Shared.Application.Localization.ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Key)))
            .MaximumLength(200).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Key), "200"));

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Category)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Category), "100"));

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Description), "500"));
    }
}
