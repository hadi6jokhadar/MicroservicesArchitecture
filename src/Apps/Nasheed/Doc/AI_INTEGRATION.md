# Nasheed Service — AI Integration

**Last Updated:** August 8, 2026

---

## Overview

Nasheed uses one chat settings key and one chat prompt key for all chat-based AI operations (enrichment, verification, and generation). Embeddings use a separate embedding settings key.

Nasheed also supports a second, feature-flagged lyrics/timing extraction pipeline (`FeatureFlags.NasheedNewLyricsExtractionEnabled`, default `false`) — a **hybrid** pipeline that reuses the old pipeline's own proven-better lyrics text and only adds a dedicated ASR transcription call plus a cheap text-only alignment call for real audio-grounded timing, instead of asking the chat model to estimate LRC timestamps in one shot. See "Two Lyrics/Timing Extraction Pipelines" below.

All keys are constants in `Nasheed.Application/Constants/NasheedAiKeys.cs`.

---

## AI.API Keys Reference

| Constant                           | Key Value                          | Purpose                               |
| ---------------------------------- | ---------------------------------- | ------------------------------------- |
| `NasheedAiKeys.ExtractionSettings` | `nasheed:extraction:settings`      | LLM settings for the **old** single-call pipeline (audio + timing)  |
| `NasheedAiKeys.ExtractionPrompt`   | `nasheed:extraction:system-prompt` | System prompt for the **old** single-call pipeline |
| `NasheedAiKeys.EmbeddingSettings`  | `nasheed:embedding:settings`       | Embedding model settings              |
| `NasheedAiKeys.TranscriptionSettings` | `nasheed:transcription:settings` | **New (hybrid)** pipeline — ASR model settings (`ModelType=Audio`, e.g. Whisper) — timing only |
| `NasheedAiKeys.MergeSettings` | `nasheed:merge:settings` | **New (hybrid)** pipeline — text-only chat model settings for aligning the extraction call's lyric lines against the ASR word list (see "Merge Call" below) |
| `NasheedAiKeys.MergePrompt` | `nasheed:merge:system-prompt` | **New (hybrid)** pipeline — system prompt for the alignment pass |
| `NasheedAiKeys.EnrichmentSettings` | `nasheed:enrichment:settings` | **New (hybrid)** pipeline — text-only chat model settings (no audio needed) |
| `NasheedAiKeys.EnrichmentPrompt` | `nasheed:enrichment:system-prompt` | **New (hybrid)** pipeline — text-only system prompt (summary/mood/legal only) |

> **These keys must exist in AI.API's database for the tenant** (`anashid` by default) or have a global fallback. Missing keys cause 404/500 errors from AI.API. The `Transcription*`/`Merge*`/`Enrichment*` keys are only required if a tenant enables `nasheedNewLyricsExtractionEnabled`. Note: the hybrid pipeline also calls `ExtractionSettings`/`ExtractionPrompt` (the same keys the old pipeline uses) for its lyrics text — see "Two Lyrics/Timing Extraction Pipelines" below.

---

## How Nasheed Calls AI.API

**Interface:** `IAiApiClient` (in `Nasheed.Infrastructure`)  
**Config key:** `Services:AiService:BaseUrl`  
**Auth headers:**

- `X-Service-Name: NasheedService`
- `X-Service-Secret: <ServiceCommunication:SharedSecret>`

### Pipeline Run Correlation (`pipeline_run_id` / `pipelineRunId`)

Every AI call Nasheed makes while processing one ingestion job — `ChatAsync`, `TranscribeAsync`,
`EmbedAsync` — sends a correlation id, computed once per job as `NasheedIngestionWorker.BuildPipelineRunId`:
`$"nasheed:job:{job.Id}"` (reuses the existing `SongIngestionJobEntity.Id`, no new identifier
minted). AI.API stores it on:

- The `AiChatSession` row created for each chat call (`ChatRequest.pipeline_run_id`) — visible as
  the **Pipeline Run** column on the admin's Chat Sessions page, matched by its free-text search
  box (client-side, against whatever page is currently loaded), and also filterable server-side via
  `GET /api/v1/ai/chat-sessions/?pipeline_run_id=nasheed:job:456` (`api/routes/chat_sessions.py`) —
  use the server-side filter when checking an older job, since the list endpoint defaults to
  `limit=50` and a match outside that page won't show up in the client-side search.
- Every `AiTokenUsageLog` row (`ChatRequest.pipeline_run_id` for chat, `TranscriptionRequest.pipeline_run_id`
  for transcription, `EmbeddingRequest.pipelineRunId` for embedding) — this is the only correlation
  path for transcription, since `/api/v1/transcription` never creates a chat session at all. Also
  filterable server-side via `GET /api/v1/token-usage-logs/?pipeline_run_id=nasheed:job:456`
  (`api/routes/token_usage_logs.py`).

**Pitfall (found and fixed August 2026, twice):** `api/routes/chat_sessions.py`'s `ChatSessionResponse`
is a separate Pydantic response schema from the `AiChatSession` SQLAlchemy model — adding a column to
the model alone does **not** make it appear in `GET /api/v1/ai/chat-sessions/`'s JSON output; FastAPI
silently drops any field the response model doesn't declare. `PipelineRunId` had to be added to
`ChatSessionResponse` explicitly, separately from the model change. A follow-up sweep found the
identical bug independently in `api/routes/token_usage_logs.py`'s `TokenUsageLogResponse` — also fixed
the same way, plus a matching `pipeline_run_id` query filter on `list_token_usage_logs`. Any future
column added to `AiChatSession` or `AiTokenUsageLog` needs the same two-place update (model +
hand-written response schema) — this codebase has hit the mistake twice already.

This never changes prompt content or model behavior — `resolve_or_create_session` only reads
`session_id` to look up/create a session row; it never loads prior message history into a new
request (confirmed in `core/ai/sessions.py`). It is purely a "find everything from this job" tool for
debugging/auditing. Only applied when a **new** session is created — if a real `session_id` is ever
reused, that existing session keeps whichever `PipelineRunId` it was created with.

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
  "file_ids": [123],
  "pipeline_run_id": "nasheed:job:456"
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
  "text": "<text to embed>",
  "pipelineRunId": "nasheed:job:456"
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
  "file_ids": [123],
  "pipeline_run_id": "nasheed:job:456"
}
Response:
{
  "text": "full transcript",
  "language": "ar",
  "duration": 178.2,
  "segments": [
    { "start": 3.52, "end": 10.98, "text": "...", "avg_logprob": -0.31, "no_speech_prob": 0.02 }
  ],
  "words": [
    { "start": 3.52, "end": 3.81, "word": "..." }
  ]
}
```

`segments[].avg_logprob`/`no_speech_prob` are Whisper's own confidence signals (`null` if the
provider doesn't report them) — Nasheed uses `no_speech_prob` to drop likely-hallucinated words
before the merge call; see "Hallucination Filtering" below.

Unlike the chat call, this is not a generative completion — `settings_key` must resolve to an
`AiProviderSettings` row with `ModelType = Audio` (a real ASR model, e.g. Whisper), and both
`segments[].start`/`end` and `words[].start`/`end` come from the model's own audio alignment, not
an LLM estimate. AI.API requests `timestamp_granularities: ["segment", "word"]` — the model
configured for this key must support word-level timestamps or the response's `words` array comes
back empty and Nasheed's worker throws (word timestamps are what let the merge call below anchor
lyric lines to real audio timing). `IAiApiClient.TranscribeAsync`
wraps this call and returns an `AiTranscriptionResult` (`Words`, not `Segments` — Nasheed only
consumes word-level data). See `Doc/AI_SERVICE_CHAT_INTEGRATION_GUIDE.md`
"Transcription Endpoint" section for the full contract.

### Merge Call (hybrid pipeline only — aligning trusted text against ASR timing)

Whisper (and ASR models generally) transcribe Arabic without diacritics (tashkeel), split lines by
pauses in speech rather than poetic meter, and — being a narrow acoustic-decoding model with no real
language understanding — produce meaningfully worse text than a multimodal LLM that hears the same
audio directly with full language/world knowledge (this is exactly why the *old* pipeline's text
tends to read better than pure-ASR text, even though its LRC timing is only ever an LLM estimate).

An earlier version of this pipeline tried to fix Whisper's text by having a second, audio-attached
chat call *correct* it. Real-world testing (August 2026) showed this still produced meaningfully
worse text than the old pipeline's own single call — every correction pass, however good, is still
bounded by having to reconcile with (and partially trust) Whisper's already-degraded starting text.
**The hybrid pipeline instead doesn't try to fix Whisper's text at all — it reuses the OLD pipeline's
own proven-better lyrics text outright, and only asks a model to align it against the ASR word list
for timing.** This is a fundamentally easier, more mechanical task (matching two texts of the same
content) than generating or correcting poetry, so it can run as a **cheap, text-only, no-audio-needed**
call:

```
Body:
{
  "settings_key": "nasheed:merge:settings",
  "system_prompt_key": "nasheed:merge:system-prompt",
  "messages": [
    { "role": "user", "content": "{\"lines\":[{\"n\":0,\"text\":\"إِذا كانَ أَمرُ اللَهِ أَمراً يُقَدَّرُ\"},{\"n\":1,\"text\":\"فَكَيفَ يَفِرُّ المَرءُ مِنهُ وَيَحذَرُ\"}],\"words\":[{\"i\":0,\"w\":\"...\"},{\"i\":1,\"w\":\"...\"}, ...]}" }
  ]
}
Response:
{
  "content": "{\"mappings\":[{\"line\":0,\"start_index\":0},{\"line\":1,\"start_index\":5}]}"
}
```

**Input** (`BuildMergePayload` in `NasheedIngestionWorker.cs`): `lines` is the OLD pipeline's own
lyrics, already trusted, already diacritized, numbered `n` in order (extracted from its
`lyrics_raw_lrc` via `ExtractLrcLines`, which just strips that response's own — unreliable — LRC
timestamps). `words` is the filtered, indexed ASR word list (`confidentWords`), exactly as before.

**Output contract:** for every line, report which ASR word index (`start_index`) the model believes
that line begins at — `start_index` values must be strictly increasing as `line` increases. The model
**never rewrites or re-words `lines`** — its only job is finding the best-matching anchor index per
line. `ParseLineMappings` parses this; `BuildLrcFromLineMappings` looks up
`confidentWords[start_index].Start` for each line's real timestamp. A line the model doesn't map gets
an interpolated timestamp from its nearest mapped neighbors (`InterpolateMissingLineTimestamps`)
rather than being dropped — see that method's doc comment.

Because this call needs no audio, `nasheed:merge:settings` can be `ModelType=Text` with any capable
text model — no `PROVIDERS_SUPPORTING_AUDIO`/`AudioDataMode` requirement, unlike the old audio-attached
correction approach this replaces. Still set `Temperature = 0`/`TopP = 1.0` — this is a deterministic
matching task, not a creative one.

**JSON encoding note:** `BuildMergePayload` (and `AiApiClientService`'s outer HTTP request
serialization) both pass `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` to
`JsonSerializer.Serialize` — .NET's default encoder escapes every non-ASCII character (Arabic
included) as `\uXXXX`, which is meant for JSON embedded in HTML/JS and is pure overhead for an
API-to-API JSON body: 6 ASCII characters per escaped Arabic letter instead of 1, inflating token
counts on every request and making the payload harder for the model to read as natural Arabic text.
Any *new* `JsonSerializer.Serialize` call added to this pipeline that carries Arabic text should use
the same option — the default silently reintroduces this.

### Hallucination Filtering (before the Merge Call)

Whisper-family models are known to hallucinate — invent fluent-sounding text — specifically over
silence, breath, and reverb tails, which acapella nasheed recordings have a lot of between verses.
Offering the merge call a garbled/hallucinated ASR word as a timing-anchor candidate is unnecessary
noise it has to reason through — cheaper and more reliable to remove it before the call than to rely
on the model never picking a bad anchor.

`NasheedIngestionWorker.FilterHallucinatedWords` drops any word whose containing ASR segment has
`no_speech_prob` above a threshold (Whisper's own signal that it believes that stretch was
silence/non-speech) **before** building the indexed word list sent to the merge call — so
hallucinated segments never reach the merge step at all as anchor candidates. Dropped words are
logged at `Warning` with the actual text that got dropped, so a consistently-noisy source file is
visible in logs rather than silently discarded.
Deliberately not also gating on `avg_logprob` — that can be naturally low for correctly-transcribed
rare/classical vocabulary or melismatic singing, so using it as a hard filter would discard real
content.

**Threshold is admin-configurable, not hardcoded:** `AiProviderSettings.NoSpeechProbThreshold` (a
new nullable `Float` column, only meaningful when `ModelType=Audio`) is set per settings row via the
same admin UI used for Temperature/TopP/MaxCompletionTokens/FrequencyPenalty/PresencePenalty/AudioDataMode
(`apps/admin/.../ai-settings/add-edit-ai-setting-dialog`). `/api/v1/transcription`'s response echoes
it back as `no_speech_prob_threshold` (`transcription.py`, reading `ai_settings.NoSpeechProbThreshold`
for the `nasheed:transcription:settings` row used); `NasheedIngestionWorker.FilterHallucinatedWords`
uses `transcription.NoSpeechProbThreshold ?? DefaultHallucinationNoSpeechProbThreshold` — i.e. the
admin value wins when set, otherwise the code default applies. This lets the threshold be tuned per
recording style without a code change or redeploy — set it directly on the `nasheed:transcription:settings`
row, leave it `null` to keep the default.

**Threshold history (self-correcting, per this repo's protocol):** the (then-hardcoded) threshold
started at `0.6` and immediately wiped out an entire song's lyrics (August 2026) — `no_speech_prob`
is calibrated for spoken conversation, and sustained/melismatic *singing* with reverb (i.e. acapella
nasheed vocals) can legitimately push it well above `0.6` even during clearly audible singing. The
code default was raised to `0.85`, and the value was made admin-configurable so it no longer requires
a code change to retune per recording/genre.

**Safety net (`MaxHallucinatedWordFraction = 0.4`):** because `no_speech_prob` can still be
miscalibrated for a specific recording even at a better threshold, if filtering would remove more
than 40% of a song's words, that is treated as a sign the heuristic itself is unreliable for that
file — filtering is skipped entirely for it (logged at `Warning` with the flagged segments'
`no_speech_prob` values) rather than risking another all-lyrics-missing failure. Worst case if this
cap trips unnecessarily: an unusually noisy file's hallucinated words aren't filtered (same exposure
as before this feature existed) — far preferable to silently deleting real lyrics.

If every word in a transcription gets filtered (only possible when the dropped fraction is at or
below the safety cap yet still 100% — effectively unreachable in practice now, but kept as a defensive
check), the job throws `"Every transcribed word was filtered out as likely-hallucinated..."`.

---

## Two Lyrics/Timing Extraction Pipelines

Nasheed's `NasheedIngestionWorker.ExtractLyricsAndMetadataAsync` branches on the tenant feature
flag `FeatureFlags.NasheedNewLyricsExtractionEnabled` (default `false`):

**Old pipeline (default):** one chat call to `nasheed:extraction:settings` /
`nasheed:extraction:system-prompt` with the audio file attached — the chat model both transcribes
the lyrics *and* estimates LRC timestamps in the same response. Simple (one call), but timing is
only as accurate as a generative model's estimate — see Stage 1 below.

**New pipeline (opt-in, per tenant) — hybrid: old pipeline's text + Whisper's timing, merged:**

Two earlier designs were tried and superseded by real-world testing (August 2026) before landing on
this one — first a pure ASR-plus-text-only-correction pipeline (Whisper's text was too unreliable
even after correction), then an audio-attached correction pipeline (better, but still measurably
worse text than the old pipeline's own single call, since correction is still bounded by reconciling
with Whisper's degraded starting point). The hybrid pipeline instead **reuses the old pipeline's own
lyrics text outright** and only asks a cheap, text-only call to align it against Whisper's timing —
see "Merge Call" above for the full rationale.

1. `IAiApiClient.ChatAsync(NasheedAiKeys.ExtractionSettings, NasheedAiKeys.ExtractionPrompt, ..., fileIds)` —
   the exact same multimodal call the *old* pipeline uses (audio attached). Only its `lyrics_raw_lrc`
   is used (via `ExtractLrcLines`, which strips this response's own unreliable LRC timestamps down to
   an ordered list of lines) — its timing estimate and other fields (summary, mood tags, etc.) are
   discarded here; enrichment is handled separately in step 4, for the same reason it always has been
   (not sharing generation budget with audio/lyrics work).
2. `IAiApiClient.TranscribeAsync(NasheedAiKeys.TranscriptionSettings, song.FileId, tenantId)` — a
   real ASR model transcribes the audio and returns word-level timestamps grounded in actual
   audio alignment (not estimated). Throws if the model didn't return any words (e.g. misconfigured
   to a model without word-level timestamp support).
3. `NasheedIngestionWorker.FilterHallucinatedWords` drops words belonging to any segment with a high
   `no_speech_prob` — see "Hallucination Filtering" above. Throws if this removes every word.
4. `IAiApiClient.ChatAsync(NasheedAiKeys.MergeSettings, NasheedAiKeys.MergePrompt, mergePayloadJson, tenantId)` —
   a **text-only, no-audio** alignment call: for each of step 1's trusted lyric lines, reports which
   ASR word index (from step 3's filtered list) it starts at. Never rewrites the trusted text — see
   "Merge Call" above for the full contract.
5. `NasheedIngestionWorker.BuildLrcFromLineMappings` builds `lyrics_raw_lrc` from step 1's lines using
   step 4's line→word-index mappings to look up each line's real timestamp from
   `confidentWords[start_index].Start` — the *text* is the old pipeline's proven-better lyrics, the
   *timing* is Whisper's real audio alignment, never an LLM's guess either way.
6. `IAiApiClient.ChatAsync(NasheedAiKeys.EnrichmentSettings, NasheedAiKeys.EnrichmentPrompt, transcription.Text, tenantId)` —
   unchanged from earlier pipeline versions: a **text-only** chat call (no `file_ids`, no audio) using
   the raw ASR transcript to produce `summary`/`vocal_style`/`mood_tags`/`legal_compliance`/`language_code`.
7. `NasheedIngestionWorker.ApplyEnrichmentMetadataAsync` saves the result, using the merged
   `lyricsRawLrc` and Whisper-derived `durationSeconds`/`language` (ASR `language` is used as a
   fallback if the enrichment response omits `language_code`) instead of parsing them from the chat
   response.

This is **4 AI calls per song** (extraction, transcribe, merge, enrich — filtering is local/free) —
up from 3 with the correction-based design and 2 with the original ASR-plus-correction design. The
extra call is worth it: text quality no longer depends on a correction pass reconciling with
Whisper's mistakes at all, since the trusted text never touches Whisper's output in the first place.

Enabling the flag requires the AI.API keys documented above (`nasheed:extraction:settings`/
`nasheed:extraction:system-prompt` — shared with the old pipeline — plus `nasheed:transcription:settings`
with `ModelType=Audio` and word-level timestamp support, `nasheed:merge:settings`/
`nasheed:merge:system-prompt`, `nasheed:enrichment:settings`/`nasheed:enrichment:system-prompt`) to
exist for the tenant (or globally).

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
| `nasheed:transcription:settings`   | settings (`ModelType=Audio`, e.g. Whisper, with word-level timestamp support; optionally set `NoSpeechProbThreshold` to override the hallucination-filter default of 0.85 — leave `null` to keep it) | Only if `nasheedNewLyricsExtractionEnabled` |
| `nasheed:merge:settings`           | settings (`ModelType=Text` — no audio needed; any capable text model, doesn't need to be audio-capable or a top-tier model since this is a mechanical alignment task, not generation. Set `Temperature = 0` and `TopP = 1.0`) | Only if `nasheedNewLyricsExtractionEnabled` |
| `nasheed:merge:system-prompt`      | prompt (align trusted lyric lines to ASR word indices, never rewrite the text — see "Merge Call" above for the exact JSON contract the prompt must produce) | Only if `nasheedNewLyricsExtractionEnabled` |
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
| New pipeline enabled but old lyrics/timing still appear for more than a few seconds | `NasheedTenantConfigUpdatedListenerService` should refresh `INasheedTenantCache` within a second or two of the flag change via Tenant Service's `tenant:updated` Redis broadcast (see `Doc/INGESTION_PIPELINE.md` "Background Tenant Config Refresh"). If it's still stale after ~5 minutes (the fallback poll interval), check Nasheed.API's logs for `Subscribed to 'tenant:updated'` at startup (confirms the listener is running — absent if `Redis:Enabled` is `false`) and for `Failed to refresh tenant '...' configuration` warnings |
| `InvalidOperationException: ASR transcription returned no word-level timestamps` | The model configured under `nasheed:transcription:settings` doesn't support `timestamp_granularities: ["segment", "word"]` — use a model that does (e.g. OpenAI `whisper-1`) |
| `InvalidOperationException: AI extraction response does not include usable lyrics lines` | The multimodal extraction call (`nasheed:extraction:settings`) didn't return a usable `lyrics_raw_lrc` — check that key/prompt the same way you would for the old pipeline, since the hybrid pipeline depends on it for lyrics text |
| `InvalidOperationException: AI merge response contained no usable line mappings` / lyrics come back with wrong or missing timing after enabling the hybrid pipeline | `nasheed:merge:settings`/`nasheed:merge:system-prompt` missing, or the system prompt isn't returning valid `{"mappings":[{"line":N,"start_index":M}]}` for every line — see "Merge Call" above for the required JSON contract |

Additional practical note:

- `Song.FileId` is numeric and passed directly into `file_ids` for AI.API chat requests.
