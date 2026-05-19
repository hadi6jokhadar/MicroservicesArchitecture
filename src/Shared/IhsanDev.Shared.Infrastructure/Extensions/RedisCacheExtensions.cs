using IhsanDev.Shared.Infrastructure.Services.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring caching services
/// </summary>
public static class RedisCacheExtensions
{
    /// <summary>
    /// Adds Redis distributed cache service
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration["Redis:ConnectionString"];
        
        if (string.IsNullOrEmpty(redisConnection))
        {
            throw new InvalidOperationException(
                "Redis connection string not found in configuration. " +
                "Please add 'Redis:ConnectionString' to appsettings.json");
        }

        // Add IConnectionMultiplexer for pattern-based cache removal
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

        // Add Redis distributed cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = configuration["Redis:InstanceName"] ?? "MicroservicesApp:";
        });

        // Register ICacheService with Redis implementation
        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Adds in-memory cache service (fallback for when Redis is not available)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddInMemoryCache(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        return services;
    }

    /// <summary>
    /// Adds cache service based on configuration (Redis if enabled, otherwise in-memory)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddCacheService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useRedis = configuration.GetValue<bool>("Redis:Enabled", false);

        if (useRedis)
        {
            services.AddRedisCache(configuration);
        }
        else
        {
            services.AddInMemoryCache();
        }

        return services;
    }
}
