using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace IhsanDev.Shared.Infrastructure.Services.Tenant;

/// <summary>
/// Provides tenant configuration by fetching from Tenant Service API with caching
/// </summary>
public class TenantConfigurationProvider : ITenantConfigurationProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantConfigurationProvider> _logger;
    private readonly TimeSpan _cacheExpiration;

    public TenantConfigurationProvider(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<TenantConfigurationProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TenantServiceClient");
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
        
        // Cache tenant config for 5 minutes by default
        _cacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue<int>("MultiTenancy:CacheExpirationMinutes", 5));
    }

    public async Task<TenantInfo?> GetTenantConfigurationAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        // Check if multi-tenancy is enabled
        if (!_configuration.GetValue<bool>("MultiTenancy:Enabled", false))
        {
            _logger.LogDebug("Multi-tenancy is disabled, returning null");
            return null;
        }

        // Check cache first
        var cacheKey = $"tenant_config_{tenantId}";
        if (_cache.TryGetValue<TenantInfo>(cacheKey, out var cachedTenant))
        {
            _logger.LogDebug("Tenant configuration for '{TenantId}' retrieved from cache", tenantId);
            return cachedTenant;
        }

        try
        {
            // Fetch from Tenant Service API
            var response = await _httpClient.GetAsync($"/api/tenant/config/{tenantId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch tenant configuration for '{TenantId}'. Status: {StatusCode}", 
                    tenantId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tenantConfig = JsonSerializer.Deserialize<TenantConfigResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tenantConfig == null)
            {
                _logger.LogWarning("Failed to deserialize tenant configuration for '{TenantId}'", tenantId);
                return null;
            }

            // Parse tenant configuration data
            var tenantInfo = ParseTenantInfo(tenantConfig);

            // Cache the result
            _cache.Set(cacheKey, tenantInfo, _cacheExpiration);
            _logger.LogInformation("Tenant configuration for '{TenantId}' fetched and cached", tenantId);

            return tenantInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenant configuration for '{TenantId}'", tenantId);
            return null;
        }
    }

    public void ClearCache(string tenantId)
    {
        var cacheKey = $"tenant_config_{tenantId}";
        _cache.Remove(cacheKey);
        _logger.LogInformation("Cache cleared for tenant '{TenantId}'", tenantId);
    }

    public void ClearAllCache()
    {
        // Note: IMemoryCache doesn't have a clear all method
        // In production, consider using distributed cache (Redis) with key patterns
        _logger.LogWarning("ClearAllCache called - MemoryCache doesn't support clearing all entries");
    }

    private TenantInfo ParseTenantInfo(TenantConfigResponse response)
    {
        var tenantInfo = new TenantInfo
        {
            TenantId = response.TenantId,
            TenantName = response.TenantName,
            UserId = response.UserId,
            IsActive = response.IsActive
        };

        // Parse the JSON data field containing configuration
        if (!string.IsNullOrEmpty(response.Data))
        {
            try
            {
                var config = JsonSerializer.Deserialize<TenantConfiguration>(response.Data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                tenantInfo.Configuration = config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse tenant configuration data for '{TenantId}'", response.TenantId);
            }
        }

        return tenantInfo;
    }

    // Response model matching the Tenant Service API
    private class TenantConfigResponse
    {
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public bool IsActive { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}
