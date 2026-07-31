using IhsanDev.Shared.Infrastructure.Persistence;
using Tenant.Domain.Entities;

namespace Tenant.Domain.Repositories;

/// <summary>
/// Repository interface for tenant operations
/// </summary>
public interface ITenantRepository : IRepository<TenantSettings>
{
    /// <summary>
    /// Get tenant settings by tenant ID
    /// </summary>
    Task<TenantSettings?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tenant settings by tenant ID, including archived tenants
    /// </summary>
    Task<TenantSettings?> GetByTenantIdIncludingArchivedAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tenant settings by user ID
    /// </summary>
    Task<TenantSettings?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active tenants with pagination, optionally filtered by a search term matched against TenantId/TenantName
    /// </summary>
    Task<(IEnumerable<TenantSettings> Items, int TotalCount)> GetAllActiveAsync(
        int pageNumber = 1,
        int pageSize = 10,
        bool? isArchived = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete tenant settings (soft delete through IsArchived)
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if tenant exists and is active
    /// </summary>
    Task<bool> IsActiveTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if tenant ID already exists
    /// </summary>
    Task<bool> TenantIdExistsAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user already has a tenant
    /// </summary>
    Task<bool> UserHasTenantAsync(int userId, CancellationToken cancellationToken = default);
}
