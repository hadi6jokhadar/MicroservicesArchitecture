# Nasheed Service — AI Integration

**Last Updated:** August 3, 2026

---

## Overview

Nasheed uses one chat settings key and one chat prompt key for all chat-based AI operations (enrichment, verification, and generation). Embeddings use a separate embedding settings key.

Nasheed also supports a second, feature-flagged lyrics/timing extraction pipeline (`FeatureFlags.NasheedNewLyricsExtractionEnabled`, default `false`) that uses a dedicated ASR transcription call for timing instead of asking the chat model to estimate LRC timestamps in one shot. See "Two Lyrics/Timing Extraction Pipelines" below.

All keys are constants in `Nasheed.Application/Constants/NasheedAiKeys.cs`.

---

## AI.API Keys Reference

| Constant                           | Key Value                          | Purpose                               |
| ---------------------------------- | ---------------------------------- | ------------------------------------- |
| `NasheedAiKeys.ExtractionSettings` | `nasheed:extraction:settings`      | LLM settings for the **old** single-call pipeline (audio + timing)  |
| `NasheedAiKeys.ExtractionPrompt`   | `nasheed:extraction:system-prompt` | System prompt for the **old** single-call pipeline |
| `NasheedAiKeys.EmbeddingSettings`  | `nasheed:embedding:settings`       | Embedding model settings              |
| `NasheedAiKeys.TranscriptionSettings` | `nasheed:transcription:settings` | **New** pipeline — ASR model settings (`ModelType=Audio`, e.g. Whisper) |
| `NasheedAiKeys.EnrichmentSettings` | `nasheed:enrichment:settings` | **New** pipeline — text-only chat model settings (no audio needed) |
| `NasheedAiKeys.EnrichmentPrompt` | `nasheed:enrichment:system-prompt` | **New** pipeline — text-only system prompt (summary/mood/legal only) |

> **These keys must exist in AI.API's database for the tenant** (`anashid` by default) or have a global fallback. Missing keys cause 404/500 errors from AI.API. The `Transcription*`/`Enrichment*` keys are only required if a tenant enables `nasheedNewLyricsExtractionEnabled`.

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

Nasheed AI chat requests do not use framework default HTTP resilience timeout policies. This prevents long-running model operations from being canceled by a fixed 30-second client-side timeout.

`AI.API` requires `file_ids` as `int[]`. Nasheed stores `FileId` as `int`, so worker calls pass file ids directly.

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

### Transcribe Call (new pipeline only — real audio-aligned timing)

```
POST {AiService.BaseUrl}/api/v1/transcription
Headers: X-Service-Name, X-Service-Secret, x-tenant-id
Body:
{
  "settings_key": "nasheed:transcription:settings",
  "file_ids": [123]
}
Response:
{
  "text": "full transcript",
  "language": "ar",
  "duration": 178.2,
  "segments": [
    { "start": 3.52, "end": 10.98, "text": "..." }
  ]
}
```

Unlike the chat call, this is not a generative completion — `settings_key` must resolve to an
`AiProviderSettings` row with `ModelType = Audio` (a real ASR model, e.g. Whisper), and the
response's `segments[].start`/`end` come from the model's own audio alignment. `IAiApiClient.TranscribeAsync`
wraps this call and returns an `AiTranscriptionResult`. See `Doc/AI_SERVICE_CHAT_INTEGRATION_GUIDE.md`
"Transcription Endpoint" section for the full contract.

---

## Two Lyrics/Timing Extraction Pipelines

Nasheed's `NasheedIngestionWorker.ExtractLyricsAndMetadataAsync` branches on the tenant feature
flag `FeatureFlags.NasheedNewLyricsExtractionEnabled` (default `false`):

**Old pipeline (default):** one chat call to `nasheed:extraction:settings` /
`nasheed:extraction:system-prompt` with the audio file attached — the chat model both transcribes
the lyrics *and* estimates LRC timestamps in the same response. Simple (one call), but timing is
only as accurate as a generative model's estimate — see Stage 1 below.

**New pipeline (opt-in, per tenant):**

1. `IAiApiClient.TranscribeAsync(NasheedAiKeys.TranscriptionSettings, song.FileId, tenantId)` — a
   real ASR model transcribes the audio and returns segment-level timestamps grounded in actual
   audio alignment (not estimated).
2. `NasheedIngestionWorker.BuildLrcFromSegments` builds `lyrics_raw_lrc` directly from those
   segments — the worker constructs the LRC text itself; no LLM ever generates a timestamp.
3. `IAiApiClient.ChatAsync(NasheedAiKeys.EnrichmentSettings, NasheedAiKeys.EnrichmentPrompt, transcription.Text, tenantId)` —
   a **text-only** chat call (no `file_ids`, no audio) using the ASR transcript to produce
   `summary`/`vocal_style`/`mood_tags`/`legal_compliance`/`language_code` in Arabic. Cheaper and
   more reliable than the old prompt for these fields too, since the model is no longer sharing
   generation budget with audio/timing work.
4. `NasheedIngestionWorker.ApplyEnrichmentMetadataAsync` saves the result, using the ASR-derived
   `lyricsRawLrc`/`durationSeconds`/`language` (ASR `language` is used as a fallback if the
   enrichment response omits `language_code`) instead of parsing them from the chat response.

Enabling the flag requires the three new AI.API keys documented above
(`nasheed:transcription:settings` with `ModelType=Audio`, `nasheed:enrichment:settings`,
`nasheed:enrichment:system-prompt`) to exist for the tenant (or globally).

---

## Stage-by-Stage Details

### 1. Song Enrichment in Background Worker (old pipeline)

**Applies when:** `FeatureFlags.NasheedNewLyricsExtractionEnabled` is `false` (default) for the tenant. See "Two Lyrics/Timing Extraction Pipelines" above for the new pipeline.

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
  "legal_compliance": {
    "copyright_risk_level": "low",
    "content_safety_flag": "safe",
    "risk_reason": null
  },
  "mood_tags": ["calm", "gratitude"]
}
```

**What gets saved:** `Song.UpdateMetadata(languageCode, lyricsRawLrc, summary, vocalStyle, durationSeconds)`, `Song.UpdateLegalComplianceFromAi(copyrightRiskLevel, contentSafetyFlag, riskReason)`, and mood tags.

Mood tags key compatibility:

- Preferred AI output key is `mood_tags`.
- Worker also accepts `moodTags` for compatibility.

`legal_compliance` values are expected as strings from AI. The domain layer normalizes casing and only accepts:

- `copyright_risk_level`: `low`, `medium`, `high`
- `content_safety_flag`: `safe`, `flagged`

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
| `nasheed:transcription:settings`   | settings (`ModelType=Audio`, e.g. Whisper) | Only if `nasheedNewLyricsExtractionEnabled` |
| `nasheed:enrichment:settings`      | settings (text-only model)                 | Only if `nasheedNewLyricsExtractionEnabled` |
| `nasheed:enrichment:system-prompt` | prompt                                     | Only if `nasheedNewLyricsExtractionEnabled` |

Refer to `Doc/AI_SERVICE_CHAT_INTEGRATION_GUIDE.md` for how AI.API stores and resolves settings keys.

---

## Troubleshooting

| Error                                      | Likely Cause                                                                                                       |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------ |
| `404` from AI.API during ingestion         | Key not found in AI.API DB for this tenant                                                                         |
| `401` from AI.API                          | `X-Service-Secret` does not match AI.API's configured secret                                                       |
| `403` from AI.API                          | `X-Service-Name` is not in AI.API `ServiceCommunication:AllowedServices` list                                      |
| `InvalidOperationException` before AI call | `MultiTenancy:TenantId` is missing in Nasheed configuration                                                        |
| Ingestion job stays `Pending`              | `NasheedIngestionWorker` not started — check if `INasheedTenantCache` is ready                                     |
| `TimeoutRejectedException` with `00:00:30` | Old timeout policy behavior. Nasheed now sends AI requests without the default 30-second framework timeout policy. |
| Empty embedding / zero scores              | Embedding model key misconfigured or empty response from AI.API                                                    |
| Generation endpoint returns 500            | Check that extraction chat keys exist and AI.API is running                                                        |
| `400` "not an Audio model" from `/api/v1/transcription` | `nasheed:transcription:settings` row has the wrong `ModelType` — must be `Audio`, not `Text` |
| New pipeline enabled but old lyrics/timing still appear | `INasheedTenantCache` is populated once at startup by `NasheedTenantLoaderService` and never refreshed afterward — toggling the feature flag requires restarting Nasheed.API for the worker to pick up the new value |

Additional practical note:

- `Song.FileId` is numeric and passed directly into `file_ids` for AI.API chat requests.
