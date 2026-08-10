using System.Text.Json;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Interfaces;
using StackExchange.Redis;

namespace Nasheed.Infrastructure.Services;

/// <summary>
/// Subscribes to <see cref="TenantConfigUpdatedEventMessage.Channel"/> and refreshes
/// <see cref="INasheedTenantCache"/> the moment Tenant Service saves a config/feature-flag change
/// for Nasheed's pinned tenant — this is the primary refresh path, replacing the wait for
/// <see cref="NasheedTenantLoaderService"/>'s periodic fallback loop. That fallback loop still
/// exists (at a much longer interval) purely for a missed message — Redis briefly disconnected,
/// this service restarting at the exact moment of the publish — same best-effort philosophy as
/// <c>TenantProvisioningListenerService&lt;TContext&gt;</c>. A failure here is logged and swallowed;
/// it never crashes the worker or blocks ingestion.
/// </summary>
public sealed class NasheedTenantConfigUpdatedListenerService : BackgroundService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INasheedTenantCache _tenantCache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NasheedTenantConfigUpdatedListenerService> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public NasheedTenantConfigUpdatedListenerService(
        IServiceScopeFactory scopeFactory,
        INasheedTenantCache tenantCache,
        IConfiguration configuration,
        ILogger<NasheedTenantConfigUpdatedListenerService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _tenantCache = tenantCache;
        _configuration = configuration;
        _logger = logger;
    }

    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_redis is null)
        {
            _logger.LogInformation(
                "Redis is disabled — tenant config changes will only be picked up by the periodic fallback refresh.");
            return;
        }

        // This is a pure optimization (see class summary) — a missed/failed subscription just
        // means config changes are only picked up by NasheedTenantLoaderService's periodic fallback
        // loop, exactly as if this listener didn't exist. So a subscribe failure must never escape
        // ExecuteAsync (fatal to the whole host under the default BackgroundServiceExceptionBehavior),
        // and it must not be given up on for the rest of the process's life either — retry with
        // backoff so a transient Redis hiccup self-heals instead of requiring a full restart.
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
                var subscriber = _redis!.GetSubscriber();

                await subscriber.SubscribeAsync(
                    RedisChannel.Literal(TenantConfigUpdatedEventMessage.Channel),
                    (channel, message) => _ = HandleMessageAsync(message, stoppingToken));

                _logger.LogInformation(
                    "Subscribed to '{Channel}' for live tenant config refresh.",
                    TenantConfigUpdatedEventMessage.Channel);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "Failed to subscribe to '{Channel}' for live tenant config refresh — retrying in " +
                    "{DelaySeconds}s (falling back to the periodic refresh loop until this succeeds)",
                    TenantConfigUpdatedEventMessage.Channel, delay.TotalSeconds);

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
        TenantConfigUpdatedEventMessage? evt;
        try
        {
            evt = JsonSerializer.Deserialize<TenantConfigUpdatedEventMessage>((string?)message ?? string.Empty, SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize tenant-config-updated event payload.");
            return;
        }

        if (evt is null || string.IsNullOrWhiteSpace(evt.TenantId) ||
            evt.SchemaVersion != TenantConfigUpdatedEventMessage.CurrentSchemaVersion)
        {
            return;
        }

        // Nasheed is single-tenant per deployment — ignore updates for any other tenant.
        var pinnedTenantId = _configuration["MultiTenancy:TenantId"];
        if (!string.Equals(evt.TenantId, pinnedTenantId, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantConfigurationProvider>();
            var tenant = await tenantProvider.GetTenantConfigurationAsync(evt.TenantId, cancellationToken);

            if (tenant != null)
            {
                _tenantCache.SetTenant(tenant);
                _logger.LogInformation(
                    "Refreshed tenant '{TenantId}' configuration in response to a tenant:updated event.",
                    evt.TenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to refresh tenant '{TenantId}' configuration after a tenant:updated event.",
                evt.TenantId);
        }
    }
}
