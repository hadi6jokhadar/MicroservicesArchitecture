using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

public record RefreshTokenCommand(
    string RefreshToken
) : IRequest<UserDtoIncludesToken>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required");
    }
}