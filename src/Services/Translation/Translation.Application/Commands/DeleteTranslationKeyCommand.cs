using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Translation.Application.Commands;

/// <summary>
/// Command to delete a translation key
/// </summary>
public record DeleteTranslationKeyCommand(int Id) : IRequest<bool>;

public class DeleteTranslationKeyCommandValidator : LocalizedValidator<DeleteTranslationKeyCommand>
{
    public DeleteTranslationKeyCommandValidator(IhsanDev.Shared.Application.Localization.ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.Id), "0"));
    }
}
