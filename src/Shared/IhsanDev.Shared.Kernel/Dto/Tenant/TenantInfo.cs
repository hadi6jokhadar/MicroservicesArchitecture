namespace IhsanDev.Shared.Kernel.Dto.Tenant;

/// <summary>
/// Tenant context information available throughout the request pipeline
/// </summary>
public class TenantInfo
{
    /// <summary>
    /// Unique tenant identifier
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Tenant display name
    /// </summary>
    public string? TenantName { get; set; }

    /// <summary>
    /// User ID who owns this tenant
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Indicates if tenant is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Tenant-specific configuration (JWT, Database, CORS, etc.)
    /// </summary>
    public TenantConfiguration? Configuration { get; set; }
}

/// <summary>
/// Tenant-specific configuration settings
/// </summary>
public class TenantConfiguration
{
    public JwtSettings? Jwt { get; set; }
    public DatabaseSettings? DatabaseSettings { get; set; }
    public CorsSettings? Cors { get; set; }
}

/// <summary>
/// JWT settings for tenant
/// </summary>
public class JwtSettings
{
    public string? Secret { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public int AccessTokenExpirationMinutes { get; set; }
    public int RefreshTokenExpirationDays { get; set; }
}

/// <summary>
/// Database settings for tenant
/// </summary>
public class DatabaseSettings
{
    public string? Provider { get; set; }
    public string? ConnectionString { get; set; }
}

/// <summary>
/// CORS settings for tenant
/// </summary>
public class CorsSettings
{
    public string[]? AllowedOrigins { get; set; }
}
