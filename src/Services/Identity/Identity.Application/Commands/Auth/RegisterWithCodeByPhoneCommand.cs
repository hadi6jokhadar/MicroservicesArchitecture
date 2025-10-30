using FluentValidation;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record RegisterWithCodeByPhoneCommand(
    string PhoneNumber,
    string FirstName,
    string LastName
) : IRequest<bool>;

public class RegisterWithCodeByPhoneCommandValidator : AbstractValidator<RegisterWithCodeByPhoneCommand>
{
    public RegisterWithCodeByPhoneCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format");

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
