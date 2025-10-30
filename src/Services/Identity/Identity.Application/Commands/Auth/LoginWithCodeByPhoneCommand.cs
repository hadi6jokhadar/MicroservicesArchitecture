using FluentValidation;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record LoginWithCodeByPhoneCommand(
    string PhoneNumber,
    string VerificationCode
) : IRequest<UserDtoIncludesToken>;

public class LoginWithCodeByPhoneCommandValidator : AbstractValidator<LoginWithCodeByPhoneCommand>
{
    public LoginWithCodeByPhoneCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format");

        RuleFor(x => x.VerificationCode)
            .NotEmpty().WithMessage("Verification code is required")
            .Length(5).WithMessage("Verification code must be 5 digits")
            .Matches(@"^\d{5}$").WithMessage("Verification code must contain only digits");
    }
}
