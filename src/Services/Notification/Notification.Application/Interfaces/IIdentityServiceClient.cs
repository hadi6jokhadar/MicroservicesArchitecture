using Notification.Application.DTOs;

namespace Notification.Application.Interfaces;

/// <summary>
/// Client for communicating with Identity Service
/// </summary>
public interface IIdentityServiceClient
{
    /// <summary>
    /// Get all device tokens for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of device tokens</returns>
    Task<List<DeviceTokenDto>> GetUserDeviceTokensAsync(
        int userId, 
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an invalid device token
    /// </summary>
    /// <param name="tokenId">Device token ID</param>
    /// <param name="tenantId">Tenant ID (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteDeviceTokenAsync(
        int tokenId, 
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get device tokens for multiple users in a single batch request
    /// </summary>
    /// <param name="userIds">List of user IDs</param>
    /// <param name="tenantId">Tenant ID (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of user IDs to their device tokens</returns>
    Task<Dictionary<int, List<DeviceTokenDto>>> GetBatchDeviceTokensAsync(
        List<int> userIds,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple device tokens in a single batch request
    /// </summary>
    /// <param name="tokenIds">List of device token IDs</param>
    /// <param name="tenantId">Tenant ID (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of successfully deleted tokens</returns>
    Task<int> DeleteBatchDeviceTokensAsync(
        List<int> tokenIds,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all device tokens for a specific tenant (for tenant-wide notifications)
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of device tokens for the tenant</returns>
    Task<List<DeviceTokenDto>> GetTenantDeviceTokensAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
