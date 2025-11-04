using FluentValidation;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record RegisterWithCodeByEmailCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Data = null
) : IRequest<bool>;

public class RegisterWithCodeByEmailCommandValidator : AbstractValidator<RegisterWithCodeByEmailCommand>
{
    public RegisterWithCodeByEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z\s]+$").WithMessage("First name can only contain letters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z\s]+$").WithMessage("Last name can only contain letters");
    }
}
