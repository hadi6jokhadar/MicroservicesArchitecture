using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Notification.Application.DTOs;
using Notification.Application.Interfaces;

namespace Notification.Infrastructure.Services;

/// <summary>
/// Client for communicating with Identity Service
/// </summary>
public class IdentityServiceClient : IIdentityServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IdentityServiceClient> _logger;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public IdentityServiceClient(
        HttpClient httpClient,
        ILogger<IdentityServiceClient> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;

        _logger.LogInformation(
            "IdentityServiceClient initialized with base URL: {BaseUrl}",
            _httpClient.BaseAddress);
    }

    public async Task<List<DeviceTokenDto>> GetUserDeviceTokensAsync(
        int userId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        var cacheKey = $"device_tokens_{userId}_{tenantId ?? "none"}";
        if (_cache.TryGetValue<List<DeviceTokenDto>>(cacheKey, out var cachedTokens))
        {
            _logger.LogDebug(
                "Retrieved {Count} device tokens from cache for user {UserId} in tenant {TenantId}",
                cachedTokens?.Count ?? 0,
                userId,
                tenantId ?? "none");
            return cachedTokens ?? new List<DeviceTokenDto>();
        }

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/device-tokens/user/{userId}");

            // Add tenant header if provided
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                request.Headers.Add("x-tenant-id", tenantId);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to get device tokens for user {UserId} in tenant {TenantId}. Status: {StatusCode}",
                    userId,
                    tenantId ?? "none",
                    response.StatusCode);

                return new List<DeviceTokenDto>();
            }

            var tokens = await response.Content.ReadFromJsonAsync<List<DeviceTokenDto>>(
                cancellationToken: cancellationToken);

            var result = tokens ?? new List<DeviceTokenDto>();

            // Cache the result
            _cache.Set(cacheKey, result, _cacheExpiration);

            _logger.LogInformation(
                "Retrieved {Count} device tokens for user {UserId} in tenant {TenantId}",
                result.Count,
                userId,
                tenantId ?? "none");

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error getting device tokens for user {UserId} in tenant {TenantId}",
                userId,
                tenantId ?? "none");

            return new List<DeviceTokenDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting device tokens for user {UserId} in tenant {TenantId}",
                userId,
                tenantId ?? "none");

            return new List<DeviceTokenDto>();
        }
    }

    public async Task<bool> DeleteDeviceTokenAsync(
        int tokenId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/v1/device-tokens/{tokenId}");

            // Add tenant header if provided
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                request.Headers.Add("x-tenant-id", tenantId);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Deleted invalid device token {TokenId} in tenant {TenantId}",
                    tokenId,
                    tenantId ?? "none");

                return true;
            }

            _logger.LogWarning(
                "Failed to delete device token {TokenId} in tenant {TenantId}. Status: {StatusCode}",
                tokenId,
                tenantId ?? "none",
                response.StatusCode);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting device token {TokenId} in tenant {TenantId}",
                tokenId,
                tenantId ?? "none");

            return false;
        }
    }

    public async Task<Dictionary<int, List<DeviceTokenDto>>> GetBatchDeviceTokensAsync(
        List<int> userIds,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, List<DeviceTokenDto>>();

        if (!userIds.Any())
        {
            return result;
        }

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/device-tokens/batch")
            {
                Content = JsonContent.Create(new { userIds })
            };

            // Add tenant header if provided
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                request.Headers.Add("x-tenant-id", tenantId);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to get batch device tokens for {Count} users in tenant {TenantId}. Status: {StatusCode}",
                    userIds.Count,
                    tenantId ?? "none",
                    response.StatusCode);

                return result;
            }

            var batchResult = await response.Content.ReadFromJsonAsync<Dictionary<int, List<DeviceTokenDto>>>(
                cancellationToken: cancellationToken);

            result = batchResult ?? new Dictionary<int, List<DeviceTokenDto>>();

            // Cache each user's tokens
            foreach (var kvp in result)
            {
                var cacheKey = $"device_tokens_{kvp.Key}_{tenantId ?? "none"}";
                _cache.Set(cacheKey, kvp.Value, _cacheExpiration);
            }

            _logger.LogInformation(
                "Retrieved batch device tokens for {Count} users in tenant {TenantId}",
                result.Count,
                tenantId ?? "none");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting batch device tokens for {Count} users in tenant {TenantId}",
                userIds.Count,
                tenantId ?? "none");

            return result;
        }
    }

    public async Task<int> DeleteBatchDeviceTokensAsync(
        List<int> tokenIds,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (!tokenIds.Any())
        {
            return 0;
        }

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Delete,
                "/api/v1/device-tokens/batch")
            {
                Content = JsonContent.Create(new { tokenIds })
            };

            // Add tenant header if provided
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                request.Headers.Add("x-tenant-id", tenantId);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadFromJsonAsync<BatchDeleteResult>(
                    cancellationToken: cancellationToken);

                var deletedCount = responseContent?.DeletedCount ?? 0;

                _logger.LogInformation(
                    "Deleted {DeletedCount} of {TotalCount} device tokens in tenant {TenantId}",
                    deletedCount,
                    tokenIds.Count,
                    tenantId ?? "none");

                return deletedCount;
            }

            _logger.LogWarning(
                "Failed to delete batch device tokens in tenant {TenantId}. Status: {StatusCode}",
                tenantId ?? "none",
                response.StatusCode);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting batch device tokens in tenant {TenantId}",
                tenantId ?? "none");

            return 0;
        }
    }

    private record BatchDeleteResult(int DeletedCount);

    public async Task<List<DeviceTokenDto>> GetTenantDeviceTokensAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/v1/device-tokens/tenant");

            // Add tenant header
            request.Headers.Add("x-tenant-id", tenantId);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to get device tokens for tenant {TenantId}. Status: {StatusCode}",
                    tenantId,
                    response.StatusCode);

                return new List<DeviceTokenDto>();
            }

            var tokens = await response.Content.ReadFromJsonAsync<List<DeviceTokenDto>>(
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} device tokens for tenant {TenantId}",
                tokens?.Count ?? 0,
                tenantId);

            return tokens ?? new List<DeviceTokenDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting device tokens for tenant {TenantId}",
                tenantId);

            return new List<DeviceTokenDto>();
        }
    }
}
