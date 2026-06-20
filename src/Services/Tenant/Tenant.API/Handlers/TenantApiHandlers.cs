using IhsanDev.Shared.Application.Localization;
using MediatR;
using Tenant.Application.Commands.Tenant;
using Tenant.Domain.Repositories;

namespace Tenant.API.Handlers;

public static class TenantApiHandlers
{
    /// <summary>
    /// Get tenant configuration by tenant ID (includes sensitive Data field)
    /// </summary>
    public static async Task<IResult> GetTenantConfigHandler(
        string tenantId,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var query = new GetTenantConfigQuery(tenantId);
        var result = await mediator.Send(query, ct);
        
        return result != null ? Results.Ok(result) : Results.NotFound(new { message = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFound, tenantId) });
    }

    /// <summary>
    /// Get tenant by tenant ID (excludes sensitive Data field)
    /// </summary>
    public static async Task<IResult> GetTenantByIdHandler(
        string tenantId,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var query = new GetTenantByIdQuery(tenantId);
        var result = await mediator.Send(query, ct);
        
        return result != null ? Results.Ok(result) : Results.NotFound(new { message = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFound, tenantId) });
    }

    /// <summary>
    /// Get tenant by user ID
    /// </summary>
    public static async Task<IResult> GetTenantByUserHandler(
        int userId,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var query = new GetTenantByUserQuery(userId);
        var result = await mediator.Send(query, ct);
        
        return result != null ? Results.Ok(result) : Results.NotFound(new { message = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFoundForUser, userId.ToString()) });
    }

    /// <summary>
    /// Get all active tenants with pagination
    /// </summary>
    public static async Task<IResult> GetAllActiveTenantsHandler(
        int pageNumber = 1,
        int pageSize = 100,
        bool isArchived = false,
        IMediator mediator = null!,
        CancellationToken ct = default)
    {
        var query = new GetAllActiveTenantsQuery(pageNumber, pageSize, isArchived);
        var result = await mediator.Send(query, ct);
        
        return Results.Ok(result);
    }

    /// <summary>
    /// Create new tenant
    /// </summary>
    public static async Task<IResult> CreateTenantHandler(
        CreateTenantCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Created($"/api/tenant/{result.TenantId}", result);
    }

    /// <summary>
    /// Update tenant settings
    /// </summary>
    public static async Task<IResult> UpdateTenantHandler(
        string tenantId,
        UpdateTenantCommand command,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        // Ensure tenantId from route matches command
        if (tenantId != command.TenantId)
        {
            return Results.BadRequest(new { message = localizationService.GetString(LocalizationKeys.Exceptions.TenantIdMismatch) });
        }

        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Delete tenant
    /// </summary>
    public static async Task<IResult> DeleteTenantHandler(
        string tenantId,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var command = new DeleteTenantCommand(tenantId);
        var result = await mediator.Send(command, ct);
        
        return Results.Ok(new { message = localizationService.GetString(LocalizationKeys.Success.TenantDeleted) });
    }

    /// <summary>
    /// Toggle tenant archived status
    /// </summary>
    public static async Task<IResult> ToggleTenantArchivedStatusHandler(
        string tenantId,
        IMediator mediator,
        ILocalizationService localizationService,
        ITenantRepository tenantRepository,
        CancellationToken ct = default)
    {
        // Need to find by tenant ID to get integer ID
        var tenant = await tenantRepository.GetByTenantIdAsync(tenantId, ct);
        if (tenant == null)
        {
            return Results.NotFound(new { message = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFound, tenantId) });
        }

        var command = new ToggleTenantArchivedStatusCommand(tenant.Id);
        var result = await mediator.Send(command, ct);
        
        return Results.Ok(result);
    }

    /// <summary>
    /// Get feature flags for a tenant (public endpoint).
    /// TenantId is optional — returns system defaults when omitted.
    /// </summary>
    public static async Task<IResult> GetTenantFeatureFlagsHandler(
        IMediator mediator,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var query = new GetTenantFeatureFlagsQuery(tenantId);
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Get all active tenants with configuration (Service/SuperAdmin only)
    /// </summary>
    public static async Task<IResult> GetAllActiveTenantsWithConfigHandler(
        int pageNumber = 1,
        int pageSize = 100,
        IMediator mediator = null!,
        CancellationToken ct = default)
    {
        var query = new GetAllActiveTenantsWithConfigQuery(pageNumber, pageSize);
        var result = await mediator.Send(query, ct);
        
        return Results.Ok(result);
    }
}
