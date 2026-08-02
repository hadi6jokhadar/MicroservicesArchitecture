using System.Text.Json;
using IhsanDev.Shared.Infrastructure.Services.Tenant;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Publisher (Tenant Service) and consumer (every per-tenant service) sides of the
/// tenant-provisioned Redis Pub/Sub broadcast. See <see cref="TenantProvisionedEventMessage"/>
/// and <see cref="TenantProvisioningListenerService{TContext}"/> for the full rationale.
/// </summary>
public static class TenantProvisioningExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Registers the eager tenant-provisioning listener for <typeparamref name="TContext"/>.
    /// No-op when multi-tenancy or Redis is disabled — falls back to the existing lazy
    /// per-request migration (<see cref="MultiTenancyExtensions.UseTenantDatabaseMigration{TContext}"/>)
    /// and startup warm-up (<see cref="TenantWarmupExtensions"/>).
    /// Call alongside <see cref="MultiTenancyExtensions.AddMultiTenancy"/> and
    /// <see cref="DatabaseExtensions.AddDatabaseContext{TContext}"/> in Program.cs.
    /// </summary>
    public static IServiceCollection AddTenantProvisioningListener<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);
        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled", false);

        if (multiTenancyEnabled && redisEnabled)
        {
            services.AddHostedService<TenantProvisioningListenerService<TContext>>();
        }

        return services;
    }

    /// <summary>
    /// Best-effort broadcast that a new tenant was just created — called by the Tenant Service
    /// right after it caches the new tenant's configuration. Never throws: a missed publish
    /// (Redis down, no subscribers yet) just means consuming services fall back to their
    /// existing lazy per-request migration or next-startup warm-up, same as before this feature
    /// existed — so this is an optimization, not a delivery-guaranteed event, and does not need
    /// the transactional outbox pattern used for business-critical entity events (see
    /// EVENT_DRIVEN_PUBLISHER_PATTERN.md).
    /// </summary>
    public static async Task PublishTenantProvisionedAsync(
        this IConnectionMultiplexer? redis,
        string tenantId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (redis is null)
        {
            return;
        }

        try
        {
            var message = new TenantProvisionedEventMessage { TenantId = tenantId };
            var payload = JsonSerializer.Serialize(message, SerializerOptions);

            await redis.GetSubscriber().PublishAsync(
                RedisChannel.Literal(TenantProvisionedEventMessage.Channel),
                payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to publish tenant-provisioned event for tenant '{TenantId}' — " +
                "consuming services will still pick it up via their next startup warm-up or the tenant's first request",
                tenantId);
        }
    }
}
