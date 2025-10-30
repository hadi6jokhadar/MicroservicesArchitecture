using FluentValidation;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record GetVerificationCodeByEmailCommand(
    string Email
) : IRequest<bool>;

public class GetVerificationCodeByEmailCommandValidator : AbstractValidator<GetVerificationCodeByEmailCommand>
{
    public GetVerificationCodeByEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email address is required");
    }
}
