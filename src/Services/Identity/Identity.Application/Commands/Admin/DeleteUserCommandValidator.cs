using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.DTOs;
using IhsanDev.Shared.Kernel.Enums.Identity;
using MediatR;

namespace Identity.Application.Commands;
// Delete User Command (Admin)
public record DeleteUserCommand(
    int Id
) : IRequest<bool>;

public class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("User ID is required");
    }
}
