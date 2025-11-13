using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Middleware that validates CORS origins based on tenant-specific configuration
/// Must run after TenantResolutionMiddleware and before CORS middleware
/// </summary>
public class TenantAwareCorsMiddleware
{
    private readonly RequestDelegate _next;

    public TenantAwareCorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IConfiguration configuration)
    {
        // Get the Origin header from the request
        var origin = context.Request.Headers["Origin"].FirstOrDefault();

        if (!string.IsNullOrEmpty(origin))
        {
            // Get allowed origins based on tenant context
            var allowedOrigins = GetAllowedOrigins(configuration, tenantContext);

            // Validate origin
            if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                // Set CORS headers for all valid origins (both OPTIONS and actual requests)
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                
                // Handle preflight requests (OPTIONS)
                if (context.Request.Method == "OPTIONS")
                {
                    context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, PATCH, OPTIONS";
                    
                    // Get requested headers from preflight request or use common defaults
                    var requestedHeaders = context.Request.Headers["Access-Control-Request-Headers"].ToString();
                    if (!string.IsNullOrEmpty(requestedHeaders))
                    {
                        context.Response.Headers["Access-Control-Allow-Headers"] = requestedHeaders;
                    }
                    else
                    {
                        // Default common headers
                        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With, x-tenant-id";
                    }
                    
                    context.Response.Headers["Access-Control-Max-Age"] = "86400";
                    
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }
            }
            else
            {
                // Invalid origin - CORS will block this request
                // For preflight requests, return 403 Forbidden
                if (context.Request.Method == "OPTIONS")
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("CORS policy: Origin not allowed");
                    return;
                }
                // For actual requests, let them through but don't set CORS headers
                // The browser will block the response
            }
        }

        await _next(context);
    }

    private static string[] GetAllowedOrigins(IConfiguration configuration, ITenantContext tenantContext)
    {
        // Always get appsettings CORS configuration as base
        var appSettingsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        
        // If multi-tenancy is enabled and tenant has CORS config, merge with tenant-specific origins
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled");
        
        if (multiTenancyEnabled && 
            tenantContext.HasTenant && 
            tenantContext.CurrentTenant?.Configuration?.Cors?.AllowedOrigins?.Length > 0)
        {
            // Merge appsettings origins with tenant-specific origins (tenant origins take precedence)
            var tenantOrigins = tenantContext.CurrentTenant.Configuration.Cors.AllowedOrigins;
            return appSettingsOrigins.Union(tenantOrigins).ToArray();
        }

        // Fallback to appsettings CORS configuration
        return appSettingsOrigins;
    }
}
