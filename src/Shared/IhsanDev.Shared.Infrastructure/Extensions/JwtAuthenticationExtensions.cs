using IhsanDev.Shared.Kernel.Enums;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for JWT authentication configuration
/// </summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Adds JWT authentication with optional per-tenant support
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration to read JWT settings</param>
    /// <param name="enablePerTenantJwt">Whether to enable per-tenant JWT validation</param>
    /// <param name="customMessageReceived">Optional custom OnMessageReceived handler that runs before tenant JWT logic</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enablePerTenantJwt = true,
        Func<MessageReceivedContext, Task>? customMessageReceived = null)
    {
        // Read JWT mode configuration to determine if JWT is shared or per-tenant
        var jwtModeString = configuration["MultiTenancy:JwtMode"] ?? "Shared";
        var jwtMode = Enum.TryParse<JwtMode>(jwtModeString, ignoreCase: true, out var parsedMode)
            ? parsedMode
            : JwtMode.Shared;

        // Always use Jwt section from appsettings.json (for both Shared and PerTenant modes)
        var jwtSettings = configuration.GetSection("Jwt");

        var secretKey = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Support per-tenant JWT validation when JwtMode is PerTenant and enabled
            if (enablePerTenantJwt && jwtMode == JwtMode.PerTenant)
            {
                options.Events = CreatePerTenantJwtEvents(secretKey, jwtSettings, customMessageReceived);
            }
            else if (customMessageReceived != null)
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = customMessageReceived,
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuthentication");
                        var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var path = context.HttpContext.Request.Path;
                        logger.LogInformation("JWT Token Validated - User ID: {UserId}, Path: {Path}", userId ?? "Unknown", path);
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuthentication");
                        var path = context.HttpContext.Request.Path;
                        logger.LogError("JWT Authentication Failed - Path: {Path}, Error: {Error}", path, context.Exception.Message);
                        return Task.CompletedTask;
                    }
                };
            }
        });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Adds JWT authentication without per-tenant support (always uses global JWT)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration to read JWT settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddJwtAuthenticationSharedOnly(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");

        var secretKey = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured in appsettings.json");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        return services;
    }

    private static JwtBearerEvents CreatePerTenantJwtEvents(
        string secretKey,
        IConfigurationSection jwtSettings,
        Func<MessageReceivedContext, Task>? customMessageReceived)
    {
        var events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");

                // Run custom message received handler first (e.g., for SignalR token extraction)
                if (customMessageReceived != null)
                {
                    customMessageReceived(context).GetAwaiter().GetResult();
                }

                var tenantId = context.HttpContext.Request.Headers["x-tenant-id"].FirstOrDefault();

                // Always create a fresh TokenValidationParameters to avoid cross-request contamination
                // Only attempt tenant-specific JWT validation if x-tenant-id header is present
                if (!string.IsNullOrEmpty(tenantId))
                {
                    try
                    {
                        var tenantConfigProvider = context.HttpContext.RequestServices.GetService<ITenantConfigurationProvider>();
                        if (tenantConfigProvider != null)
                        {
                            var tenant = tenantConfigProvider.GetTenantConfigurationAsync(tenantId, context.HttpContext.RequestAborted)
                                .GetAwaiter().GetResult();

                            if (tenant?.Configuration?.Jwt != null)
                            {
                                var tenantJwt = tenant.Configuration.Jwt;
                                if (!string.IsNullOrEmpty(tenantJwt.Secret))
                                {
                                    // Override with tenant-specific JWT validation parameters
                                    context.Options.TokenValidationParameters = new TokenValidationParameters
                                    {
                                        ValidateIssuer = true,
                                        ValidateAudience = true,
                                        ValidateLifetime = true,
                                        ValidateIssuerSigningKey = true,
                                        ValidIssuer = tenantJwt.Issuer,
                                        ValidAudience = tenantJwt.Audience,
                                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tenantJwt.Secret)),
                                        ClockSkew = TimeSpan.Zero
                                    };

                                    logger.LogInformation("🔐 Using tenant-specific JWT validation for tenant: {TenantId} (Issuer: {Issuer})",
                                        tenantId, tenantJwt.Issuer);
                                    return Task.CompletedTask;
                                }
                            }

                            logger.LogWarning("Tenant {TenantId} has no JWT configuration, falling back to global JWT", tenantId);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to fetch tenant configuration for tenant: {TenantId}, falling back to global JWT", tenantId);
                    }
                }

                // Use global JWT validation (no tenant header OR tenant config fetch failed)
                logger.LogInformation("Using global JWT validation - Secret: {SecretLength} chars, Issuer: {Issuer}",
                    secretKey.Length, jwtSettings["Issuer"]);

                // Explicitly set global JWT parameters
                context.Options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var path = context.HttpContext.Request.Path;
                logger.LogInformation("JWT Token Validated - User ID: {UserId}, Path: {Path}", userId ?? "Unknown", path);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");
                var path = context.HttpContext.Request.Path;
                logger.LogError("JWT Authentication Failed - Path: {Path}, Error: {Error}", path, context.Exception.Message);
                return Task.CompletedTask;
            }
        };

        return events;
    }
}
