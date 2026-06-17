using System.Net.Http.Json;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FileManager.Infrastructure.Services;

/// <summary>
/// Client for communicating with Tenant Service
/// </summary>
public class TenantServiceClient : ITenantServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantServiceClient> _logger;

    public TenantServiceClient(
        HttpClient httpClient,
        ILogger<TenantServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _logger.LogInformation(
            "TenantServiceClient initialized with base URL: {BaseUrl}",
            _httpClient.BaseAddress);
    }

    public async Task<List<TenantConfigDto>> GetAllTenantsWithConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allTenants = new List<TenantConfigDto>();
            int pageNumber = 1;
            const int pageSize = 100;
            bool hasMorePages = true;

            while (hasMorePages)
            {
                var response = await _httpClient.GetAsync(
                    $"/api/v1/tenant/config?pageNumber={pageNumber}&pageSize={pageSize}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to fetch tenants from Tenant service (page {PageNumber}). Status: {StatusCode}",
                        pageNumber,
                        response.StatusCode);
                    break;
                }

                var paginatedResult = await response.Content
                    .ReadFromJsonAsync<IhsanDev.Shared.Application.Common.Models.PaginatedList<TenantConfigDto>>(cancellationToken);

                if (paginatedResult?.Items != null && paginatedResult.Items.Any())
                {
                    allTenants.AddRange(paginatedResult.Items);
                    hasMorePages = pageNumber < paginatedResult.TotalPages;
                    pageNumber++;
                }
                else
                {
                    hasMorePages = false;
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} tenants with configuration from Tenant service",
                allTenants.Count);

            return allTenants;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching tenants from Tenant service");
            return new List<TenantConfigDto>();
        }
    }
}
