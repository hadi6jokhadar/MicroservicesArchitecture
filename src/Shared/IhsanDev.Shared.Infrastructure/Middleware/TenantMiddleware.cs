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

        // Extract tenant ID from header
        var tenantId = context.Request.Headers["x-tenant-id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogDebug("No tenant ID found in request headers");
            await _next(context);
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
