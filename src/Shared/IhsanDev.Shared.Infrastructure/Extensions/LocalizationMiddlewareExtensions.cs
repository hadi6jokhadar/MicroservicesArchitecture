using IhsanDev.Shared.Infrastructure.Middleware;
using Microsoft.AspNetCore.Builder;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering localization middleware
/// </summary>
public static class LocalizationMiddlewareExtensions
{
    /// <summary>
    /// Use localization middleware to detect language from Accept-Language header
    /// Should be registered early in the pipeline (after routing, before authentication)
    /// </summary>
    public static IApplicationBuilder UseLocalization(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LocalizationMiddleware>();
    }
}
