using IhsanDev.Shared.Kernel.Entities;
using IhsanDev.Shared.Kernel.Enums;

namespace Identity.Domain.Repositories;

/// <summary>
/// Repository interface for device token operations
/// </summary>
public interface IDeviceTokenRepository
{
    /// <summary>
    /// Get a device token by ID
    /// </summary>
    Task<DeviceToken?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all device tokens for a specific user
    /// </summary>
    Task<List<DeviceToken>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get device token by exact token string
    /// </summary>
    Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get device tokens by user ID and platform
    /// </summary>
    Task<List<DeviceToken>> GetByUserIdAndPlatformAsync(int userId, Platform platform, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new device token
    /// </summary>
    Task<DeviceToken> AddAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing device token
    /// </summary>
    Task<DeviceToken> UpdateAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a device token
    /// </summary>
    Task<bool> DeleteAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all device tokens for a specific user
    /// </summary>
    Task DeleteByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a device token exists
    /// </summary>
    Task<bool> ExistsAsync(string token, CancellationToken cancellationToken = default);
}
