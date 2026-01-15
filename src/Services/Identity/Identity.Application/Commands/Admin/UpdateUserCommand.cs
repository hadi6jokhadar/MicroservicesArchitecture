using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

// Update User Command (Admin)
public record UpdateUserCommand(
    int Id,
    string FirstName,
    string LastName,
    List<int> RoleIds,
    string? PhoneNumber = null,
    int? ProfilePictureId = null,
    bool? EmailConfirmed = null,
    bool? Status = null,
    string? Data = null
) : IRequest<UserDto>;

public class UpdateUserCommandValidator : LocalizedValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.UserId), "0"));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.FirstName)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.FirstName), "100"));

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.LastName)))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.LastName), "100"));

        RuleFor(x => x.RoleIds)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Roles)))
            .Must(roleIds => roleIds != null && roleIds.Any()).WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Roles)));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage(L(LocalizationKeys.Validation.PhoneNumberInvalid))
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
