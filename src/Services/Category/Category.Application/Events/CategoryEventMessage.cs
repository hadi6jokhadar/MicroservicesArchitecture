namespace Category.Application.Events;

/// <summary>
/// The type of mutation that triggered this event.
/// </summary>
public enum CategoryEventType
{
    Created,
    Updated,
    Deleted
}

/// <summary>
/// Slim, serializable payload published to Redis Pub/Sub after every category mutation.
/// Consumer services persist this as a local read-only snapshot.
/// </summary>
public record CategoryEventMessage
{
    /// <summary>Redis Pub/Sub channel name format: "category:events:{tenantId}".</summary>
    public const string ChannelPrefix = "category:events";

    /// <summary>
    /// Bump this constant when the message shape changes in a breaking way.
    /// Consumers must check this value and skip or migrate messages with unknown versions.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Schema version of this message. Used by consumers to handle future breaking changes.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public CategoryEventType EventType { get; init; }

    /// <summary>Tenant that owns this category. Null = global (no x-tenant-id header).</summary>
    public string? TenantId { get; init; }

    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Uri { get; init; } = string.Empty;
    public int? ParentId { get; init; }
    public string Path { get; init; } = "/";
    public int Depth { get; init; }
    public string? IconName { get; init; }
    public int? IconFileId { get; init; }
    public int? ImageFileId { get; init; }

    /// <summary>Locale → display name. e.g. {"en": "Music", "ar": "موسيقى"}</summary>
    public Dictionary<string, string> NameTranslations { get; init; } = new();

    /// <summary>UTC timestamp of the mutation.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
