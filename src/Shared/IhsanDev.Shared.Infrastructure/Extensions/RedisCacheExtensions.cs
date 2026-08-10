using IhsanDev.Shared.Infrastructure.Services.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;

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

    /// <summary>
    /// Gives the cache's underlying Redis connections a head start on connecting, instead of
    /// leaving it entirely to the first real request. Call this once, right after
    /// <c>app.Build()</c>, as fire-and-forget (<c>_ = app.Services.WarmUpCacheAsync();</c>) — do
    /// NOT <c>await</c> it before <c>app.Run()</c>. A cold Redis connection can take several
    /// seconds to finish its handshake (observed 11s+ under load), and blocking startup on that is
    /// trading one race for another — the real fix is every Redis consumer tolerating "not
    /// connected yet" on its own (see <see cref="Services.Cache.RedisCacheService"/>'s try/catch,
    /// and the retry-with-backoff loops in the tenant-provisioning/config-updated listener
    /// services), not winning a timing race at startup. This method is purely a best-effort
    /// optimization on top of that — it has an internal catch-all specifically so it is safe to
    /// discard without observing the result; it must never throw regardless of what changes here.
    /// </summary>
    /// <param name="services">The root service provider (app.Services)</param>
    public static async Task WarmUpCacheAsync(this IServiceProvider services)
    {
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("RedisCacheExtensions");

        try
        {
            var cacheService = services.GetService<ICacheService>();
            if (cacheService == null)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            await cacheService.GetAsync<object>("__cache_warmup__");

            // Also resolve the pattern-removal multiplexer so its connection is established now too.
            services.GetService<IConnectionMultiplexer>();

            stopwatch.Stop();
            logger?.LogInformation("Cache warm-up completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Must never throw — this is fire-and-forget from the caller's perspective (see summary).
            logger?.LogWarning(ex, "Cache warm-up failed — proceeding without it, the cache will connect lazily on first use");
        }
    }
}
