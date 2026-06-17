using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Infrastructure.Services.Cache;

namespace IhsanDev.Shared.Infrastructure.Services.Tenant;

/// <summary>
/// Provides tenant configuration by fetching from Tenant Service API with distributed caching
/// </summary>
public class TenantConfigurationProvider : ITenantConfigurationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantConfigurationProvider> _logger;
    private readonly TimeSpan _cacheExpiration;

    public TenantConfigurationProvider(
        IHttpClientFactory httpClientFactory,
        ICacheService cache,
        IConfiguration configuration,
        ILogger<TenantConfigurationProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TenantServiceClient");
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
        
        // Cache tenant config for 30 minutes by default (increased from 5 for better performance)
        // Can be overridden in appsettings.json with MultiTenancy:CacheExpirationMinutes
        _cacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue<int>("MultiTenancy:CacheExpirationMinutes", 30));
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
        var cachedTenant = await _cache.GetAsync<TenantInfo>(cacheKey, cancellationToken);
        if (cachedTenant != null)
        {
            _logger.LogDebug("Tenant configuration for '{TenantId}' retrieved from cache", tenantId);
            return cachedTenant;
        }

        try
        {
            // Fetch from Tenant Service API
            var response = await _httpClient.GetAsync($"/api/v1/tenant/config/{tenantId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch tenant configuration for '{TenantId}'. Status: {StatusCode}", 
                    tenantId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tenantConfig = JsonSerializer.Deserialize<TenantConfigResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (tenantConfig == null)
            {
                _logger.LogWarning("Failed to deserialize tenant configuration for '{TenantId}'", tenantId);
                return null;
            }

            // Parse tenant configuration data
            var tenantInfo = ParseTenantInfo(tenantConfig);

            // Cache the result
            await _cache.SetAsync(cacheKey, tenantInfo, _cacheExpiration, cancellationToken);
            _logger.LogInformation("Tenant configuration for '{TenantId}' fetched and cached", tenantId);

            return tenantInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenant configuration for '{TenantId}'", tenantId);
            return null;
        }
    }

    public async void ClearCache(string tenantId)
    {
        var cacheKey = $"tenant_config_{tenantId}";
        await _cache.RemoveAsync(cacheKey);
        _logger.LogInformation("Cache cleared for tenant '{TenantId}'", tenantId);
    }

    public async void ClearAllCache()
    {
        await _cache.RemoveByPatternAsync("tenant_config_*");
        _logger.LogInformation("All tenant configuration cache cleared");
    }

    private TenantInfo ParseTenantInfo(TenantConfigResponse response)
    {
        var tenantInfo = new TenantInfo
        {
            TenantId = response.TenantId,
            TenantName = response.TenantName,
            UserId = response.UserId,
            IsActive = response.IsActive,
            Configuration = response.Data // Data is now TenantConfiguration object, not string
        };

        return tenantInfo;
    }

    // Response model matching the Tenant Service API
    private class TenantConfigResponse
    {
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public bool IsActive { get; set; }
        public TenantConfiguration? Data { get; set; } // Changed from string to TenantConfiguration
    }
}
