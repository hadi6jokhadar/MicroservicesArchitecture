using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;
using Translation.Application.DTOs;

namespace Translation.Application.Commands;

/// <summary>
/// Command to update an existing translation key
/// </summary>
public record UpdateTranslationKeyCommand(
    int Id,
    string? Description = null
) : IRequest<TranslationKeyDto>;

public class UpdateTranslationKeyCommandValidator : LocalizedValidator<UpdateTranslationKeyCommand>
{
    public UpdateTranslationKeyCommandValidator(IhsanDev.Shared.Application.Localization.ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.Id), "0"));

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.Description), "500"));
    }
}
