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

    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This is a pure optimization (see class summary) — a missed/failed subscription just
        // means every tenant falls back to the pre-existing lazy per-request migration, exactly as
        // if this listener didn't exist. So a subscribe failure must never escape ExecuteAsync
        // (an unhandled BackgroundService exception is fatal to the whole host under the default
        // HostOptions.BackgroundServiceExceptionBehavior), and it must not just be given up on for
        // the rest of the process's life either — retry with backoff so a transient Redis hiccup
        // (down, slow/flaky during startup, auth handshake race) self-heals within seconds/minutes
        // instead of requiring a full service restart to ever pick the feature back up.
        await SubscribeWithRetryAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task SubscribeWithRetryAsync(CancellationToken stoppingToken)
    {
        var delay = InitialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var subscriber = _redis.GetSubscriber();

                await subscriber.SubscribeAsync(
                    RedisChannel.Literal(TenantProvisionedEventMessage.Channel),
                    (channel, message) => _ = HandleMessageAsync(message, stoppingToken));

                _logger.LogInformation(
                    "Subscribed to '{Channel}' for eager tenant provisioning ({ContextType})",
                    TenantProvisionedEventMessage.Channel, typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "Failed to subscribe to '{Channel}' for eager tenant provisioning ({ContextType}) — " +
                    "retrying in {DelaySeconds}s (falling back to lazy per-request migration until this succeeds)",
                    TenantProvisionedEventMessage.Channel, typeof(TContext).Name, delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, MaxRetryDelay.TotalSeconds));
            }
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
