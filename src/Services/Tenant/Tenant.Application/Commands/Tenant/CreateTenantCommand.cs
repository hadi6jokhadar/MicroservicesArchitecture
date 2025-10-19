using FluentValidation;
using MediatR;
using Tenant.Application.DTOs;

namespace Tenant.Application.Commands.Tenant;

/// <summary>
/// Command to create a new tenant
/// </summary>
public record CreateTenantCommand(
    string TenantId,
    string TenantName,
    int UserId,
    DateTime StartDate,
    DateTime ExpireDate,
    string Data
) : IRequest<TenantDto>;

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required")
            .MaximumLength(50).WithMessage("Tenant ID must not exceed 50 characters")
            .Matches(@"^[a-z0-9-]+$").WithMessage("Tenant ID can only contain lowercase letters, numbers, and hyphens");

        RuleFor(x => x.TenantName)
            .NotEmpty().WithMessage("Tenant name is required")
            .MaximumLength(200).WithMessage("Tenant name must not exceed 200 characters");

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User ID must be greater than 0");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.ExpireDate)
            .NotEmpty().WithMessage("Expire date is required")
            .GreaterThan(x => x.StartDate).WithMessage("Expire date must be after start date");

        RuleFor(x => x.Data)
            .NotEmpty().WithMessage("Configuration data is required")
            .Must(BeValidJson).WithMessage("Data must be valid JSON");
    }

    private bool BeValidJson(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return false;
        
        try
        {
            System.Text.Json.JsonDocument.Parse(data);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
