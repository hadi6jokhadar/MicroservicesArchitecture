using System.Text.Json;
using Category.Application.Events;
using Category.Domain.Entities;
using Category.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Category.Infrastructure.Services;

/// <summary>
/// Outbox-backed publisher. Instead of calling Redis directly, it queues the event
/// as an <see cref="OutboxEventEntity"/> row in the EF change tracker.
///
/// <b>Atomicity guarantee:</b> this method does NOT call <c>SaveChangesAsync</c>.
/// The caller must call <c>SaveChangesAsync</c> (via the repository) immediately after
/// so the outbox row is committed in the same DB transaction as the entity mutation.
///
/// The <see cref="OutboxEventProcessorService"/> background worker then picks up the
/// committed rows and delivers them to Redis Pub/Sub, providing at-least-once delivery
/// even when Redis is temporarily unavailable.
///
/// Registered as Scoped so it shares the same <see cref="CategoryDbContext"/> instance
/// as the command handler — both writes (entity + outbox) go through the same connection.
/// </summary>
public sealed class OutboxCategoryEventPublisher : ICategoryEventPublisher
{
    private readonly CategoryDbContext _dbContext;
    private readonly ILogger<OutboxCategoryEventPublisher> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OutboxCategoryEventPublisher(
        CategoryDbContext dbContext,
        ILogger<OutboxCategoryEventPublisher> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task PublishAsync(
        CategoryEntity entity,
        CategoryEventType eventType,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = tenantId ?? "global";
        var channel = $"{CategoryEventMessage.ChannelPrefix}:{tenantKey}";

        var message = new CategoryEventMessage
        {
            SchemaVersion    = CategoryEventMessage.CurrentSchemaVersion,
            EventType        = eventType,
            TenantId         = tenantId,
            Id               = entity.Id,
            Slug             = entity.Slug,
            Uri              = entity.Uri,
            ParentId         = entity.ParentId,
            Path             = entity.Path,
            Depth            = entity.Depth,
            IconName         = entity.IconName,
            IconFileId       = entity.IconFileId,
            ImageFileId      = entity.ImageFileId,
            NameTranslations = entity.NameTranslations.Translations
                                     .ToDictionary(kv => kv.Key, kv => kv.Value),
            OccurredAt       = DateTimeOffset.UtcNow
        };

        var payload = JsonSerializer.Serialize(message, SerializerOptions);

        var outboxEvent = new OutboxEventEntity
        {
            Channel   = channel,
            Payload   = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.OutboxEvents.Add(outboxEvent);

        // Do NOT call SaveChangesAsync here.
        // The caller (command handler) calls SaveChangesAsync via the repository
        // immediately after, committing the entity change and this outbox row
        // in the same database transaction — true atomicity.
        _logger.LogDebug(
            "Outbox event queued for {EventType} Category {Id} on channel {Channel}",
            eventType, entity.Id, channel);

        return Task.CompletedTask;
    }
}
