using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.Commands;

public record GetUserProfileCommand(
    int UserId
) : IRequest<UserDto>;

public class GetUserProfileQueryValidator : AbstractValidator<GetUserProfileCommand>
{
    public GetUserProfileQueryValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User ID is required");
    }
}