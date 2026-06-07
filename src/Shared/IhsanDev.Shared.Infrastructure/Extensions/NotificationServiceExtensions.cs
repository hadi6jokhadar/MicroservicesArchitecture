using IhsanDev.Shared.Infrastructure.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Notification Service HTTP client
/// </summary>
public static class NotificationServiceExtensions
{
    /// <summary>
    /// Registers the Notification Service HTTP client for service-to-service communication.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="serviceName">The name of the calling service (e.g., "IdentityService", "FileManagerService")</param>
    /// <param name="isDevelopment">Whether the environment is development (for SSL validation)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddNotificationServiceClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool isDevelopment = false)
    {
        services.AddTransient<CorrelationIdForwardingHandler>();

        services.AddHttpClient("NotificationService", client =>
        {
            var baseUrl = configuration["Services:NotificationService:BaseUrl"]
                ?? configuration["NotificationService:BaseUrl"]
                ?? "https://localhost:5104";
            
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            
            var timeout = configuration.GetValue<int>("Services:NotificationService:Timeout", 30);
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
        })
        .AddHttpMessageHandler<CorrelationIdForwardingHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.Retry.BackoffType = DelayBackoffType.Exponential;

            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
        });

        return services;
    }
}
