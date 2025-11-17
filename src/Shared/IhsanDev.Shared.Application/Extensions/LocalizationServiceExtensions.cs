using IhsanDev.Shared.Application.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace IhsanDev.Shared.Application.Extensions;

/// <summary>
/// Extension methods for registering localization services
/// </summary>
public static class LocalizationServiceExtensions
{
    /// <summary>
    /// Add localization services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="resourcesPath">Optional custom path to localization resources. If null, uses default path in application directory</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLocalization(
        this IServiceCollection services,
        string? resourcesPath = null)
    {
        // Add memory cache (required by LocalizationService)
        services.AddMemoryCache();

        // Register localization service
        services.AddSingleton<ILocalizationService>(provider =>
        {
            var cache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LocalizationService>>();
            return new LocalizationService(cache, logger, resourcesPath);
        });

        return services;
    }
}
