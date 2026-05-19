using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace IhsanDev.Shared.Infrastructure.Services.Cache;

/// <summary>
/// Redis-based distributed cache service implementation
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _multiplexer;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IDistributedCache cache,
        ILogger<RedisCacheService> logger,
        IConnectionMultiplexer? multiplexer = null)
    {
        _cache = cache;
        _multiplexer = multiplexer;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var data = await _cache.GetStringAsync(key, cancellationToken);
            
            if (string.IsNullOrEmpty(data))
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }

            var result = JsonSerializer.Deserialize<T>(data);
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from Redis cache for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _cache.SetStringAsync(key, json, options, cancellationToken);
            
            _logger.LogDebug("Cached value for key: {Key} with expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Redis cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _logger.LogDebug("Removed cached value for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from Redis cache for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _cache.GetStringAsync(key, cancellationToken);
            return !string.IsNullOrEmpty(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence in Redis cache for key: {Key}", key);
            return false;
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (_multiplexer == null)
        {
            _logger.LogWarning("RemoveByPatternAsync: IConnectionMultiplexer is not registered. Pattern: {Pattern}", pattern);
            return;
        }

        try
        {
            // Convert glob pattern (e.g. "categories:tree*") to Redis SCAN pattern
            // IDistributedCache uses an instance name prefix — read it from the first endpoint
            var server = _multiplexer.GetServers().FirstOrDefault(s => s.IsConnected);
            if (server == null)
            {
                _logger.LogWarning("RemoveByPatternAsync: No connected Redis server found for pattern: {Pattern}", pattern);
                return;
            }

            var keys = server.Keys(pattern: $"*{pattern}").ToArray();
            if (keys.Length == 0)
            {
                _logger.LogDebug("RemoveByPatternAsync: No keys found for pattern: {Pattern}", pattern);
                return;
            }

            var db = _multiplexer.GetDatabase();
            await db.KeyDeleteAsync(keys);
            _logger.LogDebug("RemoveByPatternAsync: Deleted {Count} keys for pattern: {Pattern}", keys.Length, pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing keys by pattern: {Pattern}", pattern);
        }
    }
}
