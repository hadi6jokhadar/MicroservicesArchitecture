using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Identity Service HTTP client
/// </summary>
public static class IdentityServiceExtensions
{
    /// <summary>
    /// Registers the Identity Service HTTP client for service-to-service communication.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="serviceName">The name of the calling service (e.g., "NotificationService", "FileManagerService")</param>
    /// <param name="isDevelopment">Whether the environment is development (for SSL validation)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddIdentityServiceClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool isDevelopment = false)
    {
        services.AddHttpClient("IdentityService", client =>
        {
            var baseUrl = configuration["Services:IdentityService:BaseUrl"]
                ?? configuration["IdentityService:BaseUrl"]
                ?? "https://localhost:5001";
            
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            
            var timeout = configuration.GetValue<int>("Services:IdentityService:Timeout", 30);
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
    /// Registers a typed Identity Service HTTP client for service-to-service communication.
    /// </summary>
    /// <typeparam name="TInterface">The interface type for the identity service client</typeparam>
    /// <typeparam name="TImplementation">The implementation type for the identity service client</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="serviceName">The name of the calling service (e.g., "NotificationService", "FileManagerService")</param>
    /// <param name="isDevelopment">Whether the environment is development (for SSL validation)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddIdentityServiceClient<TInterface, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool isDevelopment = false)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>(client =>
        {
            var baseUrl = configuration["Services:IdentityService:BaseUrl"]
                ?? configuration["IdentityService:BaseUrl"]
                ?? "https://localhost:5001";
            
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            
            var timeout = configuration.GetValue<int>("Services:IdentityService:Timeout", 30);
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
