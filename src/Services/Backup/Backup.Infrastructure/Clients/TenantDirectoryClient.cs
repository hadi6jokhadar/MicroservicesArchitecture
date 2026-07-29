using System.Net.Http.Json;
using System.Text.Json;
using Backup.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Clients;

/// <summary>
/// Calls Tenant Service's <c>GET /api/v1/tenant/config</c> (Service/SuperAdmin only) to list
/// active tenants and their database connection strings. Registered via the shared
/// <c>AddTenantServiceClient&lt;TInterface, TImplementation&gt;</c> typed-HttpClient extension,
/// which already wires the base address, resilience handler, and
/// <c>X-Service-Secret</c>/<c>X-Service-Name</c> headers.
/// </summary>
public class TenantDirectoryClient : ITenantDirectoryClient
{
    private const int PageSize = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantDirectoryClient> _logger;

    public TenantDirectoryClient(HttpClient httpClient, ILogger<TenantDirectoryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ActiveTenantSummary>> GetActiveTenantsAsync(CancellationToken ct)
    {
        var results = new List<ActiveTenantSummary>();
        var pageNumber = 1;

        while (true)
        {
            TenantConfigPage? page;
            try
            {
                var response = await _httpClient.GetAsync(
                    $"/api/v1/tenant/config?pageNumber={pageNumber}&pageSize={PageSize}", ct);
                response.EnsureSuccessStatusCode();
                page = await response.Content.ReadFromJsonAsync<TenantConfigPage>(JsonOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch active tenants from Tenant Service (page {PageNumber})", pageNumber);
                break;
            }

            if (page?.Items == null || page.Items.Count == 0)
            {
                break;
            }

            foreach (var item in page.Items)
            {
                results.Add(new ActiveTenantSummary(
                    item.TenantId,
                    item.TenantName,
                    item.IsActive,
                    item.Data?.DatabaseSettings?.ConnectionString));
            }

            if (pageNumber >= page.TotalPages)
            {
                break;
            }

            pageNumber++;
        }

        return results;
    }

    // Trimmed-down local projection of Tenant Service's TenantConfigDto — deliberately not a
    // reference to Tenant.Application's DTOs, to avoid a cross-service compile-time dependency.
    private sealed class TenantConfigPage
    {
        public List<TenantConfigItem> Items { get; set; } = [];
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    private sealed class TenantConfigItem
    {
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public TenantConfigData? Data { get; set; }
    }

    private sealed class TenantConfigData
    {
        public TenantDatabaseSettings? DatabaseSettings { get; set; }
    }

    private sealed class TenantDatabaseSettings
    {
        public string? ConnectionString { get; set; }
    }
}
