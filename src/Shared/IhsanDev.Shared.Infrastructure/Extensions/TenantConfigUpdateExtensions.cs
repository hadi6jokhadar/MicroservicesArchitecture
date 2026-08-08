using System.Text.Json;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IhsanDev.Shared.Infrastructure.Extensions;

/// <summary>
/// Publisher side of the tenant-config-updated Redis Pub/Sub broadcast — called by Tenant Service
/// wherever it already invalidates the <c>tenant_config_{tenantId}</c> cache key (update, archive
/// toggle, delete). See <see cref="TenantConfigUpdatedEventMessage"/> for the consumer-side contract.
/// </summary>
public static class TenantConfigUpdateExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Best-effort broadcast that a tenant's configuration just changed — called by Tenant Service
    /// right after it invalidates that tenant's cache. Never throws: a missed publish (Redis down,
    /// no subscribers) just means consuming services fall back to their own periodic refresh, same
    /// as before this feature existed — this is an optimization, not a delivery-guaranteed event.
    /// </summary>
    public static async Task PublishTenantConfigUpdatedAsync(
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
            var message = new TenantConfigUpdatedEventMessage { TenantId = tenantId };
            var payload = JsonSerializer.Serialize(message, SerializerOptions);

            await redis.GetSubscriber().PublishAsync(
                RedisChannel.Literal(TenantConfigUpdatedEventMessage.Channel),
                payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to publish tenant-config-updated event for tenant '{TenantId}' — " +
                "consuming services will still pick up the change via their own periodic fallback refresh",
                tenantId);
        }
    }
}
