# AI Service Overview

## Purpose

The AI service is a Python FastAPI microservice that acts as an AI gateway inside the MicroservicesArchitecture solution.
It centralizes model access, tenant-aware settings, prompt management, and both streaming and single chat responses.

Service path:

- `src/Services/AI/AI.API`

Default local URL:

- `http://localhost:5008`

## What the Service Does

1. Accepts chat requests and supports streaming or single-response LLM outputs.
2. Reads provider settings per tenant, with global fallback.
3. Stores and retrieves system prompts.
4. Logs token usage for auditing and cost tracking.
5. Supports internal service calls using shared-secret headers.
6. Supports end-user requests using JWT bearer tokens.
7. Orchestrates chat request preparation using LangGraph before calling LiteLLM.
8. Enforces request and payload validation using Pydantic models.
9. Fetches attached FileManager file IDs, encodes them as Base64, and injects OpenAI-compatible multimodal content blocks into the chat payload before LLM invocation.
10. Exposes read endpoints for sessions, messages, message files, and token usage logs.
11. Can generate and persist a session title in the background when `generate_session_title=true` is sent on chat requests, using the `Session-Title` system prompt and the `QwenAI-qwen3-vl-32b-instruct` provider settings key.
12. Generates text embedding vectors via `POST /api/v1/embedding` and logs token usage after each call.
13. Supports local BAAI bge-m3 embedding inference for any provider settings record where `Provider = BAAI` and `ModelName` is `bge-m3` or `baai/bge-m3` (key-independent detection), while preserving the existing LiteLLM provider flow.
14. Persists the exact model-input message list used in LiteLLM calls (system and request messages) for each chat turn, serializing structured JSON content blocks to strings for database debugging.

## High-Level Architecture

### API Layer

- `main.py`: FastAPI app startup, middleware, exception handlers, router registration.
- `api/routes/chat.py`: Streaming and single-response **LLM chat** endpoints. Uses `core/ai/chat_workflow.py` LangGraph pipeline.
- `api/routes/settings.py`: AI provider settings CRUD.
- `api/routes/system_prompts.py`: System prompt CRUD.
- `api/routes/chat_sessions.py`: Chat session listing with filtering and pagination.
- `api/routes/chat_messages.py`: Chat message listing with filtering and pagination.
- `api/routes/chat_message_files.py`: Chat message to file relation listing.
- `api/routes/token_usage_logs.py`: Token usage log listing with filtering, pagination, and aggregate statistics (`GET /stats`).
- `api/routes/embedding.py`: Text embedding endpoint (`POST /api/v1/embedding`). Resolves provider settings, validates `ModelType == Embedding`, uses local `BAAI/bge-m3` when configured, otherwise calls LiteLLM `aembedding`, then logs token usage.
- `api/dependencies.py`: Auth and tenant resolution helpers.
- `api/attributes.py`: Optional tenant and bypass tenant decorators.

### Core AI Layer (`core/ai/`)

Shared logic extracted from route handlers to keep endpoints thin and stable:

| File                          | Responsibility                                                                                                                                                                                                                                                                                |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `core/ai/schemas.py`          | Pydantic request/response models for chat and embedding (`ChatRequest`, `ChatSingleResponse`, `EmbeddingRequest`, `EmbeddingResponse`)                                                                                                                                                        |
| `core/ai/utils.py`            | `build_litellm_model`, `normalize_model_type`, `extract_user_id`, `estimate_tokens_if_missing`, `parse_response_format`, `map_litellm_exception_to_http`, provider strategy constants (`PROVIDERS_WITHOUT_RESPONSE_FORMAT`, `PROVIDERS_REQUIRING_MAX_TOKENS`, `ANTHROPIC_DEFAULT_MAX_TOKENS`) |
| `core/ai/db_queries.py`       | `get_settings_by_key`, `get_system_prompt_by_key`                                                                                                                                                                                                                                             |
| `core/ai/sessions.py`         | `resolve_or_create_session` — create or validate chat sessions                                                                                                                                                                                                                                |
| `core/ai/persistence.py`      | Background tasks for message persistence and token usage logging                                                                                                                                                                                                                              |
| `core/ai/file_context.py`     | FileManager client singleton (`file_manager_client`) used by the multimodal transform node                                                                                                                                                                                                    |
| `core/ai/multimodal_utils.py` | MIME classification, raw-byte fetcher, Base64 encoder, OpenAI-compatible content block builders (`image_url`, `input_audio`, document text), provider capability sets (`PROVIDERS_SUPPORTING_VISION`, `PROVIDERS_SUPPORTING_AUDIO`), batch processor `build_media_content_blocks`             |
| `core/ai/local_embeddings.py` | Local embedding helpers for BAAI bge-m3, including settings detection and lazy-loaded model caching for in-process inference                                                                                                                                                                  |
| `core/ai/chat_workflow.py`    | LangGraph chat workflow, `ChatWorkflowState`, `ChatRuntimeContext`, `build_chat_runtime_context`                                                                                                                                                                                              |
| `core/ai/session_title.py`    | Session title auto-generation: `generate_session_title_background`, `schedule_session_title_task`, constants `SESSION_TITLE_PROMPT_NAME`, `SESSION_TITLE_SETTINGS_KEY`                                                                                                                        |

## Framework, Orchestration, and Validation

### FastAPI

- API framework is FastAPI (`main.py` app initialization and route registration).
- Route dependencies and exception handlers use FastAPI dependency injection and middleware pipeline.

### LangGraph

- Chat request orchestration uses `CHAT_WORKFLOW` (compiled graph) in `core/ai/chat_workflow.py`.
- Chat workflow nodes: `normalize_provider` → `prepare_messages` → `multimodal_transform` → [conditional] → `preflight_validation` → `resolve_model`.
- `multimodal_transform` fetches FileManager file bytes, encodes them as Base64, and injects OpenAI-compatible `image_url` / `input_audio` / document-text content blocks into the last user message. LiteLLM's adapter layer translates these to provider-proprietary formats at call time.
- Anthropic requests are routed through an additional `anthropic_transform` node between `multimodal_transform` and `preflight_validation` to enforce `max_tokens` and handle provider-specific constraints.
- `preflight_validation` strips parameters unsupported by the resolved provider (e.g. `response_format` for Anthropic/Ollama) and raises HTTP 400 when a media type (audio or vision) is sent to a provider that does not support it.
- `litellm_messages` carries `List[dict[str, Any]]` — content may be a plain string or a list of typed content blocks (multimodal).
- The workflow is compiled once at module import and reused across requests.

### Pydantic Validation

- Request and response contracts are Pydantic models across chat, settings, and prompt routes.
- Chat endpoint enforces message role values (`system`, `user`, `assistant`, `tool`) and non-empty content.
- Empty message collections are rejected by validation and return standardized validation error responses.

### Automatic Session Title Generation

When `generate_session_title=true` is included in `POST /api/v1/chat/stream` or `POST /api/v1/chat/single`, a background task is enqueued after the response for sessions whose `Title` column is `NULL`.

**Implementation file:** `core/ai/session_title.py`

**Flow:**

1. `schedule_session_title_task` enqueues `generate_session_title_background` via FastAPI `BackgroundTasks`.
2. The task opens its own DB session (`AsyncSessionFactory`) so it never blocks the main request.
3. It re-fetches the session and returns immediately if `Title` is already set (concurrent write guard — title is only ever generated once per session).
4. Looks up the `Session-Title` system prompt (`SESSION_TITLE_PROMPT_NAME`). If the prompt does not exist the task exits silently — **no error is raised**.
5. Fetches AI provider settings using `SESSION_TITLE_SETTINGS_KEY = "QwenAI-qwen3-vl-32b-instruct"`.
6. Calls the AI with `[system: Session-Title prompt, user: first user message]`, no streaming.
7. Strips quotes and whitespace from the response; truncates to 255 characters.
8. In a **single commit**: persists `AiChatSession.Title` and writes an `AiTokenUsageLog` row for the title call (endpoint label `"/api/v1/chat/session-title"`).

**Required setup:**

| Resource                   | Value                                                                                                                                   |
| -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| System prompt `Name`       | `Session-Title`                                                                                                                         |
| System prompt `PromptText` | e.g. `"Generate a concise title (≤5 words) for this conversation based on the user's first message. Return only the title, no quotes."` |
| Provider settings `Key`    | `QwenAI-qwen3-vl-32b-instruct`                                                                                                          |

**Error handling:** all exceptions are caught and logged at `ERROR` level; the background task never propagates exceptions back to the caller.

### Multimodal File Attachments

When `file_ids` is provided in a chat request the `multimodal_transform` node handles the full encoding pipeline:

1. **Metadata fetch** — `file_manager_client.get_files_by_ids` retrieves name, extension, MIME type, and URLs for every file ID.
2. **Byte download** — `fetch_file_bytes_with_fallback` downloads raw bytes from `external_url` (CDN). If that request fails, it retries using the internal `url` field.
3. **MIME classification** — `classify_media_type` groups each file into `image`, `audio`, `document`, or `unknown`.
4. **Block encoding** (provider-aware):
   - Images → `{"type": "image_url", "image_url": {"url": "data:<mime>;base64,..."}}`
   - Audio (OpenAI / Gemini / Groq) → `{"type": "input_audio", "input_audio": {"data": "<base64>", "format": "mp3"}}` — file bytes downloaded and base64-encoded.
   - Audio (Qwen omni / Dashscope) → `{"type": "input_audio", "input_audio": {"data": "<https-url>", "format": "mp3"}}` — CDN URL passed directly; Dashscope fetches the file server-side. The `data` field accepts a URL, avoiding an unnecessary download.
   - Anthropic/Claude audio → text-context fallback (Claude has no native audio API).
   - Documents → `{"type": "text", "text": "Attached document: name.pdf (https://...)"}` (URL-as-context fallback)
   - Unknown MIME types are silently skipped.
5. **Payload injection** — The last user message's `content` string is replaced with a typed list: `[{"type":"text","text":"<prompt>"}, <media blocks...>]`.

- If no user message exists (for example system prompt plus `file_ids` only), AI.API appends a new `user` message whose `content` is the generated media block list.

6. **Qwen omni extra field** — When the final message list contains an `input_audio` block and the provider is Qwen, `extra_body={"modalities": ["text"]}` is added to the `acompletion` call. Dashscope's compatible-mode endpoint requires this field or returns HTTP 400.
7. **Capability guard** — `preflight_validation` raises HTTP 400 when the resolved provider does not support the media type (e.g. audio sent to a provider with no audio capability).
8. **LiteLLM adapter** — The standard OpenAI-format blocks are passed as-is to `acompletion`. LiteLLM translates them into the proprietary wire format for Claude (Anthropic), Gemini, or any other configured provider automatically.

**Provider capability matrix:**

| Provider (`provider_normalized`) | Vision (images) | Audio              |
| -------------------------------- | --------------- | ------------------ |
| `openai`                         | ✅              | ✅                 |
| `azure`                          | ✅              | ❌                 |
| `anthropic`                      | ✅              | ❌ (text fallback) |
| `gemini`                         | ✅              | ✅                 |
| `groq`                           | ✅              | ❌                 |
| `mistral`                        | ✅              | ❌                 |
| `ollama`                         | ✅              | ❌                 |

> **Note for Qwen omni:** Qwen's raw provider strings (`qwen`, `qwenai`, `alibaba`, `dashscope`) are detected by `QWEN_RAW_PROVIDERS` in `multimodal_utils.py`. Audio is sent as `input_audio` with a CDN URL in the `data` field (Dashscope fetches the file server-side). The request also includes `extra_body={"modalities": ["text"]}`, which Dashscope requires when audio is present. Vision models (e.g. `qwen-vl-plus`) use standard `image_url` blocks.

### Core Layer

- `core/config.py`: Loads appsettings json into typed settings.
- `core/database.py`: Async SQLAlchemy engine and session setup.
- `core/security.py`: Auth dependency wiring using shared package.
- `core/exceptions.py`: Re-exports shared ProblemDetails handlers.
- `core/logger.py`: Logging setup and logger helper.

### Models Layer

- `models/ai_provider_setting.py`: Tenant and global model provider settings.
- `models/ai_system_prompt.py`: Named system prompts.
- `models/ai_chat_session.py`: Chat sessions.
- `models/ai_chat_message.py`: Messages inside sessions.
- `models/ai_chat_message_file.py`: Message file links.
- `models/ai_token_usage_log.py`: Token usage logs.

### Shared Python Package

The service depends on:

- `src/Shared/ihsandev_shared`

This package provides shared config parsing, auth helpers, exception handling, logging, DB utilities, and base service clients.

## Authentication and Authorization

The service supports dual auth modes.

### Internal Service Mode

Headers:

- `X-Service-Secret`
- `X-Service-Name`

Secret and allow-list are defined in `appsettings.json` under `ServiceCommunication`.

Service-to-service calls are treated as internal trusted calls and are allowed to access admin-level AI configuration endpoints.

### End User Mode

Header:

- `Authorization: Bearer <token>`

JWT validation uses `Jwt` settings from `appsettings.json`.

Authorization behavior for configuration endpoints:

- `settings` and `prompts` endpoints require either:
  - an internal service-to-service request (`X-Service-Secret` and `X-Service-Name`), or
  - a user JWT containing the `SuperAdmin` role.

Authorization behavior for chat and observability endpoints:

- `POST /api/v1/chat/stream` and `POST /api/v1/chat/single` accept LLM chat requests from authenticated users and internal service calls.
- `POST /api/v1/embedding` accepts embedding requests from authenticated users and internal service calls.
- `GET /api/v1/chat-sessions/`, `GET /api/v1/chat-messages/`, `GET /api/v1/chat-message-files/`, `GET /api/v1/token-usage-logs/`, and `GET /api/v1/token-usage-logs/stats` require internal service authentication or `SuperAdmin`.

## Tenant Handling

Tenant ID convention in AI service is string.

Resolution order:

1. `x-tenant-id` header.
2. `tenantId` claim from JWT.

For endpoints decorated with optional tenant behavior, missing tenant does not fail and route logic can operate in global scope.

Chat endpoint tenant behavior:

- `POST /api/v1/chat/stream` and `POST /api/v1/chat/single` are optional-tenant endpoints.
- If `x-tenant-id` or JWT `tenantId` is provided, chat uses tenant plus global settings lookup.
- If tenant context is missing, settings are resolved by `Key` regardless of `TenantId`, while prompts continue to use global scope.
- Chat sessions created without tenant context are stored under the service global chat tenant scope (`global`) so persistence remains valid.

## Main Endpoints

- `GET /health`
- `POST /api/v1/chat/stream`
- `POST /api/v1/chat/single`
- `GET /api/v1/settings/`
- `GET /api/v1/settings/by-key/{key}`
- `GET /api/v1/settings/{setting_id}`
- `POST /api/v1/settings/`
- `PUT /api/v1/settings/{setting_id}`
- `DELETE /api/v1/settings/{setting_id}`
- `GET /api/v1/prompts/`
- `GET /api/v1/prompts/{prompt_id}`
- `POST /api/v1/prompts/`
- `PUT /api/v1/prompts/{prompt_id}`
- `DELETE /api/v1/prompts/{prompt_id}`
- `GET /api/v1/chat-sessions/`
- `PATCH /api/v1/chat-sessions/{session_id}`
- `DELETE /api/v1/chat-sessions/{session_id}`
- `GET /api/v1/chat-messages/`
- `GET /api/v1/chat-message-files/`
- `GET /api/v1/token-usage-logs/`
- `GET /api/v1/token-usage-logs/stats`
- `POST /api/v1/embedding`

### Chat Response Modes

- `POST /api/v1/chat/stream`: Server-Sent Events streaming mode that emits incremental `content` chunks, then emits a final completion metadata event, and finally ends with `[DONE]`.
- `POST /api/v1/chat/single`: single-response mode that waits for completion and returns one JSON payload.

Shared request fields for both chat endpoints include:

- `settings_key`: AI provider settings key.
- `messages`: ordered chat messages.
- `system_prompt_key` (optional): resolved to `AiSystemPrompt`.
- `file_ids` (optional): FileManager file IDs. The multimodal transform node fetches each file's raw bytes and encodes them as OpenAI-compatible content blocks (`image_url` for images, `input_audio` for audio, text-context for documents). External CDN URL is tried first; internal FileManager URL is used as fallback.
- `max_completion_tokens` (optional): explicit output token cap forwarded to LiteLLM as `max_tokens`.
- `generate_session_title` (optional): defaults to `false`. When `true`, schedules background session title generation after a successful chat response.

Streaming completion metadata payload fields:

- `session_id`: chat session UUID.
- `done`: always `true` for the completion metadata event.
- `finish_reason`: provider finish reason when available (for example `stop`, `length`, `max_tokens`).
- `is_truncated`: `true` when finish reason indicates truncation (`length` or `max_tokens`), otherwise `false`.

Single-response payload fields:

- `session_id`: chat session UUID.
- `content`: full assistant response text.
- `prompt_tokens`: prompt token count.
- `completion_tokens`: completion token count.
- `total_tokens`: total token count.

### Chat Message Persistence (Debug Fidelity)

For both `POST /api/v1/chat/stream` and `POST /api/v1/chat/single`, AI.API persists chat messages with debug-first fidelity:

- Saves every message in the exact `litellm_messages` list that is sent to the model for that turn.
- Preserves message roles (`system`, `user`, `assistant`, `tool`) from the model-input payload.
- When a message `content` is structured (for example multimodal blocks such as `input_audio`, `audio_url`, `image_url`), it is serialized to a JSON string and stored in `AiChatMessage.Content`.
- Saves the generated assistant output as a separate `assistant` row after the model-input rows.

This makes database records match what the model received, which simplifies payload-level debugging for multimodal and transformed prompts.

Filter and pagination support on list endpoints:

- `chat-sessions`: `user_id`, `title`, `created_from`, `created_to`, `skip`, `limit`.
- `chat-messages`: `session_id`, `role`, `created_from`, `created_to`, `skip`, `limit`.
- `chat-message-files`: `message_id`, `file_id`, `skip`, `limit`.
- `token-usage-logs`: `user_id`, `model_name`, `endpoint`, `created_from`, `created_to`, `skip`, `limit`.

### Token Usage Statistics Endpoint

`GET /api/v1/token-usage-logs/stats`

Returns aggregate token usage statistics. Requires internal service auth or `SuperAdmin`. Decorated with `@optional_tenant` — omitting `x-tenant-id` returns global stats across all tenants.

**Query parameters:** `model_name`, `endpoint`, `created_from`, `created_to` (same substring/range semantics as the list endpoint; no pagination).

**Response model (`TokenUsageStatsResponse`):**

| Field                    | Type                     | Description                                          |
| ------------------------ | ------------------------ | ---------------------------------------------------- |
| `total_tokens`           | `int`                    | Sum of all `TotalTokens`                             |
| `prompt_tokens`          | `int`                    | Sum of all `PromptTokens`                            |
| `completion_tokens`      | `int`                    | Sum of all `CompletionTokens`                        |
| `total_requests`         | `int`                    | Count of matching log rows                           |
| `avg_tokens_per_request` | `float`                  | `total_tokens / total_requests` (0 if no rows)       |
| `tokens_by_model`        | `TokensByModelItem[]`    | Per-model breakdown, ordered by total tokens desc    |
| `tokens_by_endpoint`     | `TokensByEndpointItem[]` | Per-endpoint breakdown, ordered by total tokens desc |
| `tokens_over_time`       | `TokensOverTimeItem[]`   | Daily aggregation ordered by date asc                |

**Implementation:** runs four independent async SQLAlchemy queries (totals, by-model group-by, by-endpoint group-by, daily cast-to-Date group-by). All filters are applied to each query via a shared `_apply_filters` helper inside the route function.

Settings and prompts use optional tenant behavior:

- With `x-tenant-id` or a JWT `tenantId` claim, item lookups and mutations stay inside that tenant scope.
- Without tenant context:
  - settings item lookups and mutations are not restricted by tenant when called as service or SuperAdmin.
  - prompt item lookups and mutations are not restricted by tenant when called as service or SuperAdmin.

List endpoint behavior for both `GET /api/v1/settings/` and `GET /api/v1/prompts/`:

- `scope=all` (default):
  - With tenant context: returns tenant rows plus global rows where `TenantId` is null.
  - Without tenant context: returns all rows.
- `scope=tenant`:
  - With tenant context: returns only rows for the resolved tenant.
  - Without tenant context: returns all rows where `TenantId` is not null.
- `scope=global`: returns only rows where `TenantId` is null.
- Ordering for list endpoints that sort by creation time is newest rows first by `CreatedAt` descending, with `Id` descending as a deterministic tie-breaker.

## Runtime and Startup

At startup, the service:

1. Ensures the target database exists.
2. Runs Alembic `upgrade head`.
3. Runs schema bootstrap with SQLAlchemy metadata create-all for missing tables.

If Alembic upgrade fails, the failure is logged and startup continues to the schema bootstrap step.

This combination prevents first-run failures when revision files are missing while still supporting migration-based updates.

## Configuration

Main config file:

- `src/Services/AI/AI.API/appsettings.json`

Important sections:

- `Urls`
- `DatabaseSettings`
- `Jwt`
- `Cors`
- `ServiceCommunication`
- `FileManagerSettings`

Provider setting behavior for chat endpoints:

- `Provider` is handled case-insensitively before calling LiteLLM.
- Known aliases are normalized (example: `OpenAI` becomes `openai`, `AzureOpenAI` becomes `azure`, `QwenAI` becomes `openai`).
- `ApiBaseUrl` stored on the `AiProviderSettings` record is passed to LiteLLM as `api_base` (e.g. set this to the Qwen OpenAI-compatible URL for Qwen providers).
- `MaxCompletionTokens` stored on `AiProviderSettings` is used as the default limit; the caller can override it per-request via the `max_completion_tokens` field.
- `Temperature`, `TopP`, `FrequencyPenalty`, and `PresencePenalty` stored on `AiProviderSettings` are forwarded to LiteLLM when set.
- If `ModelName` already includes a provider prefix (`provider/model`), the value is used as-is.

FileManager context enrichment behavior for chat endpoints:

- Request payload supports `file_ids` as integer FileManager IDs.
- AI service calls shared `FileManagerServiceClient.get_files_by_ids()` with tenant forwarding.
- Retrieved files are transformed into multimodal content blocks and injected into the last user message content payload.

## Development and Testing

Run script:

- `run-development-instance.bat`
- `run-development-instance.mjs`

Virtual environment setup:

- `setup_venv.py`

Tests:

- `tests/` directory
- `tests/test_chat.py` includes coverage for chat stream and single endpoints, provider-failure paths, token fallback estimation, file-context injection, and LangGraph orchestration behavior.
- `tests/test_settings.py` and `tests/test_system_prompts.py` cover CRUD and scoped lookup behavior.
- `tests/test_chat_sessions.py`, `tests/test_chat_messages.py`, `tests/test_chat_message_files.py`, and `tests/test_token_usage_logs.py` cover list endpoints, filter validation, and pagination-bound validation.
- `tests/conftest.py` uses dependency overrides to simulate authenticated SuperAdmin access and deterministic tenant context for route tests.

## Troubleshooting Notes

- For Qwen models routed through the OpenAI-compatible provider path (example: `openai/qwen3-omni-flash`), LiteLLM may emit debug logs saying the model is not mapped in `model_prices_and_context_window.json`.
- These messages affect cost estimation metadata only and do not mean chat generation failed when the API response is `200 OK`.
- AI.API startup logging suppresses verbose `litellm` and `LiteLLM` debug channels to reduce this noise in local logs.

## Related Docs

- `Doc/AI_SERVICE_MIGRATION_GUIDE.md`
- `Doc/PYTHON_SHARED_LIBRARY_GUIDE.md`
- `Doc/SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md`
- `Doc/SHARED_IDENTITY_SERVICE_GUIDE.md`
