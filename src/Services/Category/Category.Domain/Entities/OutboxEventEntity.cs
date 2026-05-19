namespace Category.Domain.Entities;

/// <summary>
/// Persisted record of a pending category event.
/// Written atomically alongside (or immediately after) the entity mutation.
/// The <see cref="OutboxEventProcessorService"/> reads unprocessed rows and
/// publishes them to Redis Pub/Sub, then marks them as delivered.
/// </summary>
public class OutboxEventEntity
{
    /// <summary>Auto-generated surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Full Redis Pub/Sub channel (e.g. "category:events:tenant-abc").</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>Serialized <c>CategoryEventMessage</c> JSON (camelCase).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>UTC time the event was written to the outbox.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC time the event was successfully published. Null = not yet delivered.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>How many publish attempts have been made (including failed ones).</summary>
    public int RetryCount { get; set; }

    /// <summary>Message from the last failed publish attempt, if any.</summary>
    public string? LastError { get; set; }
}
