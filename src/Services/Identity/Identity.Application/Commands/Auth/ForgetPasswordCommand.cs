using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

public record ForgetPasswordCommand(
    string Email
) : IRequest<string>;

public class ForgetPasswordCommandValidator : AbstractValidator<ForgetPasswordCommand>
{
    public ForgetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email address is required");
    }
}