# Nasheed Service — Ingestion Pipeline

**Last Updated:** August 13, 2026

---

## Overview

The ingestion pipeline processes songs in the background after upload. `FullPipeline` and `MetadataExtraction` both delegate lyrics/timing + enrichment extraction to `NasheedIngestionWorker.ExtractLyricsAndMetadataAsync`, which branches on the `nasheedNewLyricsExtractionEnabled` feature flag:

- **Old pipeline (default):** one AI chat request returns all enrichment data for the song record, including raw LRC lyrics with LLM-estimated timestamps.
- **New pipeline (opt-in) — hybrid:** reuses the *same* multimodal call as the old pipeline for lyrics text (proven better than pure-ASR text), plus a dedicated ASR transcription call for real audio-aligned timing, plus a cheap text-only call that aligns the two (never rewriting the trusted text), plus a separate text-only enrichment call. See `Doc/AI_INTEGRATION.md` "Two Lyrics/Timing Extraction Pipelines" for the full rationale and step-by-step.

It runs as a background `IHostedService` (`NasheedIngestionWorker`) that polls for pending jobs every 10 seconds.

```
Song uploaded (POST /api/songs)
  → SongIngestionJobEntity created (JobType=FullPipeline, Status=Pending)
  → NasheedIngestionWorker picks it up
      → [1] Lyrics/timing + enrichment extraction (old: one chat call; new/hybrid: extraction + ASR + merge + enrichment calls — flag-gated)
      → [2] Save song metadata and mood tags
      → [3] Queue EmbeddingGeneration job
  → EmbeddingGeneration job updates SongSearchDocumentEntity and marks song Indexed
```

---

## Startup Sequence

The worker cannot start processing until the DB is ready. The startup chain is:

```
NasheedTenantLoaderService.StartAsync()
  → reads MultiTenancy:TenantId from appsettings.json (currently configured as "anashid";
    throws InvalidOperationException at startup if the key is missing — there is no in-code default)
  → calls ITenantConfigurationProvider.GetTenantConfigurationAsync(tenantId)
        [fast retries: up to 12 times, 5 seconds apart = up to 60s wait]
  → on success (TryLoadTenantAsync):
      INasheedTenantCache.SetTenant(tenantInfo)         ← signals ready
      NasheedDbContext.Database.MigrateWithRecoveryAsync() ← runs EF migrations (with built-in retry)
      starts a background refresh loop (fire-and-forget, does not block StartAsync) — see
      "Background Tenant Config Refresh" below
  → on repeated failure (all 12 fast retries exhausted): logs an error, then StartAsync returns —
    but this is NOT a dead end. It kicks off RetryTenantLoadInBackgroundAsync, a fire-and-forget
    loop that keeps retrying TryLoadTenantAsync every 1 minute indefinitely (until the host shuts
    down), so a Tenant Service outage longer than ~60s can still self-heal without a service restart.

NasheedIngestionWorker.ExecuteAsync()
  → await _tenantCache.WaitUntilReadyAsync(stoppingToken)  ← blocks here until SetTenant() is called
  → starts polling loop (every 10 seconds)
```

If TenantService is unreachable and all 12 fast retries fail:

- `NasheedTenantLoaderService` logs an error, and `StartAsync` returns — but it does NOT give up:
  `RetryTenantLoadInBackgroundAsync` keeps calling `TryLoadTenantAsync` every 1 minute in the
  background until it succeeds or the host shuts down (this doc previously said the service "logs
  an error and returns" with no further recovery — corrected here; that was true only of the fast
  retry loop, not of `NasheedTenantLoaderService` as a whole)
- `INasheedTenantCache` stays not-ready until one of those background attempts succeeds
- `NasheedIngestionWorker` stays blocked (`WaitUntilReadyAsync` has no timeout of its own) until then
- HTTP requests also fail in the meantime (`NasheedDbContext` throws if neither `ITenantContext` nor `INasheedTenantCache` is ready)
- Once a background retry succeeds, the cache becomes ready, migration runs, and both the worker and HTTP requests recover with no restart required

### Background Tenant Config Refresh (no restart needed for flag/config changes)

Two mechanisms keep `INasheedTenantCache` in sync with Tenant Service after the initial startup load —
a push-based primary path and a slow polling fallback. Full design rationale and the shared-infra
pieces (`TenantConfigUpdatedEventMessage`, `PublishTenantConfigUpdatedAsync`) live in the root
`Doc/MULTI_TENANCY_GUIDE.md` → "Live Config-Change Push for Local-Snapshot Consumers"; this section
covers only Nasheed's own side.

**Primary — push via Redis Pub/Sub (`NasheedTenantConfigUpdatedListenerService`):** subscribes to
Tenant Service's `tenant:updated` channel. The moment `UpdateTenantCommandHandler` (or the
archive-toggle/delete handlers) saves a change for Nasheed's pinned tenant, this listener re-fetches
via `ITenantConfigurationProvider.GetTenantConfigurationAsync` and calls `INasheedTenantCache.SetTenant`
— typically within a second or two of the save. Registered via
`AddNasheedTenantConfigUpdatedListener(configuration)` in `Program.cs`, no-op when `Redis:Enabled` is
`false`.

**Fallback — periodic poll (`NasheedTenantLoaderService.RefreshTenantConfigurationPeriodicallyAsync`):**
a fire-and-forget loop (started right after `StartAsync`'s initial migration, without blocking it —
`StartAsync` is still awaited synchronously by the host for the initial load+migration, exactly as
before) that every **5 minutes** re-calls `ITenantConfigurationProvider.GetTenantConfigurationAsync`
and pushes the result back into `INasheedTenantCache` via `SetTenant` (safe to call repeatedly — it
just overwrites the cached `TenantInfo`). This only matters if the push above is ever missed (Redis
briefly disconnected, or this service restarting at the exact moment Tenant Service published) — it
bounds staleness to a few minutes in that rare case instead of leaving it stale until a restart.

Both mechanisms reuse the same `ITenantConfigurationProvider` (Redis-cached under
`tenant_config_{tenantId}`, invalidated by Tenant Service on every save) every other multi-tenant
service already relies on for per-request config resolution — neither introduces a separate cache,
per `Dotnet.instructions.md` pitfall #25. A transient failure in either path is logged as a warning
and the previous cached value is kept; neither ever crashes the process or blocks ingestion.

**Net effect:** a feature-flag or config change made in Tenant Service (including
`nasheedNewLyricsExtractionEnabled`) reaches `NasheedIngestionWorker` within a second or two in the
normal case, bounded to at most 5 minutes even if the push is missed — no Nasheed.API restart
required. (A restart was needed once, when this mechanism was first added, since it's new code — not
needed for subsequent flag/config changes going forward.)

---

## Job Types

### `FullPipeline`

Runs lyrics/timing + enrichment extraction (old or new pipeline, per feature flag) in sequence. Created automatically when a new song is uploaded, and can also be manually re-queued for an existing song via `POST /api/songs/{id}/retry-analysis` (see "Retrying Full Analysis" below).

After enrichment fields are saved, `FullPipeline` automatically queues `EmbeddingGeneration` so indexing happens asynchronously.

### `MetadataExtraction`

Extracts:

- `LanguageCode` — language of the song lyrics (e.g. `"ar"`, `"en"`)
- `Summary` — AI-generated description
- `VocalStyle` — stylistic description
- `DurationSeconds` — duration (old pipeline: LLM estimate; new pipeline: real ASR-reported duration)
- `LyricsRaw` — LRC-formatted lyrics (old pipeline: from the chat response; new/hybrid pipeline: the OLD pipeline's own lyrics text, re-timed by the worker via a merge/alignment pass against ASR word timestamps — see "Two Lyrics/Timing Extraction Pipelines" in `Doc/AI_INTEGRATION.md`)

Old pipeline uses AI keys: `nasheed:extraction:settings` + `nasheed:extraction:system-prompt`.
New/hybrid pipeline (flag-gated) uses: `nasheed:extraction:settings` + `nasheed:extraction:system-prompt` (shared with the old pipeline, for lyrics text) + `nasheed:transcription:settings` (for timing) + `nasheed:merge:settings` + `nasheed:merge:system-prompt` (text-only alignment) + `nasheed:enrichment:settings` + `nasheed:enrichment:system-prompt` — see `Doc/AI_INTEGRATION.md`.

`mood_tags` parsing note:

- Worker accepts both `mood_tags` and `moodTags` keys.
- Values are trimmed, empty entries are ignored, and duplicate values are de-duplicated before persistence.

### `LyricsVerification`

Optional/manual stage. Takes `LyricsRaw` and produces:

- `LyricsVerifiedLrc` — time-synced LRC format
- `LyricsPlainText` — clean plain text version

Uses AI keys: `nasheed:extraction:settings` + `nasheed:extraction:system-prompt`

Worker implementation detail: the returned AI content is treated as verified LRC text, then plain text is derived by removing LRC timestamps.

After verified lyrics are saved, an `EmbeddingGeneration` job is queued automatically.

### `EmbeddingGeneration`

Combines available song fields into `SearchText`, sends to AI.API embed endpoint, stores:

- `SongSearchDocumentEntity.EmbeddingJson` — JSON `float[]`
- `SongSearchDocumentEntity.EmbeddingModelKey` — model key used
- `Song.SearchIndexStatus = Indexed`

Uses AI key: `nasheed:embedding:settings`

This job is queued automatically after song enrichment changes and lyrics verification, and can also be queued manually by re-index operations.

Current `BuildSearchText(song)` includes:

- song title
- song summary (if present)
- verified plain lyrics truncated to first 500 chars (if present)

It currently does not append mood tags or vocal style.

---

## Job State Machine

```
                    (worker picks up)
  Pending ──────────────────────────────► Running
    ▲                                         │
    │ (MarkFailed + RetryCount < MaxRetries)  │ (MarkCompleted)
    └──────────────────────────────────────────▼
                 Pending                    Completed

  Running --(MarkFailed(retryable: false))-----------> Failed
  Running --(MarkFailed + RetryCount >= MaxRetries)--> Failed

  Any state ──► HardDeleted  (via DELETE /api/ingestion/{id})
```

**State transitions:**

- `Pending → Running`: `MarkRunning()` — sets `StartedAt`, `JobStatus = Running`
- `Running → Completed`: `MarkCompleted()` — sets `CompletedAt`, `JobStatus = Completed`
- `Running → Pending`: `MarkFailed(error, nextRetryAt, retryable: true)` when `RetryCount < MaxRetries`
- `Running → Failed`: `MarkFailed(error, nextRetryAt, retryable: true)` when `RetryCount >= MaxRetries`
- `Running → Failed`: `MarkFailed(error, nextRetryAt, retryable: false)` for non-retryable failures (for example HTTP 400 from AI API)
- `Failed/Pending → Pending`: `ResetForRetry()` clears `LastError` and `NextRetryAt`
- `Any → HardDeleted`: row is physically deleted from `SongIngestionJobs`

---

## Retry Logic

- Max retries: **10** (configurable via `MaxRetries` on the entity; `SongIngestionJobEntity.Create`'s `maxRetries` parameter defaults to 10, and every call site in the codebase — `CreateSongCommandHandler`, `RetrySongAnalysisCommandHandler`, `ReindexSongCommandHandler`, both `EmbeddingGeneration` queueing call sites — omits the argument, so every job actually gets 10. This doc previously said 3; corrected per the self-correcting-docs protocol.)
- Retry delay: exponential back-off via `NasheedIngestionWorker.GetRetryDelay(retryCount)` — attempt 1 → 30s, attempt 2 → 2min, attempt 3 → 10min, attempt 4+ → 30min (this doc previously said "fixed 5 minutes"; corrected here per the self-correcting-docs protocol — the code has always used exponential back-off)
- The worker only picks up `Pending` jobs where `NextRetryAt` is null or in the past
- Non-retryable failures are marked `Failed` immediately and are not auto-retried
- A failed job with `RetryCount >= MaxRetries` stays in `Failed` indefinitely
- Manual retry: `POST /api/ingestion/{id}/retry` calls `ResetForRetry()` and does not reset `RetryCount`

---

## Re-indexing

To force re-embedding of an already-processed song:

```
POST /api/ingestion/songs/{songId}/reindex
```

Creates a new `SongIngestionJobEntity` with `JobType = EmbeddingGeneration`. The worker re-generates the embedding and updates `SongSearchDocumentEntity`.

If a pending or running embedding job already exists for the same song, automatic queueing logic skips creating duplicates.

---

## Retrying Full Analysis

To force a song back through the *entire* AI pipeline (re-transcribe/re-extract lyrics and metadata, not just re-embed for search):

```
POST /api/songs/{id}/retry-analysis
```

`RetrySongAnalysisCommandHandler` (`Nasheed.Application/Handlers/RetrySongAnalysis/`):

- Forces `Song.LyricsVerified = false` (unconditional — not a toggle).
- Sets `Song.SongState = InQueue`, mirroring what `CreateSongCommandHandler` does right after queueing the original `FullPipeline` job, so the admin table shows the song as reprocessing immediately.
- Creates a new `SongIngestionJobEntity` with `JobType = FullPipeline`, unless one is already active for the song (`Pending`/`Running`), in which case the existing job is left alone — same idempotency pattern as re-indexing.

Unlike `POST /api/ingestion/songs/{songId}/reindex` (which only re-embeds), this re-queues the same job type that runs on initial upload, so lyrics, metadata, and mood tags are all re-extracted from the audio file again.

---

## Real-Time Progress Broadcast (SignalR)

Every job-status transition (`MarkRunning`, `MarkCompleted`, `MarkFailed`) is pushed to the Nasheed admin frontend in real time, so the ingestion/songs tables update live instead of requiring a manual refresh or polling.

**Mechanism:** `NasheedIngestionWorker.BroadcastProgressAsync` calls `INotificationServiceClient.SendTenantBroadcastAsync` (from `IhsanDev.Shared.Infrastructure`), which posts to the Notification service's `POST /api/v1/notifications/send` and fans out to the tenant's connected SignalR clients (`tenant:{tenantId}` group on `/hubs/notifications`) — see `Doc/NOTIFICATION_SERVICE_README.md`.

Hook points in `ProcessJobAsync`:

- Right after `job.MarkRunning()` is persisted
- Right after the job's terminal status is persisted following the `switch` on `JobType` (Completed, or Failed for an unknown job type)
- In the outer `catch` block, right after `job.MarkFailed(...)` is persisted

**Payload shape** (`data` field, JSON-encoded):

```json
{
  "event": "nasheed-ingestion-progress",
  "silent": true,
  "songId": 123,
  "jobId": 456,
  "jobType": "FullPipeline",
  "jobStatus": "Completed",
  "retryCount": 0,
  "errorMessage": null
}
```

- `deliveryType: "SignalR"` is used (not `"Both"`) — this fires on every job-status transition, so it deliberately skips Firebase to avoid a mobile push per pipeline step.
- `"silent": true` tells the frontend's shared `SignalrService` (`libs/shared/src/lib/services/signalr.service.ts`) to skip its usual toast popup — see `MicroservicesArchitecture-Web/Doc/REALTIME_NOTIFICATIONS_GUIDE.md`.
- A failed broadcast (network error, circuit open, non-2xx) is logged as a warning and never affects ingestion processing — notification delivery is best-effort and non-critical to the pipeline itself.

**Required configuration:**

- Nasheed.API registers the client: `builder.Services.AddNotificationServiceClient(builder.Configuration, "NasheedService", builder.Environment.IsDevelopment());` (`Program.cs`) and binds it in DI: `services.AddScoped<INotificationServiceClient, NotificationServiceClient>();` (`Nasheed.Infrastructure/Extensions/InfrastructureServiceExtensions.cs`)
- `Services:NotificationService:BaseUrl` in Nasheed's `appsettings.json`
- `"NasheedService"` must be in Notification.API's `ServiceCommunication:AllowedServices` (`Notification.API/appsettings.json`) — otherwise every broadcast call 401s silently behind the standard resilience retry/circuit-breaker (see `Doc/SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md` pitfall #9)

---

## Lyrics Fields Behavior

- `LyricsRaw` stores LRC from either pipeline — the single old-pipeline chat response, or the new pipeline's ASR-built LRC.
- `LyricsVerifiedLrc` is not auto-populated during `FullPipeline`.
- `LyricsVerifiedLrc` should be set only after explicit user verification of `LyricsRaw`.
- Updating `LyricsRaw` resets `LyricsVerifiedLrc` and `LyricsPlainText`.

---

## Worker Implementation Details

**File:** `Nasheed.Infrastructure/Workers/NasheedIngestionWorker.cs`

- Extends `BackgroundService`
- Injected: `IServiceScopeFactory`, `INasheedTenantCache`, `ILogger` (constructor has no `IConfiguration` — this doc previously listed one; corrected here per the self-correcting-docs protocol)
- Creates a scope per poll cycle to resolve scoped services (`NasheedDbContext`, `IAiApiClient`, `INotificationServiceClient`)
- Poll interval: 10 seconds
- Cancellation: respects `stoppingToken` passed to `ExecuteAsync`
- `ExtractLyricsAndMetadataAsync` — shared entry point for both `FullPipeline` and `MetadataExtraction`; branches on `IsNewLyricsExtractionEnabled()` (reads `INasheedTenantCache.Tenant.Configuration.FeatureFlags`, same pattern as `IsIngestionEnabled()`)
- `FilterHallucinatedWords` — new pipeline only; drops words belonging to a segment with `no_speech_prob` above a threshold (default `0.85`, admin-configurable via `AiProviderSettings.NoSpeechProbThreshold`; Whisper's own hallucination signal), with a safety cap that skips filtering entirely if it would remove more than 40% of a song's words — see `Doc/AI_INTEGRATION.md` "Hallucination Filtering"
- `ExtractLrcLines` — new/hybrid pipeline only; pulls `lyrics_raw_lrc` out of the extraction call's JSON response and strips its (unreliable) LRC timestamps down to an ordered list of trusted lyric lines
- `BuildMergePayload` — new/hybrid pipeline only; serializes the trusted lyric lines (numbered) and *filtered* ASR words (indexed) as `{"lines":[{"n":0,"text":"..."}],"words":[{"i":0,"w":"..."}]}` for the merge chat call
- `ParseLineMappings` — new/hybrid pipeline only; parses the merge call's `{"mappings":[{"line":N,"start_index":M}]}` response
- `BuildLrcFromLineMappings` — new/hybrid pipeline only; builds LRC text from the trusted lyric lines, looking up each line's real timestamp via the merge mappings against `confidentWords[start_index].Start` (integer-hundredths math to avoid floating-point timestamp drift) — the *text* is the old pipeline's own lyrics, unmodified; only the *timing* comes from this step
- `InterpolateMissingLineTimestamps` — new/hybrid pipeline only; fills a timestamp for any line the merge call didn't map, by linear interpolation between its nearest mapped neighbors (or carrying the nearest known value at the start/end of the song), so a missed mapping never drops a line from the final lyrics
- `ApplyEnrichmentMetadataAsync` — new pipeline only; mirrors `ApplyMetadataAsync` but takes the corrected `lyricsRawLrc` and ASR-derived `durationSeconds`/`language` as parameters instead of parsing them from the AI response
- `ApplyMoodTagsAsync` — shared mood-tag-application helper used by both `ApplyMetadataAsync` and `ApplyEnrichmentMetadataAsync`

**File:** `Nasheed.Infrastructure/Services/NasheedTenantLoaderService.cs`

- Implements `IHostedService`
- Registered as `AddHostedService<NasheedTenantLoaderService>()` — runs before `NasheedIngestionWorker`
- `StartAsync` is awaited by the host — migration completes before HTTP traffic begins (for however long the fast 12×5s retry loop takes; it does not block on the background fallback below)
- Tenant-load logic (fetch config → `SetTenant` → migrate → start periodic refresh) is factored into `TryLoadTenantAsync`, shared by both the fast startup retry loop and the background fallback below, so a caller can never populate the cache without also migrating
- If all 12 fast retries fail, `StartAsync` logs an error and returns, but first kicks off `RetryTenantLoadInBackgroundAsync` — a fire-and-forget loop that retries `TryLoadTenantAsync` every 1 minute indefinitely (bounded only by host shutdown), so a Tenant Service outage longer than ~60s doesn't permanently strand `NasheedIngestionWorker` (which has no timeout on `WaitUntilReadyAsync`) until someone notices and restarts the process
- After a successful load (fast path or background fallback), `StartAsync`/`TryLoadTenantAsync` also starts (but does not await) `RefreshTenantConfigurationPeriodicallyAsync` — a 5-minute fallback loop that keeps `INasheedTenantCache` in sync with Tenant Service in case the push-based listener below ever misses an event; see "Background Tenant Config Refresh" above
- `StopAsync` cancels both background loops (refresh and the fallback retry) via one internal `CancellationTokenSource`

**File:** `Nasheed.Infrastructure/Services/NasheedTenantConfigUpdatedListenerService.cs`

- `BackgroundService` — subscribes to Tenant Service's `tenant:updated` Redis Pub/Sub channel for the lifetime of the process; this is the primary tenant-config refresh path, not the fallback loop above
- Ignores events for any tenant other than Nasheed's pinned `MultiTenancy:TenantId`
- Registered via `AddNasheedTenantConfigUpdatedListener(configuration)` in `Program.cs`, no-op when `Redis:Enabled` is `false`
