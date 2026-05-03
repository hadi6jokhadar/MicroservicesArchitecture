# Nasheed Service — AI Integration

**Last Updated:** May 4, 2026

---

## Overview

Nasheed uses one chat settings key and one chat prompt key for all chat-based AI operations (enrichment, verification, and generation). Embeddings use a separate embedding settings key.

All keys are constants in `Nasheed.Application/Constants/NasheedAiKeys.cs`.

---

## AI.API Keys Reference

| Constant                           | Key Value                          | Purpose                               |
| ---------------------------------- | ---------------------------------- | ------------------------------------- |
| `NasheedAiKeys.ExtractionSettings` | `nasheed:extraction:settings`      | LLM settings for all chat operations  |
| `NasheedAiKeys.ExtractionPrompt`   | `nasheed:extraction:system-prompt` | System prompt for all chat operations |
| `NasheedAiKeys.EmbeddingSettings`  | `nasheed:embedding:settings`       | Embedding model settings              |

> **These keys must exist in AI.API's database for the tenant** (`anashid` by default) or have a global fallback. Missing keys cause 404/500 errors from AI.API.

---

## How Nasheed Calls AI.API

**Interface:** `IAiApiClient` (in `Nasheed.Infrastructure`)  
**Config key:** `Services:AiService:BaseUrl`  
**Auth headers:**

- `X-Service-Name: NasheedService`
- `X-Service-Secret: <ServiceCommunication:SharedSecret>`

### Chat Call (for enrichment, verification, generation)

```
POST {AiService.BaseUrl}/api/v1/chat/single
Headers: X-Service-Name, X-Service-Secret
Body:
{
  "settings_key": "nasheed:extraction:settings",
  "system_prompt_key": "nasheed:extraction:system-prompt",
  "messages": [
    {
      "role": "user",
      "content": "<optional content to process>"
    }
  ],
  "file_ids": [123]
}
Response:
{
  "content": "<AI response text>"
}
```

Nasheed always sends `x-tenant-id` on chat requests. The tenant id is resolved from `MultiTenancy:TenantId` (single-tenant configuration). If it is missing, Nasheed throws an `InvalidOperationException` before calling AI.API.

Metadata extraction jobs now call AI.API with `settings_key`, `system_prompt_key`, a user message, and `file_ids` populated from the song `FileId`.

`AI.API` requires `file_ids` as `int[]`. Nasheed stores `FileId` as `string`, so the worker parses it to `int` before sending. If the value is non-numeric, Nasheed logs a warning and sends the chat request without file attachment context.

### Embed Call (for search indexing)

```
POST {AiService.BaseUrl}/api/v1/embedding
Headers: X-Service-Name, X-Service-Secret, x-tenant-id
Body:
{
  "settingsKey": "nasheed:embedding:settings",
  "text": "<text to embed>"
}
Response:
{
  "embedding": [0.1, 0.2, ...]  // float[]
}
```

Nasheed resolves `x-tenant-id` from `MultiTenancy:TenantId` for embedding calls (same as chat). This ensures tenant-scoped embedding settings are used.

---

## Stage-by-Stage Details

### 1. Song Enrichment in Background Worker

**When:** During `IngestionJobType.FullPipeline` (default job queued when creating a song)

**Input to AI:** Song title and file reference.

**AI keys:** `nasheed:extraction:settings` + `nasheed:extraction:system-prompt`

**Expected AI response (parsed JSON from `content`):**

```json
{
  "language_code": "ar",
  "summary": "A calm nasheed about gratitude",
  "vocal_style": "Acapella, solo",
  "duration_seconds": 195,
  "lyrics_raw_lrc": "[00:00.00] Line one\n[00:05.00] Line two\n...",
  "mood_tags": ["calm", "gratitude"]
}
```

**What gets saved:** `Song.UpdateMetadata(languageCode, lyricsRawLrc, summary, vocalStyle, durationSeconds)` and mood tags.

`LyricsRaw` is expected to be LRC format from this response.

After enrichment data is saved, Nasheed queues an `EmbeddingGeneration` job to refresh semantic search index data.

---

### 2. Lyrics Verification (Optional/Manual)

**When:** During `IngestionJobType.LyricsVerification` or explicit user-driven verification flows.

**Input to AI:** `Song.LyricsRaw` in LRC format.

**AI keys:** `nasheed:extraction:settings` + `nasheed:extraction:system-prompt`

**Expected AI response:** verified lyrics text in LRC format.

**What gets saved:** `Song.SetVerifiedLyrics(verifiedLrc, plainText)` where plain text is derived by stripping LRC timestamps in worker code.

After verified lyrics are saved, Nasheed queues an `EmbeddingGeneration` job to refresh index data.

---

### 3. Embedding Generation

**When:** During `IngestionJobType.EmbeddingGeneration` jobs queued automatically after song content changes, or manually by re-index.

**Input to AI embed:** Constructed `SearchText` — currently title, summary, and first 500 chars of verified plain lyrics.

**AI key:** `nasheed:embedding:settings` (no system prompt needed)

**What gets saved:**

- `SongSearchDocumentEntity.EmbeddingJson` = `JsonSerializer.Serialize(float[])`
- `Song.SearchIndexStatus = Indexed`

---

### 4. Lyrics Generation (On-Demand)

**When:** User calls `POST /api/generation/lyrics`

**AI keys:** `nasheed:extraction:settings` + `nasheed:extraction:system-prompt`

**Not part of the ingestion pipeline.** This is a direct user-facing endpoint.

---

## Setting Up AI.API for a New Tenant

For a new tenant using Nasheed, insert the following records into AI.API's database:

| Key                                | Type                       | Required |
| ---------------------------------- | -------------------------- | -------- |
| `nasheed:extraction:settings`      | settings (model config)    | ✅       |
| `nasheed:extraction:system-prompt` | prompt                     | ✅       |
| `nasheed:embedding:settings`       | settings (embedding model) | ✅       |

Refer to `Doc/AI_SERVICE_CHAT_INTEGRATION_GUIDE.md` for how AI.API stores and resolves settings keys.

---

## Troubleshooting

| Error                                      | Likely Cause                                                                   |
| ------------------------------------------ | ------------------------------------------------------------------------------ |
| `404` from AI.API during ingestion         | Key not found in AI.API DB for this tenant                                     |
| `401` from AI.API                          | `X-Service-Secret` does not match AI.API's configured secret                   |
| `403` from AI.API                          | `X-Service-Name` is not in AI.API `ServiceCommunication:AllowedServices` list  |
| `InvalidOperationException` before AI call | `MultiTenancy:TenantId` is missing in Nasheed configuration                    |
| Ingestion job stays `Pending`              | `NasheedIngestionWorker` not started — check if `INasheedTenantCache` is ready |
| Empty embedding / zero scores              | Embedding model key misconfigured or empty response from AI.API                |
| Generation endpoint returns 500            | Check that extraction chat keys exist and AI.API is running                    |

Additional practical note:

- If `Song.FileId` is non-numeric, worker logs a warning and omits `file_ids` in chat request because AI.API expects `int[]` for file attachments.
