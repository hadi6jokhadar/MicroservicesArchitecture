using IhsanDev.Shared.Infrastructure.Attributes;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Application.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Middleware that extracts tenant ID from request header and resolves tenant configuration
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantConfigurationProvider tenantConfigProvider,
        ILocalizationService localizationService)
    {
        // Set culture early from Accept-Language header for error messages
        SetCultureFromRequest(context, localizationService);

        // Skip OPTIONS preflight requests (CORS) - they don't need tenant resolution
        if (context.Request.Method == "OPTIONS")
        {
            _logger.LogDebug("Skipping tenant resolution for OPTIONS preflight request");
            await _next(context);
            return;
        }

        // Check if multi-tenancy is enabled
        if (!tenantContext.IsMultiTenantMode)
        {
            _logger.LogDebug("Multi-tenancy is disabled, skipping tenant resolution");
            await _next(context);
            return;
        }

        // Skip tenant resolution for static files (images, videos, documents, etc.)
        var path = context.Request.Path.Value ?? "";
        var isStaticFile = path.Contains(".") && !path.StartsWith("/api/");

        if (isStaticFile)
        {
            _logger.LogDebug("Skipping tenant resolution for static file: {Path}", path);
            await _next(context);
            return;
        }

        // Skip tenant resolution for observability/infrastructure endpoints
        if (path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Skipping tenant resolution for infrastructure endpoint: {Path}", path);
            await _next(context);
            return;
        }

        // Check if endpoint has metadata to bypass tenant resolution
        var endpoint = context.GetEndpoint();
        var bypassTenant = endpoint?.Metadata.GetMetadata<BypassTenantAttribute>() != null;
        var optionalTenant = endpoint?.Metadata.GetMetadata<OptionalTenantAttribute>() != null;
        
        if (bypassTenant)
        {
            _logger.LogDebug("Bypassing tenant resolution for endpoint with BypassTenant attribute");
            await _next(context);
            return;
        }

        // Extract tenant ID from header
        var tenantId = context.Request.Headers["x-tenant-id"].FirstOrDefault();

        // When multi-tenancy is enabled, x-tenant-id header is REQUIRED (unless OptionalTenant)
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            if (optionalTenant)
            {
                // Tenant is optional - continue without tenant context
                _logger.LogDebug("Tenant ID not provided but endpoint allows optional tenant. Continuing without tenant context.");
                await _next(context);
                return;
            }
            
            _logger.LogWarning("Multi-tenancy is enabled but x-tenant-id header is missing");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = localizationService.GetString(LocalizationKeys.Tenant.MissingHeader),
                message = localizationService.GetString(LocalizationKeys.Tenant.MissingHeaderMessage),
                details = localizationService.GetString(LocalizationKeys.Tenant.MissingHeaderDetails)
            });
            return;
        }

        _logger.LogDebug("Resolving tenant configuration for tenant ID: {TenantId}", tenantId);

        try
        {
            // Fetch tenant configuration
            var tenantInfo = await tenantConfigProvider.GetTenantConfigurationAsync(
                tenantId,
                context.RequestAborted);

            if (tenantInfo == null)
            {
                _logger.LogWarning("Tenant '{TenantId}' not found or inactive", tenantId);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = localizationService.GetString(LocalizationKeys.Tenant.NotFoundOrInactive),
                    tenantId
                });
                return;
            }

            if (!tenantInfo.IsActive)
            {
                _logger.LogWarning("Tenant '{TenantId}' is not active", tenantId);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = localizationService.GetString(LocalizationKeys.Tenant.NotActive),
                    tenantId
                });
                return;
            }

            // Set tenant context for the request
            tenantContext.SetTenant(tenantInfo);
            _logger.LogInformation("Tenant context set for tenant: {TenantId} ({TenantName})",
                tenantInfo.TenantId, tenantInfo.TenantName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving tenant configuration for '{TenantId}'", tenantId);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                error = localizationService.GetString(LocalizationKeys.Tenant.ConfigurationError)
            });
            return;
        }

        await _next(context);
    }

    private void SetCultureFromRequest(HttpContext context, ILocalizationService localizationService)
    {
        try
        {
            // Check x-culture header first
            if (context.Request.Headers.TryGetValue("x-culture", out var cultureHeader))
            {
                var culture = cultureHeader.ToString().ToLowerInvariant();
                if (IsSupportedCulture(culture))
                {
                    localizationService.SetCulture(culture);
                    return;
                }
            }

            // Check Accept-Language header
            if (context.Request.Headers.TryGetValue("Accept-Language", out var acceptLanguageHeader))
            {
                var acceptLanguage = acceptLanguageHeader.ToString();
                var culture = ParseAcceptLanguage(acceptLanguage);
                if (!string.IsNullOrEmpty(culture))
                {
                    localizationService.SetCulture(culture);
                    return;
                }
            }

            // Use default culture
            localizationService.SetCulture("en");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting culture, using default");
            localizationService.SetCulture("en");
        }
    }

    private string? ParseAcceptLanguage(string acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
            return null;

        var languages = acceptLanguage
            .Split(',')
            .Select(lang => lang.Split(';')[0].Trim())
            .Select(lang => lang.Split('-')[0].ToLowerInvariant())
            .Where(IsSupportedCulture)
            .ToList();

        return languages.FirstOrDefault();
    }

    private bool IsSupportedCulture(string culture)
    {
        return culture == "en" || culture == "ar";
    }
}
