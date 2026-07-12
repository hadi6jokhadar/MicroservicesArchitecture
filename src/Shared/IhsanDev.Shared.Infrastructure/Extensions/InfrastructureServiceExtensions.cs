using IhsanDev.Shared.Application.Audit;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Infrastructure.Handlers.Audit;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Audit;
using IhsanDev.Shared.Infrastructure.Services.FeatureFlags;
using IhsanDev.Shared.Infrastructure.Services.Tenant;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers global exception handler and problem details
    /// </summary>
    public static IServiceCollection AddGlobalExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    /// <summary>
    /// Configures exception handling middleware
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        return app;
    }

    /// <summary>
    /// Registers the audit pipeline: singleton channel, background writer, and scoped per-request service.
    /// Call from every service's Program.cs.
    /// </summary>
    public static IServiceCollection AddAuditService(this IServiceCollection services)
    {
        services.AddSingleton<IAuditChannel, AuditChannelService>();
        services.AddHostedService<AuditBackgroundService>();
        services.AddScoped<IAuditService, DbAuditService>();
        return services;
    }

    /// <summary>
    /// Registers the generic audit log query handler for the given DbContext.
    /// Call from every service's Program.cs after AddAuditService().
    /// </summary>
    public static IServiceCollection AddAuditLogQueries<TDbContext>(this IServiceCollection services)
        where TDbContext : BaseDbContext
    {
        services.AddScoped<IRequestHandler<GetAuditLogsQuery, PaginatedList<AuditLogDto>>,
            GetAuditLogsQueryHandler<TDbContext>>();
        return services;
    }

    /// <summary>
    /// Registers the feature flag service backed by the current tenant's configuration.
    /// Call from every service's Program.cs that uses multi-tenancy.
    /// </summary>
    public static IServiceCollection AddFeatureFlagService(this IServiceCollection services)
    {
        services.AddScoped<IFeatureFlagService, TenantFeatureFlagService>();
        return services;
    }

    /// <summary>
    /// Registers the tenant timezone service backed by the current tenant's configuration.
    /// Falls back to UTC when the tenant has no timezone configured.
    /// Call from every service's Program.cs that uses multi-tenancy.
    /// </summary>
    public static IServiceCollection AddTenantTimeService(this IServiceCollection services)
    {
        services.AddScoped<ITenantTimeService, TenantTimeService>();
        return services;
    }

    /// <summary>
    /// Reads X-Correlation-Id from inbound requests (or generates one), stores it in
    /// HttpContext.Items, echoes it back in the response header, and enriches the
    /// structured log scope so every log line within the request includes CorrelationId.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    /// <summary>
    /// Registers localization service with proper resource path
    /// </summary>
    public static IServiceCollection AddLocalizationService(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ILocalizationService>(provider =>
        {
            var cache = provider.GetRequiredService<IMemoryCache>();
            var logger = provider.GetRequiredService<ILogger<LocalizationService>>();
            var resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Localization");
            
            return new LocalizationService(cache, logger, resourcesPath);
        });

        return services;
    }
}