using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

// Create User Command (Admin)
public record CreateUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    List<int> RoleIds,
    string? PhoneNumber = null,
    int? ProfilePictureId = null,
    string? Data = null
) : IRequest<UserDto>;

public class CreateUserCommandValidator : LocalizedValidator<CreateUserCommand>
{
    public CreateUserCommandValidator(ILocalizationService localizationService) 
        : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Email)))
            .EmailAddress().WithMessage(L(LocalizationKeys.Validation.EmailInvalid));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Password)))
            .MinimumLength(8).WithMessage(L(LocalizationKeys.Validation.PasswordTooShort, 8))
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]")
            .WithMessage(L(LocalizationKeys.Validation.PasswordRequiresUppercase));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.FirstName)))
            .MaximumLength(50).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.FirstName), 50));

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.LastName)))
            .MaximumLength(50).WithMessage(L(LocalizationKeys.Validation.MaxLength, L(LocalizationKeys.Fields.LastName), 50));

        RuleFor(x => x.RoleIds)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Roles)))
            .Must(roleIds => roleIds != null && roleIds.Any()).WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Roles)));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage(L(LocalizationKeys.Validation.PhoneNumberInvalid))
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}