using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Services.Logging;
using IhsanDev.Shared.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering logging services
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Registers the custom logger manager service
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration to read log settings</param>
    /// <param name="serviceName">Optional service name for the current microservice</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCustomLogging(
        this IServiceCollection services,
        IConfiguration configuration,
        string? serviceName = null)
    {
        // Register HttpContextAccessor (required by TraceIdProvider)
        services.AddHttpContextAccessor();

        // Register the logger manager as singleton — read FilePath lazily so test
        // factories that override Logging:FilePath via ConfigureAppConfiguration are honoured.
        services.AddSingleton<ILoggerManager>(serviceProvider =>
        {
            var conf = serviceProvider.GetRequiredService<IConfiguration>();
            var logsPath = conf["Logging:FilePath"] ?? "Logs";
            var serviceLogsPath = !string.IsNullOrWhiteSpace(serviceName)
                ? Path.Combine(logsPath, serviceName)
                : logsPath;
            var logger = serviceProvider.GetRequiredService<ILogger<LoggerManager>>();
            return new LoggerManager(logger, serviceLogsPath);
        });

        // Register TraceId provider as scoped (per request)
        services.AddScoped<ITraceIdProvider, TraceIdProvider>();

        return services;
    }

    /// <summary>
    /// Registers the custom logger manager service with explicit log path
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="logFilePath">Explicit path for log files</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCustomLogging(
        this IServiceCollection services,
        string logFilePath)
    {
        services.AddSingleton<ILoggerManager>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<LoggerManager>>();
            return new LoggerManager(logger, logFilePath);
        });

        // Register TraceId provider as scoped (per request)
        services.AddScoped<ITraceIdProvider, TraceIdProvider>();

        return services;
    }
}