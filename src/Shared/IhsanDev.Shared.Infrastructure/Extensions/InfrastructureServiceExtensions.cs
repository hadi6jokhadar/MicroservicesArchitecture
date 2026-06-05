using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Application.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace IhsanDev.Shared.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers global exception handler and problem details
    /// </summary>
    public static IServiceCollection AddGlobalExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    /// <summary>
    /// Configures exception handling middleware
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        return app;
    }

    /// <summary>
    /// Reads X-Correlation-Id from inbound requests (or generates one), stores it in
    /// HttpContext.Items, echoes it back in the response header, and enriches the
    /// structured log scope so every log line within the request includes CorrelationId.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    /// <summary>
    /// Registers localization service with proper resource path
    /// </summary>
    public static IServiceCollection AddLocalizationService(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ILocalizationService>(provider =>
        {
            var cache = provider.GetRequiredService<IMemoryCache>();
            var logger = provider.GetRequiredService<ILogger<LocalizationService>>();
            var resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Localization");
            
            return new LocalizationService(cache, logger, resourcesPath);
        });

        return services;
    }
}