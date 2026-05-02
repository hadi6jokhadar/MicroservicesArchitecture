# Nasheed Service — Ingestion Pipeline

**Last Updated:** May 2, 2026

---

## Overview

The ingestion pipeline processes songs through three AI-powered stages after upload. It runs as a background `IHostedService` (`NasheedIngestionWorker`) that polls for pending jobs every 10 seconds.

```
Song uploaded (POST /api/songs)
  → SongIngestionJobEntity created (JobType=FullPipeline, Status=Pending)
  → NasheedIngestionWorker picks it up
      → [1] MetadataExtraction
      → [2] LyricsVerification
      → [3] EmbeddingGeneration
  → Song.SongState = Done, Song.SearchIndexStatus = Indexed
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

- `NasheedTenantLoaderService` throws
- `INasheedTenantCache` never becomes ready
- `NasheedIngestionWorker` stays blocked (WaitAsync + cancellation token)
- HTTP requests also fail (NasheedDbContext throws if neither `ITenantContext` nor `INasheedTenantCache` is ready)

---

## Job Types

### `FullPipeline`

Runs all three stages in sequence. Created automatically when a new song is uploaded.

### `MetadataExtraction`

Extracts:

- `LanguageCode` — language of the song lyrics (e.g. `"ar"`, `"en"`)
- `Summary` — AI-generated description
- `VocalStyle` — stylistic description
- `DurationSeconds` — duration (if available from audio analysis)
- `LyricsRaw` — raw extracted lyrics

Uses AI keys: `nasheed:extraction:settings` + `nasheed:extraction:system-prompt`

### `LyricsVerification`

Takes `LyricsRaw` and produces:

- `LyricsVerifiedLrc` — time-synced LRC format
- `LyricsPlainText` — clean plain text version

Uses AI keys: `nasheed:verification:settings` + `nasheed:verification:system-prompt`

### `EmbeddingGeneration`

Combines available song fields into `SearchText`, sends to AI.API embed endpoint, stores:

- `SongSearchDocumentEntity.EmbeddingJson` — JSON `float[]`
- `SongSearchDocumentEntity.EmbeddingModelKey` — model key used
- `Song.SearchIndexStatus = Indexed`

Uses AI key: `nasheed:embedding:settings`

---

## Job State Machine

```
                    (worker picks up)
  Pending ──────────────────────────────► Running
    ▲                                         │
    │  (ResetForRetry, RetryCount<MaxRetries) │
    └──────────── Failed ◄────────────────────┘
                    │                         │ (MarkCompleted)
                    │ (RetryCount >= MaxRetries)│
                    ▼                         ▼
                  [stays Failed]           Completed

  Any state ──► Removed  (via DELETE /api/ingestion/{id})
```

**State transitions:**

- `Pending → Running`: `MarkRunning()` — sets `StartedAt`, `JobStatus = Running`
- `Running → Completed`: `MarkCompleted()` — sets `CompletedAt`, `JobStatus = Completed`
- `Running → Failed`: `MarkFailed(error, nextRetryAt)` — sets `LastError`, `NextRetryAt`, increments `RetryCount`
- `Failed → Pending`: `ResetForRetry()` — clears error, sets back to `Pending` (only if `RetryCount < MaxRetries`)
- `Any → Removed`: `MarkRemoved()` — sets `RemovedAt`, `JobStatus = Removed`

---

## Retry Logic

- Max retries: **3** (configurable via `MaxRetries` on the entity, default 3)
- Retry delay: exponential backoff based on `RetryCount` (set in `NextRetryAt`)
- The worker only picks up `Pending` jobs where `NextRetryAt` is null or in the past
- A failed job with `RetryCount >= MaxRetries` stays in `Failed` indefinitely
- Manual retry: `POST /api/ingestion/{id}/retry` calls `ResetForRetry()` and resets `RetryCount`

---

## Re-indexing

To force re-embedding of an already-processed song:

```
POST /api/ingestion/songs/{songId}/reindex
```

Creates a new `SongIngestionJobEntity` with `JobType = EmbeddingGeneration`. The worker re-generates the embedding and updates `SongSearchDocumentEntity`.

---

## Worker Implementation Details

**File:** `Nasheed.Infrastructure/Workers/NasheedIngestionWorker.cs`

- Extends `BackgroundService`
- Injected: `IServiceScopeFactory`, `INasheedTenantCache`, `ILogger`
- Creates a scope per poll cycle to resolve scoped services (`NasheedDbContext`, `IAiApiClient`)
- Poll interval: 10 seconds
- Cancellation: respects `stoppingToken` passed to `ExecuteAsync`

**File:** `Nasheed.Infrastructure/Services/NasheedTenantLoaderService.cs`

- Implements `IHostedService`
- Registered as `AddHostedService<NasheedTenantLoaderService>()` — runs before `NasheedIngestionWorker`
- `StartAsync` is awaited by the host — migration completes before HTTP traffic begins
