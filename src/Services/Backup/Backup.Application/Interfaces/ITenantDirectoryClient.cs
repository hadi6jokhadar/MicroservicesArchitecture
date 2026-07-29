namespace Backup.Application.Interfaces;

/// <summary>
/// Service-to-service client for fetching active tenants and their database connection strings
/// from the Tenant Service. Implemented in Infrastructure via the shared
/// <c>AddTenantServiceClient</c> typed-HttpClient extension.
/// </summary>
public interface ITenantDirectoryClient
{
    /// <summary>
    /// Returns every active tenant known to the Tenant Service, paginating internally as needed.
    /// </summary>
    Task<List<ActiveTenantSummary>> GetActiveTenantsAsync(CancellationToken ct);
}

/// <summary>
/// A trimmed-down projection of a tenant's configuration — just enough for backup targeting.
/// <see cref="ConnectionString"/> is null when the tenant has no dedicated database connection
/// string configured (falls back to a shared default DB it doesn't own) — such tenants are out
/// of scope for a dedicated tenant backup.
/// </summary>
public record ActiveTenantSummary(string TenantId, string TenantName, bool IsActive, string? ConnectionString);
