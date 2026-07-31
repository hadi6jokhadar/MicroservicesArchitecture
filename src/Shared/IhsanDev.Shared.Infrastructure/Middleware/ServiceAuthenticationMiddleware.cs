using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Infrastructure.Extensions;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Middleware to authenticate service-to-service communication using a shared secret
/// Allows internal services to communicate without requiring user JWT tokens
/// </summary>
public class ServiceAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ServiceAuthenticationMiddleware> _logger;
    private readonly string? _serviceSecret;
    private readonly bool _enabled;
    private readonly HashSet<string> _allowedServices;

    public ServiceAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ServiceAuthenticationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;

        _enabled = configuration.GetValue<bool>("ServiceCommunication:Enabled", true);
        _serviceSecret = configuration["ServiceCommunication:SharedSecret"];

        // Load allowed service names from configuration
        var allowedServices = configuration.GetSection("ServiceCommunication:AllowedServices")
            .Get<string[]>() ?? Array.Empty<string>();
        _allowedServices = new HashSet<string>(allowedServices, StringComparer.OrdinalIgnoreCase);

        if (_enabled)
        {
            JwtAuthenticationExtensions.ValidateSecretStrength(_serviceSecret, "ServiceCommunication:SharedSecret");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_enabled && !string.IsNullOrEmpty(_serviceSecret))
        {
            // Check for service authentication header
            if (context.Request.Headers.TryGetValue("X-Service-Secret", out var secretHeader))
            {
                // Constant-time comparison — this secret unlocks service-to-service auth for
                // the entire platform, so an ordinary == short-circuit is a timing side-channel
                // on the one value that matters most.
                if (FixedTimeEquals(secretHeader.ToString(), _serviceSecret))
                {
                    var serviceName = context.Request.Headers["X-Service-Name"].ToString();

                    // X-Service-Name is required, not optional: a missing header used to skip
                    // the allowlist check entirely instead of failing it. Reject outright now.
                    if (string.IsNullOrEmpty(serviceName))
                    {
                        _logger.LogWarning(
                            "Service secret presented with no X-Service-Name header. IP: {IP}, Path: {Path}",
                            context.Connection.RemoteIpAddress,
                            context.Request.Path);

                        await _next(context);
                        return;
                    }

                    // Validate service name if whitelist is configured
                    if (_allowedServices.Count > 0 && !_allowedServices.Contains(serviceName))
                    {
                        _logger.LogWarning(
                            "Service '{ServiceName}' is not in the allowed services list. IP: {IP}, Path: {Path}",
                            serviceName,
                            context.Connection.RemoteIpAddress,
                            context.Request.Path);

                        await _next(context);
                        return;
                    }

                    // Valid service request - add service claims. Deliberately NOT SuperAdmin:
                    // a "Service" role must only unlock internal, service-scoped endpoints.
                    // Endpoints that legitimately need service-to-service access declare
                    // RequireRole("Service", ...) explicitly instead of relying on this
                    // principal also holding platform admin rights.
                    var identity = new ClaimsIdentity("ServiceAccount");
                    identity.AddClaim(new Claim(ClaimTypes.Role, "Service"));
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "0")); // System user ID
                    identity.AddClaim(new Claim("IsInternalService", "true"));
                    identity.AddClaim(new Claim("ServiceName", serviceName));
                    identity.AddClaim(new Claim(ClaimTypes.Name, serviceName));

                    context.User = new ClaimsPrincipal(identity);

                    _logger.LogDebug(
                        "Authenticated service request from: {ServiceName}, IP: {IP}, Path: {Path}",
                        serviceName,
                        context.Connection.RemoteIpAddress,
                        context.Request.Path);
                }
                else
                {
                    _logger.LogWarning(
                        "Invalid service secret from IP: {IP}, Path: {Path}",
                        context.Connection.RemoteIpAddress,
                        context.Request.Path);
                }
            }
        }

        await _next(context);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        // CryptographicOperations.FixedTimeEquals still requires equal-length buffers to be
        // meaningfully constant-time; comparing against the hash of each side sidesteps a
        // length-based timing signal without weakening the actual secret comparison.
        var aHash = SHA256.HashData(aBytes);
        var bHash = SHA256.HashData(bBytes);
        return CryptographicOperations.FixedTimeEquals(aHash, bHash);
    }
}

/// <summary>
/// Extension methods for ServiceAuthenticationMiddleware
/// </summary>
public static class ServiceAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds service authentication middleware to the pipeline
    /// Must be called BEFORE UseAuthentication()
    /// </summary>
    public static IApplicationBuilder UseServiceAuthentication(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ServiceAuthenticationMiddleware>();
    }
}
