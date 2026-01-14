using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Identity.Infrastructure.Services;

namespace Identity.Infrastructure.Middleware;

/// <summary>
/// Middleware that automatically seeds default roles and claims for each tenant
/// Similar to DatabaseMigrationMiddleware, runs once per tenant on first request
/// </summary>
public class DatabaseSeederMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DatabaseSeederMiddleware> _logger;
    private static readonly HashSet<string> _seededTenants = new();
    private static readonly SemaphoreSlim _seedingLock = new(1, 1);

    public DatabaseSeederMiddleware(
        RequestDelegate next,
        ILogger<DatabaseSeederMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext)
    {
        // Skip seeding for static paths (Swagger, health checks, etc.)
        if (ShouldSkipSeeding(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Determine tenant key for seeding tracking
        string tenantKey;
        if (tenantContext.IsMultiTenantMode && tenantContext.HasTenant)
        {
            tenantKey = tenantContext.CurrentTenant?.TenantId ?? "default";
        }
        else
        {
            tenantKey = "default";
        }

        // Check if we've already seeded this tenant in this application lifetime
        if (!_seededTenants.Contains(tenantKey))
        {
            await _seedingLock.WaitAsync();
            try
            {
                // Double-check inside lock
                if (!_seededTenants.Contains(tenantKey))
                {
                    _logger.LogDebug(
                        "First request for tenant/database '{TenantKey}', seeding default roles and claims...",
                        tenantKey);

                    try
                    {
                        // Get the seeder service
                        var seeder = context.RequestServices.GetRequiredService<DatabaseSeeder>();
                        await seeder.SeedDefaultRolesAndClaimsAsync(context.RequestAborted);

                        _seededTenants.Add(tenantKey);
                        _logger.LogInformation(
                            "Default roles and claims seeded successfully for tenant/database '{TenantKey}'",
                            tenantKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "An error occurred while seeding default roles and claims for tenant/database '{TenantKey}'",
                            tenantKey);
                        // Continue anyway - seeding is not critical for request processing
                    }
                }
            }
            finally
            {
                _seedingLock.Release();
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Determines if seeding should be skipped for the given path
    /// </summary>
    private static bool ShouldSkipSeeding(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        if (string.IsNullOrEmpty(pathValue))
            return false;

        // Skip Swagger UI and API documentation paths
        if (pathValue.StartsWith("/swagger"))
            return true;

        // Skip health check endpoints
        if (pathValue.StartsWith("/health"))
            return true;

        // Skip metrics endpoints
        if (pathValue.StartsWith("/metrics"))
            return true;

        return false;
    }

    /// <summary>
    /// Clear the seeding cache (useful for testing or when roles need to be re-seeded)
    /// </summary>
    public static void ClearSeedingCache(string? tenantKey = null)
    {
        _seedingLock.Wait();
        try
        {
            if (tenantKey != null)
            {
                _seededTenants.Remove(tenantKey);
            }
            else
            {
                _seededTenants.Clear();
            }
        }
        finally
        {
            _seedingLock.Release();
        }
    }
}
