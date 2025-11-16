using IhsanDev.Shared.Infrastructure.Attributes;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Middleware that extracts tenant ID from request header and resolves tenant configuration
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantConfigurationProvider tenantConfigProvider)
    {
        // Check if multi-tenancy is enabled
        if (!tenantContext.IsMultiTenantMode)
        {
            _logger.LogDebug("Multi-tenancy is disabled, skipping tenant resolution");
            await _next(context);
            return;
        }

        // Skip tenant resolution for static files (images, videos, documents, etc.)
        var path = context.Request.Path.Value ?? "";
        var isStaticFile = path.Contains(".") && !path.StartsWith("/api/");
        
        if (isStaticFile)
        {
            _logger.LogDebug("Skipping tenant resolution for static file: {Path}", path);
            await _next(context);
            return;
        }

        // Check if endpoint has metadata to bypass tenant resolution
        var endpoint = context.GetEndpoint();
        var bypassTenant = endpoint?.Metadata.GetMetadata<BypassTenantAttribute>() != null;
        var optionalTenant = endpoint?.Metadata.GetMetadata<OptionalTenantAttribute>() != null;
        
        if (bypassTenant)
        {
            _logger.LogDebug("Bypassing tenant resolution for endpoint with BypassTenant attribute");
            await _next(context);
            return;
        }

        // Extract tenant ID from header
        var tenantId = context.Request.Headers["x-tenant-id"].FirstOrDefault();

        // When multi-tenancy is enabled, x-tenant-id header is REQUIRED (unless OptionalTenant)
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            if (optionalTenant)
            {
                // Tenant is optional - continue without tenant context
                _logger.LogDebug("Tenant ID not provided but endpoint allows optional tenant. Continuing without tenant context.");
                await _next(context);
                return;
            }
            
            _logger.LogWarning("Multi-tenancy is enabled but x-tenant-id header is missing");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Missing required header",
                message = "Multi-tenancy is enabled. The 'x-tenant-id' header is required for all requests.",
                details = "Please provide a valid tenant ID in the 'x-tenant-id' header."
            });
            return;
        }

        _logger.LogDebug("Resolving tenant configuration for tenant ID: {TenantId}", tenantId);

        try
        {
            // Fetch tenant configuration
            var tenantInfo = await tenantConfigProvider.GetTenantConfigurationAsync(
                tenantId,
                context.RequestAborted);

            if (tenantInfo == null)
            {
                _logger.LogWarning("Tenant '{TenantId}' not found or inactive", tenantId);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Tenant not found or inactive",
                    tenantId
                });
                return;
            }

            if (!tenantInfo.IsActive)
            {
                _logger.LogWarning("Tenant '{TenantId}' is not active", tenantId);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Tenant is not active",
                    tenantId
                });
                return;
            }

            // Set tenant context for the request
            tenantContext.SetTenant(tenantInfo);
            _logger.LogInformation("Tenant context set for tenant: {TenantId} ({TenantName})",
                tenantInfo.TenantId, tenantInfo.TenantName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving tenant configuration for '{TenantId}'", tenantId);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Error resolving tenant configuration"
            });
            return;
        }

        await _next(context);
    }
}
