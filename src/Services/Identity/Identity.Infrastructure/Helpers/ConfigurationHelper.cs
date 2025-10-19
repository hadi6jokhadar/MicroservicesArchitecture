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
    /// Gets configuration value with tenant fallback support
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
        // Try tenant config first if available
        if (tenantContext.HasTenant && 
            tenantContext.CurrentTenant?.Configuration != null &&
            tenantValueSelector != null)
        {
            var tenantValue = tenantValueSelector(tenantContext.CurrentTenant.Configuration);
            if (!string.IsNullOrEmpty(tenantValue))
            {
                return tenantValue;
            }
        }

        // Fallback to appsettings
        return configuration[configKey] 
            ?? throw new InvalidOperationException($"Configuration key '{configKey}' not found in appsettings or tenant configuration");
    }

    /// <summary>
    /// Gets JWT settings with automatic tenant/default resolution
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="tenantContext">Current tenant context</param>
    /// <returns>JWT settings from tenant configuration or appsettings</returns>
    public static JwtSettings GetJwtSettings(
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        // If tenant has custom JWT config, use it
        if (tenantContext.HasTenant && 
            tenantContext.CurrentTenant?.Configuration?.Jwt != null)
        {
            var tenantJwt = tenantContext.CurrentTenant.Configuration.Jwt;
            if (!string.IsNullOrEmpty(tenantJwt.Secret))
            {
                return new JwtSettings
                {
                    Secret = tenantJwt.Secret,
                    Issuer = tenantJwt.Issuer ?? configuration["Jwt:Issuer"]!,
                    Audience = tenantJwt.Audience ?? configuration["Jwt:Audience"]!,
                    AccessTokenExpirationMinutes = tenantJwt.AccessTokenExpirationMinutes > 0
                        ? tenantJwt.AccessTokenExpirationMinutes
                        : int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60")
                };
            }
        }

        // Fallback to appsettings
        return new JwtSettings
        {
            Secret = configuration["Jwt:Secret"] 
                ?? throw new InvalidOperationException("JWT Secret not configured"),
            Issuer = configuration["Jwt:Issuer"] 
                ?? throw new InvalidOperationException("JWT Issuer not configured"),
            Audience = configuration["Jwt:Audience"] 
                ?? throw new InvalidOperationException("JWT Audience not configured"),
            AccessTokenExpirationMinutes = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60")
        };
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
            tenant => tenant.Database?.ConnectionString
        );
    }

    /// <summary>
    /// Gets CORS allowed origins with tenant support
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="tenantContext">Current tenant context</param>
    /// <returns>Array of allowed CORS origins</returns>
    public static string[] GetCorsAllowedOrigins(
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        // Try tenant config first
        if (tenantContext.HasTenant && 
            tenantContext.CurrentTenant?.Configuration?.Cors?.AllowedOrigins?.Length > 0)
        {
            return tenantContext.CurrentTenant.Configuration.Cors.AllowedOrigins;
        }

        // Fallback to appsettings
        return configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
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
