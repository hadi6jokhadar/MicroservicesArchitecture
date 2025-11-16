using FileManager.Application.DTOs;

namespace FileManager.Application.Interfaces;

/// <summary>
/// Client interface for communicating with Tenant Service
/// </summary>
public interface ITenantServiceClient
{
    /// <summary>
    /// Get all tenants with their full configuration
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant configurations</returns>
    Task<List<TenantConfigDto>> GetAllTenantsWithConfigAsync(CancellationToken cancellationToken = default);
}
