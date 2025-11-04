using MediatR;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.DTOs;
using FluentValidation;

namespace Identity.Application.Commands;

public record UpdateProfileCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? ProfilePictureUrl,
    int? Id,
    string? Data = null
) : IRequest<UserDto>;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(50).WithMessage("First name cannot exceed 50 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
