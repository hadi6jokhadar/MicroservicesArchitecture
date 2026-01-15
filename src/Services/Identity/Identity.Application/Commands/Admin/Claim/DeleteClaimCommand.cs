using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;

namespace Identity.Application.Commands.Admin.Claim;

public record DeleteClaimCommand(int Id) : IRequest<bool>;

public class DeleteClaimCommandValidator : LocalizedValidator<DeleteClaimCommand>
{
    public DeleteClaimCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.ClaimId), "0"));
    }
}
