using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notification.Application.Interfaces;

namespace Notification.Infrastructure.Services;

/// <summary>
/// Client for communicating with Tenant Service
/// </summary>
public class TenantServiceClient : ITenantServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantServiceClient> _logger;
    private readonly string _sharedSecret;

    public TenantServiceClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TenantServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration.GetValue<string>("MultiTenancy:TenantServiceUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("MultiTenancy:TenantServiceUrl is not configured");
        }

        _httpClient.BaseAddress = new Uri(baseUrl);

        // Get shared secret for service-to-service authentication
        _sharedSecret = configuration.GetValue<string>("ServiceCommunication:SharedSecret") ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_sharedSecret))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Service-Secret", _sharedSecret);
        }

        _logger.LogInformation(
            "TenantServiceClient initialized with base URL: {BaseUrl}, Authentication: {HasAuth}",
            baseUrl,
            !string.IsNullOrWhiteSpace(_sharedSecret));
    }

    public async Task<List<string>> GetAllActiveTenantIdsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all active tenants (paginated)
            var allTenantIds = new List<string>();
            int pageNumber = 1;
            const int pageSize = 100;
            bool hasMorePages = true;

            while (hasMorePages)
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"/api/admin/tenant?pageNumber={pageNumber}&pageSize={pageSize}");

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Failed to get active tenants (page {PageNumber}). Status: {StatusCode}",
                        pageNumber,
                        response.StatusCode);
                    break;
                }

                var result = await response.Content.ReadFromJsonAsync<PaginatedTenantResult>(
                    cancellationToken: cancellationToken);

                if (result?.Items != null && result.Items.Any())
                {
                    allTenantIds.AddRange(result.Items.Select(t => t.TenantId));
                    
                    // Check if there are more pages
                    hasMorePages = pageNumber < result.TotalPages;
                    pageNumber++;
                }
                else
                {
                    hasMorePages = false;
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} active tenant IDs",
                allTenantIds.Count);

            return allTenantIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting all active tenant IDs");

            return new List<string>();
        }
    }

    private record PaginatedTenantResult(
        List<TenantItem> Items,
        int PageNumber,
        int TotalPages,
        int TotalCount,
        bool HasPreviousPage,
        bool HasNextPage);

    private record TenantItem(
        int Id,
        string TenantId,
        string Name,
        bool IsActive);
}
