using FluentValidation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record LoginWithCodeByEmailCommand(
    string Email,
    string VerificationCode
) : IRequest<UserDtoIncludesToken>;

public class LoginWithCodeByEmailCommandValidator : AbstractValidator<LoginWithCodeByEmailCommand>
{
    public LoginWithCodeByEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email address is required");

        RuleFor(x => x.VerificationCode)
            .NotEmpty().WithMessage("Verification code is required")
            .Length(5).WithMessage("Verification code must be 5 digits")
            .Matches(@"^\d{5}$").WithMessage("Verification code must contain only digits");
    }
}
