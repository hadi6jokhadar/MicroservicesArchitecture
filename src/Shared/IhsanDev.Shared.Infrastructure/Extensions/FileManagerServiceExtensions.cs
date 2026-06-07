using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering FileManager service client
/// </summary>
public static class FileManagerServiceExtensions
{
    /// <summary>
    /// Registers the FileManager service client for fast service-to-service communication.
    /// Uses the internal endpoint that bypasses rate limiting and most middleware.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="serviceName">The name of the calling service (e.g., "IdentityService", "NotificationService")</param>
    /// <param name="isDevelopment">Whether the environment is development (for SSL validation)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFileManagerServiceClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool isDevelopment = false)
    {
        services.AddTransient<CorrelationIdForwardingHandler>();

        services.AddHttpClient<IFileManagerServiceClient, FileManagerServiceClient>(client =>
        {
            var baseUrl = configuration["Services:FileManagerService:BaseUrl"]
                ?? configuration["FileManagerService:BaseUrl"]
                ?? "https://localhost:5005";
            
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue<int>("Services:FileManagerService:Timeout", 5));

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
            // FileManager is a fast internal call — keep retries tight
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(100);
            options.Retry.BackoffType = DelayBackoffType.Exponential;

            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
