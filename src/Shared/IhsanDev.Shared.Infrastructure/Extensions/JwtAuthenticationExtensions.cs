using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Enums;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Text;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for JWT authentication configuration
/// </summary>
public static class JwtAuthenticationExtensions
{
    private static readonly string[] KnownPlaceholderSecrets =
    {
        "CHANGE_ME_JWT_SECRET",
        "CHANGE_ME_SHARED_SECRET",
        "your-secret-key-here-min-32-chars-for-production-use"
    };

    /// <summary>
    /// Fails fast at startup if a signing/shared secret is missing, too short to be a real
    /// key, or matches a known committed placeholder value. Called for both Jwt:Secret and
    /// ServiceCommunication:SharedSecret — a weak or default value on either one is a total
    /// platform compromise (forgeable JWTs / forgeable service-to-service auth).
    ///
    /// Skipped when running under a test host: every service's WebApplicationFactory-based
    /// integration tests set their own Jwt:Secret override via ConfigureAppConfiguration, but for
    /// a minimal-hosting Program.cs that override isn't merged into the SAME ConfigurationManager
    /// until builder.Build() runs — and this method is called from Program.cs's own top-level code
    /// BEFORE that point (both the read of configuration["Jwt:Secret"] here and the
    /// SymmetricSecurityKey built from it happen pre-Build). So every test run has always signed/
    /// validated tokens using the literal "CHANGE_ME_JWT_SECRET" placeholder as the real key —
    /// self-consistent and harmless before this check existed (nothing ever inspected the string's
    /// content), but a hard failure the moment this validation was added. ASPNETCORE_ENVIRONMENT
    /// isn't a reliable signal here either (WebApplicationFactory's UseEnvironment() is subject to
    /// the exact same pre-Build timing gap), so this checks the OS process name instead — "testhost"
    /// is VSTest's actual runner process for `dotnet test`, unrelated to ASP.NET Core's config
    /// pipeline. Real deployments never run under that process name.
    /// </summary>
    internal static void ValidateSecretStrength(string? secret, string settingName)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException($"{settingName} is not configured.");

        if (IsRunningUnderTestHost())
            return;

        if (KnownPlaceholderSecrets.Any(p => string.Equals(p, secret, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                $"{settingName} is still set to a committed placeholder value. " +
                $"Set a real, unique secret (at least 32 bytes) in appsettings.Development.json " +
                $"or the deployment's secrets manager/environment variables.");

        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException(
                $"{settingName} is only {Encoding.UTF8.GetByteCount(secret)} bytes — must be at least 32 bytes " +
                $"(256 bits) to be a safe HMAC-SHA256 signing/shared secret.");
    }

    private static bool IsRunningUnderTestHost()
    {
        var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        return processName.Contains("testhost", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("vstest", StringComparison.OrdinalIgnoreCase);
    }

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
        ValidateSecretStrength(secretKey, "Jwt:Secret");
        var globalIssuer = jwtSettings["Issuer"];
        var globalAudience = jwtSettings["Audience"];
        var globalSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        // IHttpContextAccessor is required by the PerTenant resolver below (reads the
        // already-resolved, request-scoped ITenantContext instead of re-fetching tenant
        // config here). TryAddSingleton under the hood, safe to call regardless of whether
        // AddCustomLogging/other extensions already registered it.
        services.AddHttpContextAccessor();

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
                IssuerSigningKey = globalSigningKey,
                ValidateIssuer = true,
                ValidIssuer = globalIssuer,
                ValidateAudience = true,
                ValidAudience = globalAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            if (customMessageReceived != null || (enablePerTenantJwt && jwtMode == JwtMode.PerTenant))
            {
                options.Events = new JwtBearerEvents
                {
                    // Only extracts the token (e.g. SignalR query-string tokens) — never
                    // mutates shared Options state. Per-tenant key/issuer/audience resolution
                    // happens below via IssuerSigningKeyResolver/IssuerValidator/AudienceValidator,
                    // which run per-validation with no shared mutable state to race on.
                    OnMessageReceived = customMessageReceived ?? (_ => Task.CompletedTask),
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

        // Wire up per-tenant signing-key/issuer/audience resolution via DI-aware options
        // configuration (Microsoft.Extensions.Options), NOT via a per-request event handler
        // that mutates JwtBearerOptions.TokenValidationParameters — that object is a single
        // instance SHARED by every concurrent request. Mutating it from OnMessageReceived
        // meant one request's validation could run against a different, concurrently
        // in-flight request's freshly-overwritten parameters, intermittently rejecting valid
        // tokens with "signature key was not found" under load (see LOAD_TESTING_GUIDE.md).
        //
        // The resolver/validators below are pure, stateless callbacks — no writes to shared
        // state — and read ITenantContext (already populated earlier in the SAME request's
        // pipeline by UseTenantResolution, which runs before UseAuthentication) via
        // IHttpContextAccessor instead of independently re-fetching tenant config, so there's
        // no extra I/O and no blocking sync-over-async call in the hot path either.
        if (enablePerTenantJwt && jwtMode == JwtMode.PerTenant)
        {
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IHttpContextAccessor>((options, httpContextAccessor) =>
                {
                    TenantInfo? CurrentTenant() =>
                        httpContextAccessor.HttpContext?.RequestServices
                            .GetService<ITenantContext>()?.CurrentTenant;

                    options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) =>
                    {
                        var keys = new List<SecurityKey> { globalSigningKey };
                        var tenantSecret = CurrentTenant()?.Configuration?.Jwt?.Secret;
                        if (!string.IsNullOrWhiteSpace(tenantSecret))
                            keys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tenantSecret)));
                        return keys;
                    };

                    options.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                    {
                        var tenantIssuer = CurrentTenant()?.Configuration?.Jwt?.Issuer;
                        if (!string.IsNullOrWhiteSpace(tenantIssuer) && issuer == tenantIssuer)
                            return issuer;
                        if (issuer == globalIssuer)
                            return issuer;
                        throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' is not valid.");
                    };

                    options.TokenValidationParameters.AudienceValidator = (audiences, _, _) =>
                    {
                        var tenantAudience = CurrentTenant()?.Configuration?.Jwt?.Audience;
                        return audiences.Any(a => a == tenantAudience || a == globalAudience);
                    };
                });
        }

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
        ValidateSecretStrength(secretKey, "Jwt:Secret");

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
}
