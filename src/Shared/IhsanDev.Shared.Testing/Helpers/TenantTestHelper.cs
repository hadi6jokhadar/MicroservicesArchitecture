using System.Net.Http.Json;

namespace IhsanDev.Shared.Testing.Helpers;

/// <summary>
/// Shared helper for tenant-related test operations
/// Can be reused across all services that need tenant functionality
/// </summary>
public static class TenantTestHelper
{
    private static int _userIdCounter = 2000; // Start from 2000 to avoid conflicts with service-specific counters
    
    /// <summary>
    /// Generate a unique user ID for testing (thread-safe)
    /// </summary>
    public static int GenerateUniqueUserId()
    {
        return Interlocked.Increment(ref _userIdCounter);
    }
    
    /// <summary>
    /// Generate a unique tenant ID for testing
    /// </summary>
    public static string GenerateUniqueTenantId(string prefix = "shared-tenant")
    {
        return $"{prefix}-{Guid.NewGuid().ToString()[..8]}";
    }
    
    /// <summary>
    /// Create a user and tenant via HTTP client (for cross-service testing)
    /// </summary>
    /// <param name="httpClient">HTTP client configured for the tenant service</param>
    /// <param name="tenantId">Optional tenant ID, generates unique if not provided</param>
    /// <param name="userId">Optional user ID, generates unique if not provided</param>
    /// <returns>Tuple of (UserId, TenantId, TenantResponseId)</returns>
    public static async Task<(int UserId, string TenantId, int TenantResponseId)> CreateUserAndTenantAsync(
        HttpClient httpClient,
        string? tenantId = null,
        int? userId = null)
    {
        var actualUserId = userId ?? GenerateUniqueUserId();
        var actualTenantId = tenantId ?? GenerateUniqueTenantId();
        
        var createTenantRequest = new
        {
            TenantId = actualTenantId,
            TenantName = $"Test Tenant {Guid.NewGuid().ToString()[..8]}",
            UserId = actualUserId,
            StartDate = DateTime.UtcNow,
            ExpireDate = DateTime.UtcNow.AddYears(1),
            Data = "{\"Jwt\":{\"Secret\":\"test-secret-key\",\"Issuer\":\"TestTenant\"}}"
        };
        
        var response = await httpClient.PostAsJsonAsync("/api/tenants", createTenantRequest);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        
        return (actualUserId, actualTenantId, result!.Id);
    }
    
    /// <summary>
    /// Get tenant by ID via HTTP client
    /// </summary>
    /// <param name="httpClient">HTTP client configured for the tenant service</param>
    /// <param name="tenantId">The tenant ID to retrieve</param>
    /// <returns>Tenant data or null if not found</returns>
    public static async Task<TenantResponse?> GetTenantByIdAsync(
        HttpClient httpClient,
        string tenantId)
    {
        var response = await httpClient.GetAsync($"/api/tenants/{tenantId}");
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        
        return await response.Content.ReadFromJsonAsync<TenantResponse>();
    }
    
    /// <summary>
    /// Check if a service has tenant support enabled
    /// </summary>
    /// <param name="httpClient">HTTP client configured for the service</param>
    /// <param name="healthEndpoint">Optional health/info endpoint to check, defaults to /health</param>
    /// <returns>True if tenant-enabled, false otherwise</returns>
    public static async Task<bool> IsTenantEnabledAsync(
        HttpClient httpClient,
        string healthEndpoint = "/health")
    {
        try
        {
            // Try to access tenant endpoint - if it exists, tenant is enabled
            var response = await httpClient.GetAsync("/api/tenants");
            // 200 OK, 401 Unauthorized, or 404 NotFound all indicate tenant endpoint exists
            return response.IsSuccessStatusCode || 
                   response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                   response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Response model for tenant creation
/// </summary>
public class CreateTenantResponse
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}

/// <summary>
/// Response model for tenant retrieval
/// </summary>
public class TenantResponse
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}
