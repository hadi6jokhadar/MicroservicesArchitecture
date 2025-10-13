using FluentValidation;
using Identity.Application.DTOs;
using IhsanDev.Shared.Application.Common.Models;
using MediatR;

namespace Identity.Application.Commands;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<UserDtoIncludesToken>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email address is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters");
    }
}
