using FluentValidation;
using MediatR;

namespace Tenant.Application.Commands.Tenant;

/// <summary>
/// Command to delete a tenant
/// </summary>
public record DeleteTenantCommand(string TenantId) : IRequest<bool>;

public class DeleteTenantCommandValidator : AbstractValidator<DeleteTenantCommand>
{
    public DeleteTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");
    }
}
