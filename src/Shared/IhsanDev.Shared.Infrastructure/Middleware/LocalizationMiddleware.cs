using System.Globalization;
using IhsanDev.Shared.Application.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Middleware to detect and set culture from Accept-Language header
/// Supports: en, ar, and fallback to default culture
/// </summary>
public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LocalizationMiddleware> _logger;
    private const string DefaultCulture = "en";
    private static readonly string[] SupportedCultures = { "en", "ar" };

    public LocalizationMiddleware(
        RequestDelegate next,
        ILogger<LocalizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ILocalizationService localizationService)
    {
        try
        {
            var culture = GetCultureFromRequest(context);
            SetCulture(culture, localizationService);

            _logger.LogDebug("Request culture set to: {Culture}", culture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting culture, using default: {DefaultCulture}", DefaultCulture);
            SetCulture(DefaultCulture, localizationService);
        }

        await _next(context);
    }

    private string GetCultureFromRequest(HttpContext context)
    {
        // 1. Check x-culture header (custom header for explicit language selection)
        if (context.Request.Headers.TryGetValue("x-culture", out var cultureHeader))
        {
            var culture = cultureHeader.ToString().ToLowerInvariant();
            if (IsSupportedCulture(culture))
            {
                _logger.LogDebug("Culture from x-culture header: {Culture}", culture);
                return culture;
            }
        }

        // 2. Check Accept-Language header
        if (context.Request.Headers.TryGetValue("Accept-Language", out var acceptLanguageHeader))
        {
            var acceptLanguage = acceptLanguageHeader.ToString();
            var culture = ParseAcceptLanguage(acceptLanguage);
            if (!string.IsNullOrEmpty(culture))
            {
                _logger.LogDebug("Culture from Accept-Language header: {Culture}", culture);
                return culture;
            }
        }

        // 3. Fallback to default culture
        _logger.LogDebug("No valid culture found, using default: {DefaultCulture}", DefaultCulture);
        return DefaultCulture;
    }

    private string? ParseAcceptLanguage(string acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
            return null;

        // Parse Accept-Language header (e.g., "en-US,en;q=0.9,ar;q=0.8")
        var languages = acceptLanguage
            .Split(',')
            .Select(lang => lang.Split(';')[0].Trim()) // Remove quality values (q=0.9)
            .Select(lang => lang.Split('-')[0].ToLowerInvariant()) // Get language code only (en from en-US)
            .Where(IsSupportedCulture)
            .ToList();

        return languages.FirstOrDefault();
    }

    private bool IsSupportedCulture(string culture)
    {
        return SupportedCultures.Contains(culture.ToLowerInvariant());
    }

    private void SetCulture(string culture, ILocalizationService localizationService)
    {
        var cultureInfo = new CultureInfo(culture);
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;

        // Also set in localization service
        localizationService.SetCulture(culture);
    }
}
