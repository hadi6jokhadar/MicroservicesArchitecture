using MediatR;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using FluentValidation;

namespace Identity.Application.Commands;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Data = null
) : IRequest<UserDtoIncludesToken>;


public class RegisterCommandValidator : LocalizedValidator<RegisterCommand>
{
    public RegisterCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Email"))
            .EmailAddress().WithMessage(L(LocalizationKeys.Validation.EmailInvalid))
            .MaximumLength(256).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Email", "256"));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Password"))
            .MinimumLength(8).WithMessage(L(LocalizationKeys.Validation.PasswordTooShort, "8"))
            .Matches(@"[A-Z]").WithMessage(L(LocalizationKeys.Validation.PasswordRequiresUppercase))
            .Matches(@"[a-z]").WithMessage(L(LocalizationKeys.Validation.PasswordRequiresLowercase))
            .Matches(@"[0-9]").WithMessage(L(LocalizationKeys.Validation.PasswordRequiresDigit))
            .Matches(@"[\W_]").WithMessage(L(LocalizationKeys.Validation.PasswordRequiresNonAlphanumeric));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "First name"))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, "First name", "100"))
            .Matches(@"^[a-zA-Z\s]+$").WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "First name (letters only)"));

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Last name"))
            .MaximumLength(100).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Last name", "100"))
            .Matches(@"^[a-zA-Z\s]+$").WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "Last name (letters only)"));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage(L(LocalizationKeys.Validation.PhoneNumberInvalid))
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}