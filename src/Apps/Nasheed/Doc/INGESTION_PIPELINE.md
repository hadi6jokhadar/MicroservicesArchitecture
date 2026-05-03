# Nasheed Service — Ingestion Pipeline

**Last Updated:** May 4, 2026

---

## Overview

The ingestion pipeline processes songs in the background after upload. `FullPipeline` now uses one AI chat request that returns all enrichment data for the song record (including raw LRC lyrics). It runs as a background `IHostedService` (`NasheedIngestionWorker`) that polls for pending jobs every 10 seconds.

```
Song uploaded (POST /api/songs)
  → SongIngestionJobEntity created (JobType=FullPipeline, Status=Pending)
  → NasheedIngestionWorker picks it up
      → [1] Single AI enrichment request (metadata + raw LRC)
      → [2] Save song metadata and mood tags
      → [3] Queue EmbeddingGeneration job
  → EmbeddingGeneration job updates SongSearchDocumentEntity and marks song Indexed
```

---

## Startup Sequence

The worker cannot start processing until the DB is ready. The startup chain is:

```
NasheedTenantLoaderService.StartAsync()
  → reads MultiTenancy:TenantId from appsettings.json (default: "anashid")
  → calls ITenantConfigurationProvider.GetTenantConfigurationAsync(tenantId)
        [retries: up to 12 times, 5 seconds apart = up to 60s wait]
  → on success:
      INasheedTenantCache.SetTenant(tenantInfo)    ← signals ready
      NasheedDbContext.Database.MigrateAsync()     ← runs EF migrations

NasheedIngestionWorker.ExecuteAsync()
  → await _tenantCache.WaitUntilReadyAsync(stoppingToken)  ← blocks here until SetTenant() is called
  → starts polling loop (every 10 seconds)
```

If TenantService is unreachable and all 12 retries fail:

- `NasheedTenantLoaderService` logs an error and returns
- `INasheedTenantCache` never becomes ready
- `NasheedIngestionWorker` stays blocked (WaitAsync + cancellation token)
- HTTP requests also fail (NasheedDbContext throws if neither `ITenantContext` nor `INasheedTenantCache` is ready)

---

## Job Types

### `FullPipeline`

Runs one AI enrichment request in sequence. Created automatically when a new song is uploaded.

After enrichment fields are saved, `FullPipeline` automatically queues `EmbeddingGeneration` so indexing happens asynchronously.

### `MetadataExtraction`

Extracts:

- `LanguageCode` — language of the song lyrics (e.g. `"ar"`, `"en"`)
- `Summary` — AI-generated description
- `VocalStyle` — stylistic description
- `DurationSeconds` — duration (if available from audio analysis)
- `LyricsRaw` — LRC-formatted lyrics from the enrichment response

Uses AI keys: `nasheed:extraction:settings` + `nasheed:extraction:system-prompt`

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

  Running --(MarkFailed + RetryCount >= MaxRetries)--> Failed

  Any state ──► HardDeleted  (via DELETE /api/ingestion/{id})
```

**State transitions:**

- `Pending → Running`: `MarkRunning()` — sets `StartedAt`, `JobStatus = Running`
- `Running → Completed`: `MarkCompleted()` — sets `CompletedAt`, `JobStatus = Completed`
- `Running → Pending`: `MarkFailed(error, nextRetryAt)` when `RetryCount < MaxRetries`
- `Running → Failed`: `MarkFailed(error, nextRetryAt)` when `RetryCount >= MaxRetries`
- `Failed/Pending → Pending`: `ResetForRetry()` clears `LastError` and `NextRetryAt`
- `Any → HardDeleted`: row is physically deleted from `SongIngestionJobs`

---

## Retry Logic

- Max retries: **3** (configurable via `MaxRetries` on the entity, default 3)
- Retry delay: fixed 5 minutes (`RetryDelay = TimeSpan.FromMinutes(5)`) when retry is still allowed
- The worker only picks up `Pending` jobs where `NextRetryAt` is null or in the past
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

## Lyrics Fields Behavior

- `LyricsRaw` stores LRC from the single enrichment response.
- `LyricsVerifiedLrc` is not auto-populated during `FullPipeline`.
- `LyricsVerifiedLrc` should be set only after explicit user verification of `LyricsRaw`.
- Updating `LyricsRaw` resets `LyricsVerifiedLrc` and `LyricsPlainText`.

---

## Worker Implementation Details

**File:** `Nasheed.Infrastructure/Workers/NasheedIngestionWorker.cs`

- Extends `BackgroundService`
- Injected: `IServiceScopeFactory`, `INasheedTenantCache`, `IConfiguration`, `ILogger`
- Creates a scope per poll cycle to resolve scoped services (`NasheedDbContext`, `IAiApiClient`)
- Poll interval: 10 seconds
- Cancellation: respects `stoppingToken` passed to `ExecuteAsync`

**File:** `Nasheed.Infrastructure/Services/NasheedTenantLoaderService.cs`

- Implements `IHostedService`
- Registered as `AddHostedService<NasheedTenantLoaderService>()` — runs before `NasheedIngestionWorker`
- `StartAsync` is awaited by the host — migration completes before HTTP traffic begins
