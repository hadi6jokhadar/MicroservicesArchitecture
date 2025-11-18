using IhsanDev.Shared.Infrastructure.Attributes;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Application.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Middleware that verifies the tenant_id claim in JWT matches the x-tenant-id header
/// Prevents users from accessing resources in other tenants by changing the header
/// </summary>
public class JwtTenantVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtTenantVerificationMiddleware> _logger;

    public JwtTenantVerificationMiddleware(RequestDelegate next, ILogger<JwtTenantVerificationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ILocalizationService localizationService)
    {
        // Skip verification if:
        // 1. Multi-tenancy is disabled
        // 2. User is not authenticated
        // 3. Endpoint has BypassTenant or OptionalTenant attributes
        // 4. Static files
        
        if (!tenantContext.IsMultiTenantMode)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        var isStaticFile = path.Contains(".") && !path.StartsWith("/api/");
        
        if (isStaticFile)
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        var bypassTenant = endpoint?.Metadata.GetMetadata<BypassTenantAttribute>() != null;
        var optionalTenant = endpoint?.Metadata.GetMetadata<OptionalTenantAttribute>() != null;
        
        if (bypassTenant || optionalTenant)
        {
            await _next(context);
            return;
        }

        // Only verify if user is authenticated
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Get tenant_id claim from JWT
        var jwtTenantIdClaim = context.User?.FindFirst("tenant_id")?.Value;
        
        // Get x-tenant-id from header
        var headerTenantId = context.Request.Headers["x-tenant-id"].FirstOrDefault();

        // If JWT has tenant_id claim, it MUST match the header
        if (!string.IsNullOrWhiteSpace(jwtTenantIdClaim))
        {
            if (string.IsNullOrWhiteSpace(headerTenantId))
            {
                _logger.LogWarning(
                    "JWT contains tenant_id claim '{JwtTenantId}' but x-tenant-id header is missing. User: {UserId}",
                    jwtTenantIdClaim,
                    context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");
                
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = localizationService.GetString(LocalizationKeys.Tenant.MissingHeader),
                    message = "Your authentication token is associated with a tenant, but no tenant header was provided."
                });
                return;
            }

            if (!jwtTenantIdClaim.Equals(headerTenantId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Tenant mismatch: JWT tenant_id '{JwtTenantId}' does not match x-tenant-id header '{HeaderTenantId}'. User: {UserId}",
                    jwtTenantIdClaim,
                    headerTenantId,
                    context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");
                
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = localizationService.GetString(LocalizationKeys.Exceptions.Forbidden),
                    message = $"Access denied. Your authentication token is for tenant '{jwtTenantIdClaim}', but you are trying to access tenant '{headerTenantId}'."
                });
                return;
            }

            _logger.LogDebug(
                "JWT tenant verification passed. Tenant: {TenantId}, User: {UserId}",
                jwtTenantIdClaim,
                context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");
        }
        else
        {
            // JWT has no tenant_id claim = Global user (SuperAdmin or Service)
            // They can access any tenant via x-tenant-id header
            var userRole = context.User?.FindFirst(ClaimTypes.Role)?.Value;
            
            if (!string.IsNullOrWhiteSpace(headerTenantId))
            {
                _logger.LogDebug(
                    "Global user (Role: {Role}) accessing tenant '{TenantId}'. User: {UserId}",
                    userRole ?? "Unknown",
                    headerTenantId,
                    context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");
            }
        }

        await _next(context);
    }
}
