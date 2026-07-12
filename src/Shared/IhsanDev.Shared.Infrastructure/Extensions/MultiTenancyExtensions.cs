using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Services.Tenant;
using IhsanDev.Shared.Infrastructure.Services.Database;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

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
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();

            // In development, bypass SSL certificate validation for self-signed certificates
            // This is necessary for local HTTPS development with self-signed certificates
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        })
        // This client is called from TenantConfigurationProvider, invoked by TenantMiddleware on
        // EVERY tenant-scoped request across every multi-tenant service whenever the 30-minute
        // Redis cache misses (cold start, cache flush, first request for a tenant). It must fail
        // fast, not just eventually succeed — previously this had no resilience handler at all
        // (just a flat 10s client.Timeout, no circuit breaker), so repeated failures never tripped
        // open and every miss paid the same ~10s penalty indefinitely.
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(100);
            options.Retry.BackoffType = DelayBackoffType.Exponential;

            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(4);
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
    /// Add JWT tenant verification middleware to the request pipeline
    /// Verifies that the tenant_id claim in JWT matches the x-tenant-id header
    /// Must be called AFTER UseTenantResolution() and BEFORE UseAuthentication()
    /// Only adds middleware if multi-tenancy is enabled
    /// </summary>
    public static IApplicationBuilder UseJwtTenantVerification(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);
        
        if (!multiTenancyEnabled)
        {
            // Skip JWT tenant verification if multi-tenancy is disabled
            return app;
        }
        
        return app.UseMiddleware<JwtTenantVerificationMiddleware>();
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
