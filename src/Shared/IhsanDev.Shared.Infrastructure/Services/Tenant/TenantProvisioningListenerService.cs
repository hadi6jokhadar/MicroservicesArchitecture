using System.Text.Json;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Database;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IhsanDev.Shared.Infrastructure.Services.Tenant;

/// <summary>
/// Subscribes to <see cref="TenantProvisionedEventMessage.Channel"/> and, for every newly
/// created tenant, eagerly runs the same migrate step <see cref="DatabaseMigrationMiddleware{TContext}"/>
/// would otherwise defer to that tenant's first request for <typeparamref name="TContext"/> —
/// then seeds it (via the DbContext's own <c>SeedAsync</c> method, if it defines one, exactly
/// like <see cref="Extensions.DatabaseExtensions.InitializeDatabaseAsync{TContext}"/> does for the
/// global database at startup). This removes the need to restart every service after adding a
/// tenant just to trigger <see cref="Extensions.TenantWarmupExtensions.WarmTenantDatabaseMigrationsAsync{TContext}"/>.
/// A failure here is logged and swallowed — the tenant's first real request (or this service's
/// next restart) still catches it via the existing lazy/warm-up fallbacks.
/// </summary>
public sealed class TenantProvisioningListenerService<TContext> : BackgroundService
    where TContext : DbContext
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantProvisioningListenerService<TContext>> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TenantProvisioningListenerService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<TenantProvisioningListenerService<TContext>> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync(
            RedisChannel.Literal(TenantProvisionedEventMessage.Channel),
            (channel, message) => _ = HandleMessageAsync(message, stoppingToken));

        _logger.LogInformation(
            "Subscribed to '{Channel}' for eager tenant provisioning ({ContextType})",
            TenantProvisionedEventMessage.Channel, typeof(TContext).Name);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task HandleMessageAsync(RedisValue message, CancellationToken cancellationToken)
    {
        TenantProvisionedEventMessage? evt;
        try
        {
            evt = JsonSerializer.Deserialize<TenantProvisionedEventMessage>((string?)message ?? string.Empty, SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize tenant-provisioned event payload ({ContextType})",
                typeof(TContext).Name);
            return;
        }

        if (evt is null || string.IsNullOrWhiteSpace(evt.TenantId) ||
            evt.SchemaVersion != TenantProvisionedEventMessage.CurrentSchemaVersion)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();

            var configProvider = scope.ServiceProvider.GetRequiredService<ITenantConfigurationProvider>();
            var tenantInfo = await configProvider.GetTenantConfigurationAsync(evt.TenantId, cancellationToken);

            if (tenantInfo?.Configuration?.DatabaseSettings?.ConnectionString is null)
            {
                _logger.LogDebug(
                    "Tenant-provisioned event for '{TenantId}' has no database settings yet — skipping eager migration ({ContextType})",
                    evt.TenantId, typeof(TContext).Name);
                return;
            }

            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetTenant(tenantInfo);

            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            var migrationService = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();

            var migrated = await migrationService.EnsureDatabaseExistsAsync(dbContext, evt.TenantId, cancellationToken);
            if (!migrated)
            {
                _logger.LogWarning(
                    "Eager migration failed for tenant '{TenantId}' ({ContextType}) — will fall back to the tenant's first real request",
                    evt.TenantId, typeof(TContext).Name);
                return;
            }

            var seedMethod = dbContext.GetType().GetMethod("SeedAsync");
            if (seedMethod != null)
            {
                _logger.LogInformation(
                    "Seeding {ContextType} for newly provisioned tenant '{TenantId}'...",
                    typeof(TContext).Name, evt.TenantId);
                await (Task)seedMethod.Invoke(dbContext, null)!;
            }

            DatabaseMigrationMiddleware<TContext>.MarkAsMigrated(evt.TenantId);

            _logger.LogInformation(
                "Eagerly migrated{Seeded} {ContextType} for newly provisioned tenant '{TenantId}' — no restart needed",
                seedMethod != null ? " and seeded" : "", typeof(TContext).Name, evt.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Eager migration/seed failed for tenant '{TenantId}' ({ContextType}) — will fall back to the tenant's first real request or this service's next restart",
                evt.TenantId, typeof(TContext).Name);
        }
    }
}
