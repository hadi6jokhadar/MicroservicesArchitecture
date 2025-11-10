using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Services.Tenant;
using IhsanDev.Shared.Infrastructure.Services.Database;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
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

        // Register database migration service
        services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();

        // Add cache service (Redis if enabled, otherwise memory cache)
        services.AddCacheService(configuration);

        // Configure HttpClient for Tenant Service API with service authentication
        var tenantServiceUrl = configuration["MultiTenancy:TenantServiceUrl"]
            ?? throw new InvalidOperationException("MultiTenancy:TenantServiceUrl is not configured");

        services.AddHttpClient("TenantServiceClient", client =>
        {
            client.BaseAddress = new Uri(tenantServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            
            // Add service authentication headers for service-to-service communication
            var serviceSecret = configuration["ServiceCommunication:SharedSecret"];
            if (!string.IsNullOrEmpty(serviceSecret))
            {
                client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
                
                // Determine service name from current service configuration or default
                var serviceName = configuration["ServiceCommunication:ServiceName"] 
                    ?? configuration["ApplicationName"]
                    ?? "UnknownService";
                client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
            }
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

    /// <summary>
    /// Add database migration middleware for automatic tenant database creation
    /// When multi-tenancy is enabled, x-tenant-id header is REQUIRED
    /// This middleware only handles tenant-specific databases
    /// This must be called AFTER UseTenantResolution() and BEFORE UseAuthentication()
    /// Only adds middleware if multi-tenancy is enabled
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to migrate</typeparam>
    public static IApplicationBuilder UseTenantDatabaseMigration<TContext>(
        this IApplicationBuilder app,
        IConfiguration configuration)
        where TContext : DbContext
    {
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);
        
        if (!multiTenancyEnabled)
        {
            // Skip database migration middleware if multi-tenancy is disabled
            return app;
        }
        
        return app.UseMiddleware<DatabaseMigrationMiddleware<TContext>>();
    }

    /// <summary>
    /// Add tenant-aware CORS middleware to the request pipeline
    /// This middleware validates CORS origins based on tenant-specific configuration
    /// Must be called AFTER UseTenantResolution() and BEFORE standard UseCors()
    /// Automatically uses tenant-specific CORS origins when multi-tenancy is enabled,
    /// otherwise falls back to appsettings.json configuration
    /// </summary>
    public static IApplicationBuilder UseTenantAwareCors(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantAwareCorsMiddleware>();
    }
}
