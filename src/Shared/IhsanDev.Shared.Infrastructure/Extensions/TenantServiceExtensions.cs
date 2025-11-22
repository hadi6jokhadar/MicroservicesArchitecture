using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Tenant Service HTTP client
/// </summary>
public static class TenantServiceExtensions
{
    /// <summary>
    /// Registers the Tenant Service HTTP client for service-to-service communication.
    /// Used by background jobs and services that need to interact with tenant management.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="serviceName">The name of the calling service (e.g., "FileManagerService", "IdentityService")</param>
    /// <param name="isDevelopment">Whether the environment is development (for SSL validation)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddTenantServiceClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool isDevelopment = false)
    {
        services.AddHttpClient("TenantService", client =>
        {
            var baseUrl = configuration["Services:TenantService:BaseUrl"]
                ?? configuration["MultiTenancy:TenantServiceUrl"]
                ?? "https://localhost:5002";
            
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            
            var timeout = configuration.GetValue<int>("Services:TenantService:Timeout", 30);
            client.Timeout = TimeSpan.FromSeconds(timeout);
            
            // Add service authentication headers
            var serviceSecret = configuration["ServiceCommunication:SharedSecret"];
            if (!string.IsNullOrEmpty(serviceSecret))
            {
                client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
                client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (isDevelopment)
            {
                handler.ServerCertificateCustomValidationCallback = 
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            return handler;
        });

        return services;
    }

    /// <summary>
    /// Registers a typed Tenant Service HTTP client for service-to-service communication.
    /// Used by background jobs and services that need to interact with tenant management.
    /// </summary>
    /// <typeparam name="TInterface">The interface type for the tenant service client</typeparam>
    /// <typeparam name="TImplementation">The implementation type for the tenant service client</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="serviceName">The name of the calling service (e.g., "FileManagerService", "IdentityService")</param>
    /// <param name="isDevelopment">Whether the environment is development (for SSL validation)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddTenantServiceClient<TInterface, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool isDevelopment = false)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>(client =>
        {
            var baseUrl = configuration["Services:TenantService:BaseUrl"]
                ?? configuration["MultiTenancy:TenantServiceUrl"]
                ?? "https://localhost:5002";
            
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            
            var timeout = configuration.GetValue<int>("Services:TenantService:Timeout", 30);
            client.Timeout = TimeSpan.FromSeconds(timeout);
            
            // Add service authentication headers
            var serviceSecret = configuration["ServiceCommunication:SharedSecret"];
            if (!string.IsNullOrEmpty(serviceSecret))
            {
                client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
                client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (isDevelopment)
            {
                handler.ServerCertificateCustomValidationCallback = 
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            return handler;
        });

        return services;
    }
}
