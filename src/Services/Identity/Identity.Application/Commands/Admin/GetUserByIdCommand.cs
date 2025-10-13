using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

public record GetUserByIdCommand(
    int UserId
) : IRequest<UserDto>;

public class GetUserByIdCommandValidator : AbstractValidator<GetUserByIdCommand>
{
    public GetUserByIdCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User ID is required");
    }
}