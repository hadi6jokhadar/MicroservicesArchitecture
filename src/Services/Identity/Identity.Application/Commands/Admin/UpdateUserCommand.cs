using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.DTOs;
using IhsanDev.Shared.Kernel.Enums.Identity;
using MediatR;

namespace Identity.Application.Commands;

// Update User Command (Admin)
public record UpdateUserCommand(
    int Id,
    string FirstName,
    string LastName,
    UserRole Role,
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
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "User ID", "0"));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "First name"))
            .MaximumLength(50).WithMessage(L(LocalizationKeys.Validation.MaxLength, "First name", "50"));

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Last name"))
            .MaximumLength(50).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Last name", "50"));

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage(L(LocalizationKeys.Validation.InvalidRole));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage(L(LocalizationKeys.Validation.PhoneNumberInvalid))
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
