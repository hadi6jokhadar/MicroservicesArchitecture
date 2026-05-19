using System.Text.Json;
using Category.Application.Events;
using Category.Domain.Entities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Category.Infrastructure.Services;

/// <summary>
/// Publishes category mutation events to a Redis Pub/Sub channel.
/// Channel pattern: "category:events:{tenantId}" (or "category:events:global" for null tenant).
///
/// Consumers subscribe to this channel and upsert/delete their local snapshot.
/// Failures are swallowed — the mutation itself is never rolled back because of a publish failure.
/// </summary>
public sealed class RedisCategoryEventPublisher : ICategoryEventPublisher
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisCategoryEventPublisher> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCategoryEventPublisher(
        IConnectionMultiplexer multiplexer,
        ILogger<RedisCategoryEventPublisher> logger)
    {
        _multiplexer = multiplexer;
        _logger = logger;
    }

    public async Task PublishAsync(
        CategoryEntity entity,
        CategoryEventType eventType,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantKey = tenantId ?? "global";
            var channel = new RedisChannel(
                $"{CategoryEventMessage.ChannelPrefix}:{tenantKey}",
                RedisChannel.PatternMode.Literal);

            var message = new CategoryEventMessage
            {
                SchemaVersion    = CategoryEventMessage.CurrentSchemaVersion,
                EventType    = eventType,
                TenantId     = tenantId,
                Id           = entity.Id,
                Slug         = entity.Slug,
                Uri          = entity.Uri,
                ParentId     = entity.ParentId,
                Path         = entity.Path,
                Depth        = entity.Depth,
                IconName     = entity.IconName,
                IconFileId   = entity.IconFileId,
                ImageFileId  = entity.ImageFileId,
                NameTranslations = entity.NameTranslations.Translations.ToDictionary(kv => kv.Key, kv => kv.Value),
                OccurredAt   = DateTimeOffset.UtcNow
            };

            var json = JsonSerializer.Serialize(message, SerializerOptions);

            var sub = _multiplexer.GetSubscriber();
            await sub.PublishAsync(channel, json);

            _logger.LogDebug(
                "Published {EventType} for Category {Id} on channel {Channel}",
                eventType, entity.Id, channel);
        }
        catch (Exception ex)
        {
            // Never let a publish failure roll back the primary mutation.
            _logger.LogWarning(ex,
                "Failed to publish {EventType} for Category {Id}. Consumer snapshots may be stale.",
                eventType, entity.Id);
        }
    }
}
