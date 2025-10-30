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
    public OtpSettings? Otp { get; set; }
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

/// <summary>
/// OTP (One-Time Password) settings for tenant
/// </summary>
public class OtpSettings
{
    /// <summary>
    /// Length of the verification code (default: 6)
    /// Valid range: 4-10 characters
    /// </summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>
    /// Code expiration time in seconds (default: 300 = 5 minutes)
    /// Valid range: 30 seconds to 30 minutes (1800 seconds)
    /// </summary>
    public int ExpirationSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum failed attempts before lockout (default: 3)
    /// Valid range: 1-10 attempts
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Lockout duration in minutes after max attempts (default: 15)
    /// Valid range: 1-60 minutes
    /// </summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>
    /// Cooldown period in seconds before allowing resend (default: 60)
    /// Valid range: 10-300 seconds (5 minutes)
    /// </summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>
    /// Use alphanumeric codes instead of numeric only (default: false)
    /// </summary>
    public bool UseAlphanumeric { get; set; } = false;

    /// <summary>
    /// Secret key for additional security (optional)
    /// Minimum length: 32 characters for production use
    /// </summary>
    public string? SecretKey { get; set; }
}
