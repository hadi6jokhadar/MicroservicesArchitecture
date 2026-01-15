using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;
// Delete User Command (Admin)
public record DeleteUserCommand(
    int Id
) : IRequest<bool>;

public class DeleteUserCommandValidator : LocalizedValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator(ILocalizationService localizationService) 
        : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.UserId), 0));
    }
}
