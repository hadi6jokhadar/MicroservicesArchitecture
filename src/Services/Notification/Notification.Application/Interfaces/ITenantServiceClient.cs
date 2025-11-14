using Notification.Application.DTOs;

namespace Notification.Application.Interfaces;

/// <summary>
/// Client interface for communicating with Tenant Service
/// </summary>
public interface ITenantServiceClient
{
    /// <summary>
    /// Get all active tenant IDs
    /// </summary>
    Task<List<string>> GetAllActiveTenantIdsAsync(CancellationToken cancellationToken = default);
}
