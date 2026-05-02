# Nasheed Service — AI Integration

**Last Updated:** May 2, 2026

---

## Overview

Nasheed calls AI.API for four distinct purposes. Each call uses a hardcoded **settings key** (which tells AI.API which model/provider to use) and optionally a **system prompt key**.

All keys are constants in `Nasheed.Application/Constants/NasheedAiKeys.cs`.

---

## AI.API Keys Reference

| Constant                             | Key Value                            | Purpose                              |
| ------------------------------------ | ------------------------------------ | ------------------------------------ |
| `NasheedAiKeys.ExtractionSettings`   | `nasheed:extraction:settings`        | LLM settings for metadata extraction |
| `NasheedAiKeys.ExtractionPrompt`     | `nasheed:extraction:system-prompt`   | System prompt for extraction         |
| `NasheedAiKeys.VerificationSettings` | `nasheed:verification:settings`      | LLM settings for lyrics verification |
| `NasheedAiKeys.VerificationPrompt`   | `nasheed:verification:system-prompt` | System prompt for verification       |
| `NasheedAiKeys.GenerationSettings`   | `nasheed:generation:settings`        | LLM settings for lyrics generation   |
| `NasheedAiKeys.GenerationPrompt`     | `nasheed:generation:system-prompt`   | System prompt for lyrics generation  |
| `NasheedAiKeys.EmbeddingSettings`    | `nasheed:embedding:settings`         | Embedding model settings             |

> **These keys must exist in AI.API's database for the tenant** (`anashid` by default) or have a global fallback. Missing keys cause 404/500 errors from AI.API.

---

## How Nasheed Calls AI.API

**Interface:** `IAiApiClient` (in `Nasheed.Infrastructure`)  
**Config key:** `Services:AiService:BaseUrl`  
**Auth headers:**

- `X-Service-Name: nasheed`
- `X-Service-Secret: <ServiceCommunication:SharedSecret>`

### Chat Call (for extraction, verification, generation)

```
POST {AiService.BaseUrl}/api/v1/chat/single
Headers: X-Service-Name, X-Service-Secret
Body:
{
  "settingsKey": "nasheed:extraction:settings",
  "systemPromptKey": "nasheed:extraction:system-prompt",
  "userMessage": "<content to process>"
}
Response:
{
  "content": "<AI response text>"
}
```

### Embed Call (for search indexing)

```
POST {AiService.BaseUrl}/api/v1/embed
Headers: X-Service-Name, X-Service-Secret
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

---

## Stage-by-Stage Details

### 1. Metadata Extraction

**When:** During `IngestionJobType.MetadataExtraction` (and first stage of `FullPipeline`)

**Input to AI:** Audio analysis text or existing lyrics if available, plus song title and artist name.

**AI keys:** `nasheed:extraction:settings` + `nasheed:extraction:system-prompt`

**Expected AI response (parsed JSON from `content`):**

```json
{
  "languageCode": "ar",
  "summary": "A calm nasheed about gratitude",
  "vocalStyle": "Acapella, solo",
  "durationSeconds": 195,
  "lyricsRaw": "..."
}
```

**What gets saved:** `Song.UpdateMetadata(languageCode, lyricsRaw, summary, vocalStyle, durationSeconds)`

---

### 2. Lyrics Verification

**When:** During `IngestionJobType.LyricsVerification` (and second stage of `FullPipeline`)  
**Requires:** `Song.LyricsRaw` is not null (skipped if no lyrics were extracted)

**Input to AI:** The raw lyrics text.

**AI keys:** `nasheed:verification:settings` + `nasheed:verification:system-prompt`

**Expected AI response (parsed from `content`):**

```json
{
  "lrc": "[00:00.00] Line one\n[00:05.00] Line two\n...",
  "plainText": "Line one\nLine two\n..."
}
```

**What gets saved:** `Song.SetVerifiedLyrics(lrc, plainText)`

---

### 3. Embedding Generation

**When:** During `IngestionJobType.EmbeddingGeneration` (and third stage of `FullPipeline`), or on re-index.

**Input to AI embed:** Constructed `SearchText` — a concatenation of title, summary, lyrics plain text, mood tags, and vocal style.

**AI key:** `nasheed:embedding:settings` (no system prompt needed)

**What gets saved:**

- `SongSearchDocumentEntity.EmbeddingJson` = `JsonSerializer.Serialize(float[])`
- `Song.SearchIndexStatus = Indexed`

---

### 4. Lyrics Generation (On-Demand)

**When:** User calls `POST /api/generation/lyrics`

**AI keys:** `nasheed:generation:settings` + `nasheed:generation:system-prompt`

**Not part of the ingestion pipeline.** This is a direct user-facing endpoint.

---

## Setting Up AI.API for a New Tenant

For a new tenant using Nasheed, insert the following records into AI.API's database:

| Key                                  | Type                       | Required |
| ------------------------------------ | -------------------------- | -------- |
| `nasheed:extraction:settings`        | settings (model config)    | ✅       |
| `nasheed:extraction:system-prompt`   | prompt                     | ✅       |
| `nasheed:verification:settings`      | settings                   | ✅       |
| `nasheed:verification:system-prompt` | prompt                     | ✅       |
| `nasheed:generation:settings`        | settings                   | ✅       |
| `nasheed:generation:system-prompt`   | prompt                     | ✅       |
| `nasheed:embedding:settings`         | settings (embedding model) | ✅       |

Refer to `Doc/AI_SERVICE_CHAT_INTEGRATION_GUIDE.md` for how AI.API stores and resolves settings keys.

---

## Troubleshooting

| Error                              | Likely Cause                                                                   |
| ---------------------------------- | ------------------------------------------------------------------------------ |
| `404` from AI.API during ingestion | Key not found in AI.API DB for this tenant                                     |
| `401` from AI.API                  | `X-Service-Secret` does not match AI.API's configured secret                   |
| Ingestion job stays `Pending`      | `NasheedIngestionWorker` not started — check if `INasheedTenantCache` is ready |
| Empty embedding / zero scores      | Embedding model key misconfigured or empty response from AI.API                |
| Generation endpoint returns 500    | Check that generation keys exist and AI.API is running                         |
