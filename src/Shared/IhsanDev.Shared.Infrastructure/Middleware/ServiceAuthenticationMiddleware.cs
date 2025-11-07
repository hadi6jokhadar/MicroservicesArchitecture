using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

        if (_enabled && string.IsNullOrEmpty(_serviceSecret))
        {
            _logger.LogWarning(
                "Service authentication is enabled but no shared secret is configured. " +
                "Service-to-service communication will not work properly.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_enabled && !string.IsNullOrEmpty(_serviceSecret))
        {
            // Check for service authentication header
            if (context.Request.Headers.TryGetValue("X-Service-Secret", out var secretHeader))
            {
                // Validate the secret
                if (secretHeader == _serviceSecret)
                {
                    var serviceName = context.Request.Headers["X-Service-Name"].ToString();

                    // Validate service name if whitelist is configured
                    if (_allowedServices.Count > 0 && !string.IsNullOrEmpty(serviceName))
                    {
                        if (!_allowedServices.Contains(serviceName))
                        {
                            _logger.LogWarning(
                                "Service '{ServiceName}' is not in the allowed services list. IP: {IP}, Path: {Path}",
                                serviceName,
                                context.Connection.RemoteIpAddress,
                                context.Request.Path);

                            await _next(context);
                            return;
                        }
                    }

                    // Valid service request - add service claims
                    var identity = new ClaimsIdentity("ServiceAccount");
                    identity.AddClaim(new Claim(ClaimTypes.Role, "Service"));
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "0")); // System user ID
                    identity.AddClaim(new Claim("IsInternalService", "true"));

                    if (!string.IsNullOrEmpty(serviceName))
                    {
                        identity.AddClaim(new Claim("ServiceName", serviceName));
                        identity.AddClaim(new Claim(ClaimTypes.Name, serviceName));
                    }

                    context.User = new ClaimsPrincipal(identity);

                    _logger.LogDebug(
                        "Authenticated service request from: {ServiceName}, IP: {IP}, Path: {Path}",
                        serviceName ?? "Unknown",
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
