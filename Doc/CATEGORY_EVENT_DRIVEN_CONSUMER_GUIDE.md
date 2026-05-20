# Category Event-Driven Consumer Guide

**Last Updated:** May 19, 2026  
**Status:** ✅ Production Ready  
**Applies to:** Any service that needs a list or single item from the Category service without calling it at runtime.

---

## Overview

The Category service publishes a **Redis Pub/Sub event** after every mutation (Create, Update, Delete, Move). Consumer services subscribe to this channel on startup, maintain a **local read-only snapshot** in their own database, and query it directly — zero network hops, zero dependency on Category service availability at runtime.

```
Category Service ──publishes──▶ Redis Pub/Sub ──▶ Your Service
   (Create/Update/Delete/Move)      channel          (local snapshot table)
                                                          ▲
                                                          │ local DB JOIN
                                                    Item / Song / etc.
```

---

## Redis Channel

| Pattern                      | Example                                  |
| ---------------------------- | ---------------------------------------- |
| `category:events:{tenantId}` | `category:events:tenant-abc`             |
| `category:events:global`     | When no `x-tenant-id` header was present |

One channel per tenant. Subscribe to the channel(s) that match your service's tenants.

---

## Event Payload

```csharp
public record CategoryEventMessage
{
    public int SchemaVersion { get; init; }            // always check — skip unknown versions
    public CategoryEventType EventType { get; init; }  // Created | Updated | Deleted
    public string? TenantId { get; init; }             // null = global
    public int Id { get; init; }
    public string Slug { get; init; }
    public string Uri { get; init; }
    public int? ParentId { get; init; }
    public string Path { get; init; }                  // materialized path e.g. "/electronics/phones/"
    public int Depth { get; init; }                    // 0 = root
    public string? IconName { get; init; }
    public int? IconFileId { get; init; }
    public int? ImageFileId { get; init; }
    public Dictionary<string, string> NameTranslations { get; init; }  // {"en":"Music","ar":"موسيقى"}
    public DateTimeOffset OccurredAt { get; init; }
}
```

The JSON payload uses **camelCase** property names.

> **Current `SchemaVersion`:** `1`. The consumer must check this and skip (log + discard) any event whose version is not supported.

> **Note:** `IconFile` and `ImageFile` (resolved URLs) are intentionally excluded from events to keep the payload small and dependency-free. Resolve file URLs in your own service using File Manager if needed.

---

## Step-by-Step Implementation

### 1 — Add a `CategorySnapshot` entity to your domain

Create a read-only entity in your `{Service}.Domain/Entities/` folder:

```csharp
// {Service}.Domain/Entities/CategorySnapshotEntity.cs
namespace YourService.Domain.Entities;

/// <summary>
/// Read-only local copy of a Category, kept in sync by the category event subscriber.
/// Never write to this table directly — it is managed by CategorySnapshotSyncService.
/// </summary>
public class CategorySnapshotEntity
{
    public int Id { get; set; }              // same Id as Category service
    public string? TenantId { get; set; }   // null = global
    public string Slug { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string Path { get; set; } = "/";
    public int Depth { get; set; }
    public string? IconName { get; set; }
    public int? IconFileId { get; set; }
    public int? ImageFileId { get; set; }

    /// <summary>Stored as JSONB. e.g. {"en":"Music","ar":"موسيقى"}</summary>
    public Dictionary<string, string> NameTranslations { get; set; } = new();

    public DateTimeOffset LastSyncedAt { get; set; }
}
```

---

### 2 — Add to DbContext and create migration

```csharp
// In your {Service}DbContext:
public DbSet<CategorySnapshotEntity> CategorySnapshots => Set<CategorySnapshotEntity>();

protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    builder.Entity<CategorySnapshotEntity>(e =>
    {
        e.HasKey(x => new { x.Id, x.TenantId });   // composite PK — Id is NOT unique across tenants

        e.Property(x => x.TenantId).HasMaxLength(100);
        e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        e.Property(x => x.Uri).HasMaxLength(500).IsRequired();
        e.Property(x => x.Path).HasMaxLength(2000);

        e.Property(x => x.NameTranslations)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());

        e.HasIndex(x => new { x.TenantId, x.Slug });
        e.HasIndex(x => new { x.TenantId, x.Path });
    });
}
```

Then run:

```powershell
cd "src/Apps/YourService/YourService.Infrastructure"
dotnet ef migrations add AddCategorySnapshots
dotnet ef database update
```

---

### 3 — Add the background subscriber service

Create `{Service}.Infrastructure/BackgroundServices/CategorySnapshotSyncService.cs`:

```csharp
using System.Text.Json;
using Category.Application.Events;          // shared payload — add project reference or copy the record
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using YourService.Domain.Entities;
using YourService.Infrastructure.Persistence;

namespace YourService.Infrastructure.BackgroundServices;

/// <summary>
/// Subscribes to Category service Redis Pub/Sub events and keeps the local
/// CategorySnapshots table in sync. Runs as a singleton hosted service.
/// </summary>
public sealed class CategorySnapshotSyncService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CategorySnapshotSyncService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CategorySnapshotSyncService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<CategorySnapshotSyncService> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = _redis.GetSubscriber();

        // Subscribe to ALL tenant channels using a pattern
        await sub.SubscribeAsync(
            new RedisChannel($"{CategoryEventMessage.ChannelPrefix}:*", RedisChannel.PatternMode.Pattern),
            (_, message) => _ = HandleMessageAsync(message!, stoppingToken));

        _logger.LogInformation("CategorySnapshotSyncService subscribed to category:events:*");

        // Keep running until the host stops
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleMessageAsync(string json, CancellationToken ct)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<CategoryEventMessage>(json, JsonOpts);
            if (msg is null) return;

            // ✅ Version guard — skip unknown/future schema versions
            const int SupportedSchemaVersion = 1;
            if (msg.SchemaVersion != SupportedSchemaVersion)
            {
                _logger.LogWarning(
                    "Skipping CategoryEventMessage with unsupported SchemaVersion {Version} (supported: {Supported})",
                    msg.SchemaVersion, SupportedSchemaVersion);
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<YourServiceDbContext>();

            switch (msg.EventType)
            {
                case CategoryEventType.Created:
                case CategoryEventType.Updated:
                    await UpsertAsync(db, msg, ct);
                    break;

                case CategoryEventType.Deleted:
                    await DeleteAsync(db, msg, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing category event: {Json}", json);
        }
    }

    private static async Task UpsertAsync(YourServiceDbContext db, CategoryEventMessage msg, CancellationToken ct)
    {
        var existing = await db.CategorySnapshots
            .FindAsync(new object?[] { msg.Id, msg.TenantId }, ct);

        if (existing is null)
        {
            db.CategorySnapshots.Add(new CategorySnapshotEntity
            {
                Id               = msg.Id,
                TenantId         = msg.TenantId,
                Slug             = msg.Slug,
                Uri              = msg.Uri,
                ParentId         = msg.ParentId,
                Path             = msg.Path,
                Depth            = msg.Depth,
                IconName         = msg.IconName,
                IconFileId       = msg.IconFileId,
                ImageFileId      = msg.ImageFileId,
                NameTranslations = msg.NameTranslations,
                LastSyncedAt     = msg.OccurredAt
            });
        }
        else
        {
            // ✅ Staleness guard — backfill and subscriber start concurrently;
            //    don't let a bulk-seed row silently overwrite a newer live event.
            if (existing.LastSyncedAt >= msg.OccurredAt) return;

            existing.Slug             = msg.Slug;
            existing.Uri              = msg.Uri;
            existing.ParentId         = msg.ParentId;
            existing.Path             = msg.Path;
            existing.Depth            = msg.Depth;
            existing.IconName         = msg.IconName;
            existing.IconFileId       = msg.IconFileId;
            existing.ImageFileId      = msg.ImageFileId;
            existing.NameTranslations = msg.NameTranslations;
            existing.LastSyncedAt     = msg.OccurredAt;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task DeleteAsync(YourServiceDbContext db, CategoryEventMessage msg, CancellationToken ct)
    {
        var existing = await db.CategorySnapshots
            .FindAsync(new object?[] { msg.Id, msg.TenantId }, ct);

        if (existing is not null)
        {
            db.CategorySnapshots.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

---

### 4 — Register the background service

In your `{Service}.Infrastructure/Extensions/InfrastructureServiceExtensions.cs`:

```csharp
// Only register when Redis is enabled — in-memory dev mode has no pub/sub
var redisEnabled = configuration.GetValue<bool>("Redis:Enabled", false);
if (redisEnabled)
{
    services.AddHostedService<CategorySnapshotSyncService>();
}
```

---

### 5 — Reference the shared event payload

Two options:

**Option A — Project reference (recommended for services in this solution):**

Add to your `{Service}.Infrastructure.csproj`:

```xml
<ProjectReference Include="..\..\..\..\Services\Category\Category.Application\Category.Application.csproj" />
```

**Option B — Copy the record (for isolated services):**

Copy `CategoryEventMessage.cs` and `CategoryEventType.cs` into your project and keep the namespace identical. Do **not** add a project reference in this case.

---

### 6 — Use the snapshot in your queries

Instead of calling the Category HTTP API, JOIN or filter against the local table:

```csharp
// Example: get songs with their category names
var results = await _db.Songs
    .Join(_db.CategorySnapshots.Where(c => c.TenantId == tenantId),
          s => s.CategoryId,
          c => c.Id,
          (s, c) => new SongWithCategoryDto
          {
              SongId       = s.Id,
              SongTitle    = s.Title,
              CategorySlug = c.Slug,
              CategoryName = c.NameTranslations.GetValueOrDefault("en") ?? c.Slug
          })
    .ToListAsync(ct);
```

Or as a navigation property if you prefer EF relationships:

```csharp
// In your Song entity / configuration:
public int CategoryId { get; set; }
public CategorySnapshotEntity? Category { get; set; }

// EF config:
builder.HasOne(s => s.Category)
       .WithMany()
       .HasForeignKey(s => s.CategoryId)
       .HasPrincipalKey(c => c.Id)  // local FK — no cross-DB join
       .OnDelete(DeleteBehavior.Restrict);
```

---

## Startup Sync (Snapshot Seeding)

When you first deploy a service that uses this pattern, the `CategorySnapshots` table will be empty. Rather than calling the old public Category API, use the **internal snapshot endpoint** which is secured with a service-to-service key:

> **Where is this endpoint defined?** `Category.API/Endpoints/CategoryInternalEndpoints.cs` — `MapCategoryInternalEndpoints()`. See `EVENT_DRIVEN_PUBLISHER_PATTERN.md` for the full publisher-side implementation of the internal endpoint.

```
GET http://category-service/internal/categories/snapshot
Headers:
  x-internal-service-key: {InternalServices__ApiKey}
  x-tenant-id: {tenantId}   # optional — omit for global categories
```

Add the startup seeder to your service:

```csharp
// {Service}.Infrastructure/BackgroundServices/CategorySnapshotBackfillService.cs
public sealed class CategorySnapshotBackfillService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CategorySnapshotBackfillService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CategorySnapshotBackfillService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<CategorySnapshotBackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<YourServiceDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var categoryBaseUrl = config["Services:Category:BaseUrl"] ?? throw new InvalidOperationException("Services:Category:BaseUrl is not configured");
        var internalKey = config["InternalServices:ApiKey"] ?? throw new InvalidOperationException("InternalServices:ApiKey is not configured");

        // Seed global categories (no x-tenant-id header).
        // For tenant-scoped categories, call this once per tenant and add:
        //   client.DefaultRequestHeaders.Add("x-tenant-id", tenantId);
        // before the GetFromJsonAsync call.
        _logger.LogInformation("Seeding CategorySnapshots from Category internal snapshot endpoint...");

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-internal-service-key", internalKey);
        client.BaseAddress = new Uri(categoryBaseUrl);

        var categories = await client.GetFromJsonAsync<List<CategoryEventMessage>>(
            "/internal/categories/snapshot", JsonOpts, ct)
            ?? [];

        // Per-row upsert: insert rows that do not yet exist in the local table.
        // Rows already written by the live subscriber (CategorySnapshotSyncService) are
        // skipped so we never overwrite a live event with potentially older snapshot data.
        // The staleness guard in UpsertAsync protects ordering in the subscriber direction.
        var seeded = 0;
        foreach (var c in categories)
        {
            var exists = await db.CategorySnapshots
                .AnyAsync(x => x.Id == c.Id && x.TenantId == c.TenantId, ct);

            if (!exists)
            {
                db.CategorySnapshots.Add(new CategorySnapshotEntity
                {
                    Id               = c.Id,
                    TenantId         = c.TenantId,
                    Slug             = c.Slug,
                    Uri              = c.Uri,
                    ParentId         = c.ParentId,
                    Path             = c.Path,
                    Depth            = c.Depth,
                    IconName         = c.IconName,
                    IconFileId       = c.IconFileId,
                    ImageFileId      = c.ImageFileId,
                    NameTranslations = c.NameTranslations,
                    LastSyncedAt     = DateTimeOffset.UtcNow
                });
                seeded++;
            }
        }

        if (seeded > 0) await db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "CategorySnapshot seed complete: {Seeded} inserted, {Skipped} already synced.",
            seeded, categories.Count - seeded);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

Register it in DI:

```csharp
services.AddHttpClient();  // if not already registered
services.AddHostedService<CategorySnapshotBackfillService>();
```

Add the config values:

```json
// appsettings.json
{
  "Services": {
    "Category": { "BaseUrl": "http://localhost:5002" }
  },
  "InternalServices": {
    "ApiKey": "your-secret-key-here" // same key configured in Category service
  }
}
```

> **Important:** The `InternalServices:ApiKey` must match the value configured in the **Category service's** `appsettings.json`. Use a long random string (32+ characters) and store it in your secrets manager in production.

The Category service internal endpoint is:

- `GET /internal/categories/snapshot` — returns `List<CategoryEventMessage>` for all non-archived categories
- Protected by `x-internal-service-key` header
- Respects `x-tenant-id` header for per-tenant scoping (omit for global categories)
- Hidden from public Swagger

---

## What Happens When a Category is Moved

A **Move** triggers a single `Updated` event for the moved node only (with its new `Path` and `Depth`). Descendants are updated in the Category DB but individual events are not published for each descendant.

**Implication for consumers:** If your query depends on the `Path` of descendant nodes (e.g., "give me all categories under /electronics/"), you may have stale paths for descendants until each is explicitly updated later.

**Mitigation options (choose one):**

1. On receiving an `Updated` event where `Path` changed, call `GET /internal/categories/all` once to re-sync all snapshots.
2. Store only `ParentId` in your snapshot and rebuild hierarchies at query time.
3. If strict path consistency is not required, accept brief staleness — path updates will self-heal as items in the subtree are eventually updated.

---

## Checklist for a New Consumer Service

- [ ] `CategorySnapshotEntity` added to domain
- [ ] EF model configured with JSONB for `NameTranslations`
- [ ] Migration created and applied
- [ ] `CategorySnapshotSyncService` added to Infrastructure
- [ ] Registered in DI (Redis-gated)
- [ ] `CategorySnapshotBackfillService` added for first-deployment seeding (calls `/internal/categories/snapshot`)
- [ ] `InternalServices:ApiKey` configured in both Category service and consumer service
- [ ] `SchemaVersion` guard added to `HandleMessageAsync` (skip unsupported versions)
- [ ] Queries use `CategorySnapshots` table — no HTTP call to Category service at runtime
- [ ] Redis is enabled in production `appsettings.json`

---

## Key Design Decisions

| Decision                                 | Reason                                                                                                                                                                                                                                                                      |
| ---------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Pub/Sub over polling                     | Zero latency — snapshot is updated within milliseconds of mutation                                                                                                                                                                                                          |
| Redis Pub/Sub over a full message broker | No new infrastructure; `IConnectionMultiplexer` is already registered                                                                                                                                                                                                       |
| Per-tenant channels                      | Tenant isolation — a consumer for tenant A never receives tenant B events                                                                                                                                                                                                   |
| Mutations never block on Redis           | The mutation handler never waits for Redis; `PublishAsync` only queues a row in the EF change tracker. The `OutboxEventProcessorService` handles actual Redis delivery asynchronously — category writes never fail because Redis is unavailable.                            |
| No file URLs in payload                  | Keeps events small; file URLs are resolved on-demand, not stored                                                                                                                                                                                                            |
| Composite PK `(Id, TenantId)`            | Same category Id can exist in multiple tenants                                                                                                                                                                                                                              |
| `SchemaVersion` field                    | Consumers safely discard unknown/future schema versions instead of corrupting local data                                                                                                                                                                                    |
| Internal snapshot endpoint               | Consumers seed their table on first deployment without calling the public API — secured with `x-internal-service-key`                                                                                                                                                       |
| Outbox pattern for publishing            | Publisher queues outbox row in EF change tracker; the handler's final repository `SaveChangesAsync` commits both the entity change and the outbox row atomically; background worker polls and publishes to Redis — at-least-once delivery even if Redis is temporarily down |
