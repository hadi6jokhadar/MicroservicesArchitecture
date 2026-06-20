using FluentValidation;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;
using Tenant.Application.DTOs;

namespace Tenant.Application.Commands.Tenant;

/// <summary>
/// Query to get tenant configuration by tenant ID (includes Data field)
/// </summary>
public record GetTenantConfigQuery(string TenantId) : IRequest<TenantConfigDto?>;

public class GetTenantConfigQueryValidator : LocalizedValidator<GetTenantConfigQuery>
{
    public GetTenantConfigQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.TenantId)));
    }
}

/// <summary>
/// Query to get tenant by tenant ID (excludes Data field)
/// </summary>
public record GetTenantByIdQuery(string TenantId) : IRequest<TenantDto?>;

public class GetTenantByIdQueryValidator : LocalizedValidator<GetTenantByIdQuery>
{
    public GetTenantByIdQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.TenantId)));
    }
}

/// <summary>
/// Query to get tenant by user ID
/// </summary>
public record GetTenantByUserQuery(int UserId) : IRequest<TenantDto?>;

public class GetTenantByUserQueryValidator : LocalizedValidator<GetTenantByUserQuery>
{
    public GetTenantByUserQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.UserId), "0"));
    }
}

/// <summary>
/// Query to get all active tenants with pagination
/// </summary>
public record GetAllActiveTenantsQuery(int PageNumber = 1, int PageSize = 10, bool IsArchived = false) : IRequest<PaginatedList<TenantDto>>;

public class GetAllActiveTenantsQueryValidator : LocalizedValidator<GetAllActiveTenantsQuery>
{
    public GetAllActiveTenantsQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Validation.PageNumber), "0"));

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Validation.PageSize), "0"))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.PageSizeExceeded));
    }
}

/// <summary>
/// Query to get all active tenants with configuration (includes Data field)
/// </summary>
public record GetAllActiveTenantsWithConfigQuery(int PageNumber = 1, int PageSize = 100) : IRequest<PaginatedList<TenantConfigDto>>;

/// <summary>
/// Public query to get feature flags for a tenant. TenantId is optional — returns defaults when omitted.
/// </summary>
public record GetTenantFeatureFlagsQuery(string? TenantId) : IRequest<Dictionary<string, bool>>;

public class GetAllActiveTenantsWithConfigQueryValidator : LocalizedValidator<GetAllActiveTenantsWithConfigQuery>
{
    public GetAllActiveTenantsWithConfigQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Validation.PageNumber), "0"));

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Validation.PageSize), "0"))
            .LessThanOrEqualTo(1000).WithMessage(L(LocalizationKeys.Validation.PageSizeExceeded));
    }
}
