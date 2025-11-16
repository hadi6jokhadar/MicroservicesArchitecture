using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using MediatR;
using Tenant.Application.DTOs;

namespace Tenant.Application.Commands.Tenant;

/// <summary>
/// Query to get tenant configuration by tenant ID (includes Data field)
/// </summary>
public record GetTenantConfigQuery(string TenantId) : IRequest<TenantConfigDto?>;

public class GetTenantConfigQueryValidator : AbstractValidator<GetTenantConfigQuery>
{
    public GetTenantConfigQueryValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");
    }
}

/// <summary>
/// Query to get tenant by tenant ID (excludes Data field)
/// </summary>
public record GetTenantByIdQuery(string TenantId) : IRequest<TenantDto?>;

public class GetTenantByIdQueryValidator : AbstractValidator<GetTenantByIdQuery>
{
    public GetTenantByIdQueryValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");
    }
}

/// <summary>
/// Query to get tenant by user ID
/// </summary>
public record GetTenantByUserQuery(int UserId) : IRequest<TenantDto?>;

public class GetTenantByUserQueryValidator : AbstractValidator<GetTenantByUserQuery>
{
    public GetTenantByUserQueryValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User ID must be greater than 0");
    }
}

/// <summary>
/// Query to get all active tenants with pagination
/// </summary>
public record GetAllActiveTenantsQuery(int PageNumber = 1, int PageSize = 10) : IRequest<PaginatedList<TenantDto>>;

public class GetAllActiveTenantsQueryValidator : AbstractValidator<GetAllActiveTenantsQuery>
{
    public GetAllActiveTenantsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Page size must not exceed 100");
    }
}

/// <summary>
/// Query to get all active tenants with configuration (includes Data field)
/// </summary>
public record GetAllActiveTenantsWithConfigQuery(int PageNumber = 1, int PageSize = 100) : IRequest<PaginatedList<TenantConfigDto>>;

public class GetAllActiveTenantsWithConfigQueryValidator : AbstractValidator<GetAllActiveTenantsWithConfigQuery>
{
    public GetAllActiveTenantsWithConfigQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(1000).WithMessage("Page size must not exceed 1000");
    }
}
