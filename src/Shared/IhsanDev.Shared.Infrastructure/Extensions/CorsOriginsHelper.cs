namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Merges statically configured CORS origins (appsettings.json/appsettings.Docker.json's
/// Cors:AllowedOrigins) with origins from the CORS_EXTRA_ORIGINS environment variable — a single,
/// comma-separated value set once in docker-compose.yml (sourced from .env) and shared by every
/// service. Moving this platform to a new server/hostname then only requires updating that one
/// .env value instead of editing every service's Cors.AllowedOrigins array individually. See
/// Doc/DOCKER_DEPLOYMENT_GUIDE.md, "Pitfall: Cors.AllowedOrigins must match the frontend's ACTUAL
/// hostname".
/// </summary>
public static class CorsOriginsHelper
{
    public static string[] ResolveOrigins(string[] configuredOrigins)
    {
        var extra = Environment.GetEnvironmentVariable("CORS_EXTRA_ORIGINS");
        if (string.IsNullOrWhiteSpace(extra))
        {
            return configuredOrigins;
        }

        var extraOrigins = extra.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return configuredOrigins.Union(extraOrigins, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
