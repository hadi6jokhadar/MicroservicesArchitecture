using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.DTOs;
using IhsanDev.Shared.Kernel.Enums.Identity;
using MediatR;

namespace Identity.Application.Commands;

public record GetUsersCommand(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    UserRole? Role = null,
    bool? Status = null
) : IRequest<PaginatedList<UserDto>>;

public class GetUsersCommandValidator : AbstractValidator<GetUsersCommand>
{
    public GetUsersCommandValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Page size cannot exceed 100");
    }
}