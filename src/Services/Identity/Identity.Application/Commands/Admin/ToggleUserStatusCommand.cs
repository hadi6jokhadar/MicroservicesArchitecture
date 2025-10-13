using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.DTOs;
using IhsanDev.Shared.Kernel.Enums.Identity;
using MediatR;

namespace Identity.Application.Commands;
public record ToggleUserStatusCommand(
    int UserId
) : IRequest<UserDto>;

public class ToggleUserStatusCommandValidator : AbstractValidator<ToggleUserStatusCommand>
{
    public ToggleUserStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User ID is required");
    }
}