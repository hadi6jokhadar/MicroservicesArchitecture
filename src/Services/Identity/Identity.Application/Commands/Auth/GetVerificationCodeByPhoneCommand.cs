using FluentValidation;
using MediatR;

namespace Identity.Application.Commands.Auth;

public record GetVerificationCodeByPhoneCommand(
    string PhoneNumber
) : IRequest<bool>;

public class GetVerificationCodeByPhoneCommandValidator : AbstractValidator<GetVerificationCodeByPhoneCommand>
{
    public GetVerificationCodeByPhoneCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format");
    }
}
