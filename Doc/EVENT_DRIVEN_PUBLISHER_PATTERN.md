# Event-Driven Publisher Pattern

**Last Updated:** May 19, 2026  
**Status:** ✅ Production Ready  
**Reference Implementation:** Category service  
**Applies to:** Any microservice that needs to broadcast state changes to other services via Redis Pub/Sub.

---

## Overview

This guide documents the **publisher side** of Strategy 2 (Event-Driven Local Copy). When any service mutates data that other services care about, it publishes a slim Redis Pub/Sub event. Consumer services subscribe, maintain a local snapshot table, and query it directly — zero runtime coupling.

```
Your Service ──publishes──▶ Redis Pub/Sub channel ──▶ Consumer Services
  (Create/Update/Delete)        "{name}:events:{tenantId}"    (local snapshot tables)
```

> **Consumer side:** See `CATEGORY_EVENT_DRIVEN_CONSUMER_GUIDE.md` for the full consumer implementation guide. That guide is Category-specific in naming but its pattern is identical for any event source.

---

## Architecture at a Glance

| Layer                                       | What you add                                                                  | Why                                                                                             |
| ------------------------------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `{Name}.Application/Events/`                | `{Name}EventMessage.cs`                                                       | Serializable payload record (includes `SchemaVersion`)                                          |
| `{Name}.Application/Events/`                | `{Name}EventType.cs` _(or enum in message file)_                              | Created / Updated / Deleted                                                                     |
| `{Name}.Application/Events/`                | `I{Name}EventPublisher.cs`                                                    | Interface — handlers depend on this                                                             |
| `{Name}.Domain/Entities/`                   | `OutboxEventEntity.cs`                                                        | DB row for the outbox table                                                                     |
| `{Name}.Infrastructure/Configurations/`     | `OutboxEventEntityConfiguration.cs`                                           | EF table/column/index config                                                                    |
| `{Name}.Infrastructure/Persistence/`        | `{Name}DbContext.cs`                                                          | Add `DbSet<OutboxEventEntity>`                                                                  |
| `{Name}.Infrastructure/Services/`           | `Outbox{Name}EventPublisher.cs`                                               | **Scoped** — writes to outbox table (not Redis directly)                                        |
| `{Name}.Infrastructure/BackgroundServices/` | `OutboxEventProcessorService.cs`                                              | BackgroundService — polls outbox, publishes to Redis                                            |
| `{Name}.Infrastructure/Services/`           | `NoOp{Name}EventPublisher.cs`                                                 | Local dev fallback (Redis disabled)                                                             |
| `{Name}.Infrastructure/Extensions/`         | `InfrastructureServiceExtensions.cs`                                          | Conditional DI registration                                                                     |
| Every mutation handler                      | Call `_eventPublisher.PublishAsync(...)` **before** the final repository save | Queues outbox row in EF change tracker; repository's `SaveChangesAsync` commits both atomically |

> **Why Outbox?** Without the outbox, if Redis is temporarily unavailable the event is silently dropped and consumer snapshots become stale forever. The publisher queues the outbox row in the EF change tracker; the handler calls `_eventPublisher.PublishAsync(...)` **before** the final `repository.UpdateAsync/DeleteAsync` so both the entity change and the outbox row are committed in the same `SaveChangesAsync` call — **at-least-once delivery** even when Redis is down.

---

## Step 1 — Event Message Record

Create `{Name}.Application/Events/{Name}EventMessage.cs`:

```csharp
namespace YourService.Application.Events;

public enum YourEntityEventType
{
    Created,
    Updated,
    Deleted
}

/// <summary>
/// Slim, serializable payload published to Redis Pub/Sub after every {entity} mutation.
/// Consumer services persist this as a local read-only snapshot.
/// </summary>
public record YourEntityEventMessage
{
    /// <summary>Redis channel prefix. Full channel: "{prefix}:{tenantId}".</summary>
    public const string ChannelPrefix = "yourentity:events";

    /// <summary>Bump this when adding/removing/renaming fields. Consumers skip unknown versions.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public YourEntityEventType EventType { get; init; }

    /// <summary>Null = global (no x-tenant-id header).</summary>
    public string? TenantId { get; init; }

    // ── Required identity fields ─────────────────────────────────────────────
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;

    // ── Include only what consumers need to store locally ────────────────────
    // Example: localized names
    public Dictionary<string, string> NameTranslations { get; init; } = new();

    // Add your domain-specific fields here...

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

**Rules for the message payload:**

- Include only fields that consumers need to store in their local snapshot table
- **Exclude** resolved URLs (file URIs, CDN links) — keep the payload small and transport-independent
- Use primitive types only (`string`, `int`, `bool`, `DateTimeOffset`, `Dictionary<string,string>`)
- The JSON serializer uses **camelCase** — keep property names PascalCase in C#

---

## Step 2 — Publisher Interface

Create `{Name}.Application/Events/I{Name}EventPublisher.cs`:

```csharp
using YourService.Domain.Entities;

namespace YourService.Application.Events;

/// <summary>
/// Publishes entity change events so consumer services can maintain
/// a local read-only snapshot without calling this service at runtime.
/// </summary>
public interface IYourEntityEventPublisher
{
    /// <summary>
    /// Queues an outbox event in the EF change tracker for the given entity.
    /// The caller must call the repository's final save method immediately after
    /// so the outbox row and the entity mutation are committed in the same transaction.
    /// </summary>
    Task PublishAsync(
        YourEntityEntity entity,
        YourEntityEventType eventType,
        string? tenantId,
        CancellationToken cancellationToken = default);
}
```

> **Why in Application layer?** Handlers (also in Application) inject this interface. The concrete implementation lives in Infrastructure — Application never references Infrastructure.

---

## Step 3 — Outbox Publisher + Background Processor

The direct Redis publisher (write to Redis inside the handler) has a reliability gap: if Redis is down when the handler runs, the event is silently lost. The **Transactional Outbox** pattern fixes this.

### 3a — OutboxEventEntity (Domain)

Create `{Name}.Domain/Entities/OutboxEventEntity.cs`:

```csharp
namespace YourService.Domain.Entities;

/// <summary>
/// Represents a pending Pub/Sub event persisted inside the same DB transaction as the entity save.
/// A background worker reads rows WHERE ProcessedAt IS NULL and publishes to Redis.
/// </summary>
public class OutboxEventEntity
{
    public long Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;     // JSON
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }        // null = pending
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
```

### 3b — EF Configuration (Infrastructure)

Create `{Name}.Infrastructure/Persistence/Configurations/OutboxEventEntityConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YourService.Domain.Entities;

namespace YourService.Infrastructure.Persistence.Configurations;

public class OutboxEventEntityConfiguration : IEntityTypeConfiguration<OutboxEventEntity>
{
    public void Configure(EntityTypeBuilder<OutboxEventEntity> builder)
    {
        builder.ToTable("yourentity_outbox_events");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.Channel).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);

        builder.HasIndex(x => x.ProcessedAt)
               .HasDatabaseName("ix_yourentity_outbox_events_processed_at");
        builder.HasIndex(x => x.CreatedAt)
               .HasDatabaseName("ix_yourentity_outbox_events_created_at");
    }
}
```

Add `DbSet<OutboxEventEntity> OutboxEvents` to your `{Name}DbContext`.

### 3c — Scoped Outbox Publisher (Infrastructure)

Create `{Name}.Infrastructure/Services/Outbox{Name}EventPublisher.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging;
using YourService.Application.Events;
using YourService.Domain.Entities;
using YourService.Infrastructure.Persistence;

namespace YourService.Infrastructure.Services;

/// <summary>
/// Scoped publisher: writes the event to the outbox table inside the same
/// EF Core Unit of Work as the entity save. A background worker publishes to Redis.
/// </summary>
public sealed class OutboxYourEntityEventPublisher : IYourEntityEventPublisher
{
    private readonly YourServiceDbContext _dbContext;
    private readonly ILogger<OutboxYourEntityEventPublisher> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OutboxYourEntityEventPublisher(
        YourServiceDbContext dbContext,
        ILogger<OutboxYourEntityEventPublisher> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task PublishAsync(
        YourEntityEntity entity,
        YourEntityEventType eventType,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = tenantId ?? "global";
        var channel = $"{YourEntityEventMessage.ChannelPrefix}:{tenantKey}";

        var message = new YourEntityEventMessage
        {
            SchemaVersion    = YourEntityEventMessage.CurrentSchemaVersion,
            EventType        = eventType,
            TenantId         = tenantId,
            Id               = entity.Id,
            Slug             = entity.Slug,
            NameTranslations = entity.NameTranslations.Translations
                                     .ToDictionary(kv => kv.Key, kv => kv.Value),
            OccurredAt       = DateTimeOffset.UtcNow
        };

        var payload = JsonSerializer.Serialize(message, SerializerOptions);

        _dbContext.OutboxEvents.Add(new OutboxEventEntity
        {
            Channel = channel,
            Payload = payload
        });

        // Do NOT call SaveChangesAsync here.
        // The handler calls PublishAsync BEFORE the final repository save so that
        // the entity mutation and this outbox row are committed in the same transaction.
        _logger.LogDebug(
            "Outbox event queued for {EventType} {Entity} {Id} on channel {Channel}",
            eventType, nameof(YourEntityEntity), entity.Id, channel);

        return Task.CompletedTask;
    }
}
```

> **Scoped** — depends on `DbContext`. Register as `Scoped`, not `Singleton`.
> **Important** — `PublishAsync` only queues the row in the EF change tracker. The handler must call `_eventPublisher.PublishAsync(...)` **before** the final `repository.UpdateAsync/DeleteAsync` call so both are committed atomically.

### 3d — Background Processor (Infrastructure)

Create `{Name}.Infrastructure/BackgroundServices/OutboxEventProcessorService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using YourService.Infrastructure.Persistence;

namespace YourService.Infrastructure.BackgroundServices;

/// <summary>
/// Singleton BackgroundService: polls the outbox table and publishes pending rows to Redis.
/// Uses IServiceScopeFactory to safely resolve the Scoped DbContext per poll cycle.
/// </summary>
public sealed class OutboxEventProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OutboxEventProcessorService> _logger;
    private readonly TimeSpan _pollInterval;

    private const int BatchSize = 100;
    private const int MaxRetries = 5;

    public OutboxEventProcessorService(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        ILogger<OutboxEventProcessorService> logger,
        TimeSpan? pollInterval = null)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<YourServiceDbContext>();

        var pending = await db.OutboxEvents
            .Where(e => e.ProcessedAt == null && e.RetryCount < MaxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        var sub = _redis.GetSubscriber();

        foreach (var row in pending)
        {
            try
            {
                await sub.PublishAsync(
                    new RedisChannel(row.Channel, RedisChannel.PatternMode.Literal),
                    row.Payload);
                row.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                row.RetryCount++;
                row.LastError = ex.Message;
                if (row.RetryCount >= MaxRetries)
                {
                    row.ProcessedAt = DateTimeOffset.UtcNow; // dead-letter
                    _logger.LogError(ex,
                        "Outbox event {Id} on channel {Channel} exhausted retries and is dead-lettered.",
                        row.Id, row.Channel);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Outbox event {Id} failed (attempt {Attempt}/{Max}).",
                        row.Id, row.RetryCount, MaxRetries);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
```

### 3e — Create EF migration

```powershell
cd "MicroservicesArchitecture"
dotnet ef migrations add AddOutboxEvents `
    --project "src/Services/{Name}/{Name}.Infrastructure" `
    --startup-project "src/Services/{Name}/{Name}.API"
```

---

**Key rule:** Map `LocalizedMapping` via `.Translations.ToDictionary(kv => kv.Key, kv => kv.Value)` — **not** `.ToDictionary()` directly (that method does not exist on `LocalizedMapping`).

---

## Step 4 — No-Op Fallback

Create `{Name}.Infrastructure/Services/NoOp{Name}EventPublisher.cs`:

```csharp
using Microsoft.Extensions.Logging;
using YourService.Application.Events;
using YourService.Domain.Entities;

namespace YourService.Infrastructure.Services;

/// <summary>
/// No-op publisher used when Redis is disabled (local dev / in-memory cache mode).
/// Events are logged at Debug level and dropped.
/// </summary>
public sealed class NoOpYourEntityEventPublisher : IYourEntityEventPublisher
{
    private readonly ILogger<NoOpYourEntityEventPublisher> _logger;

    public NoOpYourEntityEventPublisher(ILogger<NoOpYourEntityEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(
        YourEntityEntity entity,
        YourEntityEventType eventType,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[NoOp] {Entity} event {EventType} for Id {Id} (tenant: {TenantId}) not published — Redis is disabled.",
            nameof(YourEntityEntity), eventType, entity.Id, tenantId ?? "global");
        return Task.CompletedTask;
    }
}
```

---

## Step 5 — Register in DI

In `{Name}.Infrastructure/Extensions/InfrastructureServiceExtensions.cs`, add the conditional registration **after** your cache setup:

```csharp
// Add these usings at the top:
using YourService.Application.Events;
using YourService.Infrastructure.Services;
using YourService.Infrastructure.BackgroundServices;

// Inside AddInfrastructureServices():

// Event publisher — Outbox pattern (writes to DB, background worker publishes to Redis).
// Falls back to no-op when Redis is disabled so local dev still works.
var redisEnabled = configuration.GetValue<bool>("Redis:Enabled", false);
if (redisEnabled)
{
    services.AddScoped<IYourEntityEventPublisher, OutboxYourEntityEventPublisher>();
    services.AddHostedService<OutboxEventProcessorService>();
}
else
    services.AddScoped<IYourEntityEventPublisher, NoOpYourEntityEventPublisher>();
```

> **Why Scoped?** `OutboxYourEntityEventPublisher` depends on `YourServiceDbContext` which is Scoped.  
> `NoOpYourEntityEventPublisher` is also registered as Scoped for symmetry.

---

## Step 6 — Call from Mutation Handlers

In every handler that mutates the entity, inject the publisher and call it **after** cache invalidation (so the cache is already fresh if a consumer calls back):

```csharp
// 1. Add field and constructor parameter:
private readonly IYourEntityEventPublisher _eventPublisher;

// 2. In the constructor:
_eventPublisher = eventPublisher;

// 3. BEFORE the final repository save — queue outbox, then save entity + outbox atomically:
await _eventPublisher.PublishAsync(
    entity,
    YourEntityEventType.Created,   // or Updated / Deleted
    _tenantContext.TenantId,
    cancellationToken);
await _repository.UpdateAsync(entity, cancellationToken);  // commits entity + outbox together
```

**Pattern for each operation:**

| Handler                 | Event type                                             |
| ----------------------- | ------------------------------------------------------ |
| CreateXxxCommandHandler | `Created`                                              |
| UpdateXxxCommandHandler | `Updated`                                              |
| DeleteXxxCommandHandler | `Deleted`                                              |
| Move/Reorder/Archive    | `Updated` _(use Updated unless you add a custom type)_ |

**Order within Handle():**

```
1. Business logic / entity modification
2. EventPublisher.PublishAsync(...)   ← queue outbox row BEFORE final save
3. Repository save (UpdateAsync / DeleteAsync)  ← commits entity + outbox atomically
4. Downstream calls (file manager, etc.)
5. Cache invalidation
6. Return DTO
```

> **Exception — Create handlers:** `AddAsync` must be called first to get the database-assigned `Id` (needed for path calculation). Only the **final** `UpdateAsync` (path update) needs to be preceded by `PublishAsync`.

---

## Step 7 — Add usings to each handler

Each handler file needs:

```csharp
using YourService.Application.Events;
```

If the handler didn't already have `ITenantContext`, inject it too:

```csharp
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
```

---

## Naming Conventions

Replace the following placeholders throughout all files:

| Placeholder                 | Example (Category)        | Your service               |
| --------------------------- | ------------------------- | -------------------------- |
| `YourService`               | `Category`                | e.g. `Item`, `Product`     |
| `YourEntity`                | `Category`                | e.g. `Item`, `Product`     |
| `YourEntityEntity`          | `CategoryEntity`          | e.g. `ItemEntity`          |
| `yourentity:events`         | `category:events`         | e.g. `item:events`         |
| `YourEntityEventMessage`    | `CategoryEventMessage`    | e.g. `ItemEventMessage`    |
| `IYourEntityEventPublisher` | `ICategoryEventPublisher` | e.g. `IItemEventPublisher` |

---

## File Checklist

```
{Name}.Application/
└── Events/
    ├── {Name}EventMessage.cs            ← event payload + channel prefix + SchemaVersion constant
    └── I{Name}EventPublisher.cs         ← interface

{Name}.Domain/
└── Entities/
    └── OutboxEventEntity.cs             ← DB row for outbox table

{Name}.Infrastructure/
├── Persistence/
│   ├── {Name}DbContext.cs               ← MODIFIED: add DbSet<OutboxEventEntity>
│   └── Configurations/
│       └── OutboxEventEntityConfiguration.cs
├── Services/
│   ├── Outbox{Name}EventPublisher.cs    ← Scoped: writes to outbox table
│   └── NoOp{Name}EventPublisher.cs      ← local dev fallback
├── BackgroundServices/
│   └── OutboxEventProcessorService.cs   ← Singleton: polls outbox → publishes to Redis
└── Extensions/
    └── InfrastructureServiceExtensions.cs  ← MODIFIED: conditional DI registration

{Name}.Application/Handlers/
├── Create{Name}/Create{Name}CommandHandler.cs  ← MODIFIED: inject + call
├── Update{Name}/Update{Name}CommandHandler.cs  ← MODIFIED: inject + call
└── Delete{Name}/Delete{Name}CommandHandler.cs  ← MODIFIED: inject + call
```

---

## Build Verification

After completing all steps, run:

```powershell
cd "MicroservicesArchitecture"
dotnet build "src/Services/{Name}/{Name}.API/{Name}.API.csproj" --no-restore -v q
```

Expected output: `Build succeeded. 0 Error(s). 0 Warning(s)`

---

## Common Mistakes

| Mistake                                                    | Fix                                                                                                                                                                                                                    |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `entity.NameTranslations.ToDictionary()` compile error     | Use `.Translations.ToDictionary(kv => kv.Key, kv => kv.Value)` — `.ToDictionary()` does not exist on `LocalizedMapping`                                                                                                |
| Publisher registered as `Singleton`                        | Must be **Scoped** — `OutboxXxxEventPublisher` depends on `DbContext` (Scoped)                                                                                                                                         |
| Calling `PublishAsync` **after** the final repository save | Call `PublishAsync` **before** the final repository save — the outbox row must be in the EF change tracker so both the entity mutation and the outbox row are committed atomically in the same `SaveChangesAsync` call |
| Throwing from the publisher                                | Wrap entire body in `try/catch` — never let a publish failure surface to the caller                                                                                                                                    |
| Moving `IXxxEventPublisher` interface to Infrastructure    | Keep it in **Application** — handlers depend on it and Application must not reference Infrastructure                                                                                                                   |
| Forgetting to run the EF migration                         | Run `dotnet ef migrations add AddOutboxEvents` after adding `OutboxEventEntity`                                                                                                                                        |
| No `SchemaVersion` in message                              | Always set `SchemaVersion = YourEntityEventMessage.CurrentSchemaVersion` — consumers need it to guard against future changes                                                                                           |

---

## Related Documentation

| File                                      | Purpose                                                                                  |
| ----------------------------------------- | ---------------------------------------------------------------------------------------- |
| `CATEGORY_EVENT_DRIVEN_CONSUMER_GUIDE.md` | How a **consumer** service subscribes to these events and stores local snapshots         |
| `CACHING_STRATEGY_COMPARISON.md`          | Full comparison of all three strategies (HTTP call, event-driven snapshot, shared cache) |
| `CATEGORY_SERVICE_GUIDE.md`               | Reference implementation of this pattern in production                                   |
