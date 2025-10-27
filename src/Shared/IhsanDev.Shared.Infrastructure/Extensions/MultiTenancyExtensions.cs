using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Services.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for multi-tenancy support
/// </summary>
public static class MultiTenancyExtensions
{
    /// <summary>
    /// Add multi-tenancy services to the service collection
    /// </summary>
    public static IServiceCollection AddMultiTenancy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);

        if (!multiTenancyEnabled)
        {
            // Register empty implementations if multi-tenancy is disabled
            services.AddScoped<ITenantContext, TenantContext>();
            return services;
        }

        // Register tenant context (scoped per request)
        services.AddScoped<ITenantContext, TenantContext>();

        // Register tenant configuration provider
        services.AddScoped<ITenantConfigurationProvider, TenantConfigurationProvider>();

        // Add memory cache for tenant configuration caching
        services.AddMemoryCache();

        // Configure HttpClient for Tenant Service API
        var tenantServiceUrl = configuration["MultiTenancy:TenantServiceUrl"]
            ?? throw new InvalidOperationException("MultiTenancy:TenantServiceUrl is not configured");

        services.AddHttpClient("TenantServiceClient", client =>
        {
            client.BaseAddress = new Uri(tenantServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    /// <summary>
    /// Add tenant middleware to the request pipeline
    /// Must be called before authentication middleware
    /// Only adds middleware if multi-tenancy is enabled
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);
        
        if (!multiTenancyEnabled)
        {
            // Skip tenant middleware if multi-tenancy is disabled
            return app;
        }
        
        return app.UseMiddleware<TenantMiddleware>();
    }
}
