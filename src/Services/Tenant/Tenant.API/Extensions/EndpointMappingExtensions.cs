using Tenant.API.Handlers;
using Tenant.Application.Commands.Tenant;

namespace Tenant.API.Extensions;

public static class EndpointMappingExtensions
{
    /// <summary>
    /// Map all tenant-related API endpoints
    /// </summary>
    public static WebApplication MapTenantEndpoints(this WebApplication app)
    {
        // Public tenant configuration endpoint (for Identity Service to fetch config)
        // Allow both anonymous access and service authentication
        var publicGroup = app.MapGroup("/api/tenant")
            .WithTags("Tenant Configuration")
            .WithOpenApi();

        // This endpoint is used by other services to fetch tenant configuration
        // Accessible ONLY by services with service authentication (not by users or anonymous)
        publicGroup.MapGet("/config/{tenantId}", TenantApiHandlers.GetTenantConfigHandler)
            .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
            .WithName("GetTenantConfig")
            .WithSummary("Get tenant configuration (Service-to-Service only)")
            .WithDescription("Get tenant-specific configuration including settings data. This endpoint is restricted to authenticated internal services only.")
            .Produces<object>(200)
            .Produces(404)
            .Produces(401)
            .Produces(403);

        // Get all active tenants with configuration (Service/SuperAdmin only)
        publicGroup.MapGet("/config", TenantApiHandlers.GetAllActiveTenantsWithConfigHandler)
            .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
            .WithName("GetAllTenantsWithConfig")
            .WithSummary("Get all active tenants with configuration (Service-to-Service only)")
            .WithDescription("Get paginated list of all active tenants including configuration data. This endpoint is restricted to authenticated internal services and SuperAdmin only.")
            .Produces<object>(200)
            .Produces(401)
            .Produces(403);

        // Public tenant info endpoint (without sensitive data)
        publicGroup.MapGet("/{tenantId}", TenantApiHandlers.GetTenantByIdHandler)
            .WithName("GetTenantById")
            .WithSummary("Get tenant by ID")
            .WithDescription("Get tenant information (excludes sensitive configuration data)")
            .Produces<object>(200)
            .Produces(404);

        // Tenant management endpoints (Admin only)
        var adminGroup = app.MapGroup("/api/admin/tenant")
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"))
            .WithTags("Tenant Management (Super Admin)")
            .WithOpenApi();

        adminGroup.MapGet("/", TenantApiHandlers.GetAllActiveTenantsHandler)
            .WithName("GetAllActiveTenants")
            .WithSummary("Get all active tenants")
            .WithDescription("Get paginated list of all active tenants (SuperAdmin only)")
            .Produces<object>(200);

        adminGroup.MapGet("/user/{userId:int}", TenantApiHandlers.GetTenantByUserHandler)
            .WithName("GetTenantByUser")
            .WithSummary("Get tenant by user ID")
            .WithDescription("Get tenant associated with a specific user (SuperAdmin only)")
            .Produces<object>(200)
            .Produces(404);

        adminGroup.MapPost("/", TenantApiHandlers.CreateTenantHandler)
            .WithName("CreateTenant")
            .WithSummary("Create new tenant")
            .WithDescription("Create a new tenant configuration (Admin only)")
            .Produces<object>(201)
            .ProducesValidationProblem();

        adminGroup.MapPut("/{tenantId}", TenantApiHandlers.UpdateTenantHandler)
            .WithName("UpdateTenant")
            .WithSummary("Update tenant settings")
            .WithDescription("Update tenant configuration and settings (Admin only)")
            .Produces<object>(200)
            .ProducesValidationProblem();

        adminGroup.MapDelete("/{tenantId}", TenantApiHandlers.DeleteTenantHandler)
            .WithName("DeleteTenant")
            .WithSummary("Delete tenant")
            .WithDescription("Delete tenant configuration (Admin only)")
            .Produces<object>(200);

        return app;
    }
}
