using Microsoft.Extensions.Configuration;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace Identity.Infrastructure.Helpers;

/// <summary>
/// Helper class for accessing configuration with automatic tenant/default resolution
/// </summary>
public static class ConfigurationHelper
{
    /// <summary>
    /// Gets configuration value based on multi-tenancy mode
    /// When multi-tenancy is enabled: ONLY use tenant configuration (no fallback)
    /// When multi-tenancy is disabled: use appsettings.json
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="tenantContext">Current tenant context</param>
    /// <param name="configKey">Configuration key (e.g., "Jwt:Secret")</param>
    /// <param name="tenantValueSelector">Function to extract value from tenant configuration</param>
    /// <returns>Configuration value from tenant or appsettings</returns>
    public static string GetConfigValue(
        IConfiguration configuration,
        ITenantContext tenantContext,
        string configKey,
        Func<TenantConfiguration, string?>? tenantValueSelector = null)
    {
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);

        if (multiTenancyEnabled)
        {
            // When multi-tenancy is enabled, ONLY use tenant configuration
            if (!tenantContext.HasTenant || 
                tenantContext.CurrentTenant?.Configuration == null ||
                tenantValueSelector == null)
            {
                throw new InvalidOperationException(
                    $"Multi-tenancy is enabled but tenant configuration is not available for key '{configKey}'. " +
                    "Ensure x-tenant-id header is provided and tenant exists with valid configuration.");
            }

            var tenantValue = tenantValueSelector(tenantContext.CurrentTenant.Configuration);
            if (string.IsNullOrEmpty(tenantValue))
            {
                throw new InvalidOperationException(
                    $"Configuration key '{configKey}' not found in tenant configuration");
            }

            return tenantValue;
        }
        else
        {
            // When multi-tenancy is disabled, use appsettings.json
            return configuration[configKey] 
                ?? throw new InvalidOperationException($"Configuration key '{configKey}' not found in appsettings.json");
        }
    }

    /// <summary>
    /// Gets JWT settings based on multi-tenancy mode
    /// When multi-tenancy is enabled: ONLY use tenant JWT settings (no fallback)
    /// When multi-tenancy is disabled: use appsettings.json
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="tenantContext">Current tenant context</param>
    /// <returns>JWT settings from tenant configuration or appsettings</returns>
    public static JwtSettings GetJwtSettings(
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);

        if (multiTenancyEnabled)
        {
            // When multi-tenancy is enabled, ONLY use tenant JWT configuration
            if (!tenantContext.HasTenant || 
                tenantContext.CurrentTenant?.Configuration?.Jwt == null ||
                string.IsNullOrEmpty(tenantContext.CurrentTenant.Configuration.Jwt.Secret))
            {
                throw new InvalidOperationException(
                    "Multi-tenancy is enabled but tenant JWT configuration is not available. " +
                    "Ensure x-tenant-id header is provided and tenant exists with valid JWT settings.");
            }

            var tenantJwt = tenantContext.CurrentTenant.Configuration.Jwt;
            return new JwtSettings
            {
                Secret = tenantJwt.Secret,
                Issuer = tenantJwt.Issuer ?? "IdentityService",
                Audience = tenantJwt.Audience ?? "MicroservicesApp",
                AccessTokenExpirationMinutes = tenantJwt.AccessTokenExpirationMinutes > 0
                    ? tenantJwt.AccessTokenExpirationMinutes
                    : 60
            };
        }
        else
        {
            // When multi-tenancy is disabled, use appsettings.json
            return new JwtSettings
            {
                Secret = configuration["Jwt:Secret"] 
                    ?? throw new InvalidOperationException("JWT Secret not configured in appsettings.json"),
                Issuer = configuration["Jwt:Issuer"] 
                    ?? throw new InvalidOperationException("JWT Issuer not configured in appsettings.json"),
                Audience = configuration["Jwt:Audience"] 
                    ?? throw new InvalidOperationException("JWT Audience not configured in appsettings.json"),
                AccessTokenExpirationMinutes = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60")
            };
        }
    }

    /// <summary>
    /// Gets database connection string with tenant support
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="tenantContext">Current tenant context</param>
    /// <returns>Database connection string from tenant configuration or appsettings</returns>
    public static string GetDatabaseConnectionString(
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        return GetConfigValue(
            configuration,
            tenantContext,
            "DatabaseSettings:ConnectionString",
            tenant => tenant.DatabaseSettings?.ConnectionString
        );
    }

    /// <summary>
    /// Gets CORS allowed origins based on multi-tenancy mode
    /// When multi-tenancy is enabled: ONLY use tenant CORS settings (no fallback)
    /// When multi-tenancy is disabled: use appsettings.json
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="tenantContext">Current tenant context</param>
    /// <returns>Array of allowed CORS origins</returns>
    public static string[] GetCorsAllowedOrigins(
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);

        if (multiTenancyEnabled)
        {
            // When multi-tenancy is enabled, ONLY use tenant CORS configuration
            if (!tenantContext.HasTenant || 
                tenantContext.CurrentTenant?.Configuration?.Cors?.AllowedOrigins == null ||
                tenantContext.CurrentTenant.Configuration.Cors.AllowedOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    "Multi-tenancy is enabled but tenant CORS configuration is not available. " +
                    "Ensure x-tenant-id header is provided and tenant exists with valid CORS settings.");
            }

            return tenantContext.CurrentTenant.Configuration.Cors.AllowedOrigins;
        }
        else
        {
            // When multi-tenancy is disabled, use appsettings.json
            return configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        }
    }
}

/// <summary>
/// JWT configuration settings
/// </summary>
public record JwtSettings
{
    public required string Secret { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int AccessTokenExpirationMinutes { get; init; }
}
