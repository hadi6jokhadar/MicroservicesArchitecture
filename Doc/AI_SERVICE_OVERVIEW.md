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

## High-Level Architecture

### API Layer

- `main.py`: FastAPI app startup, middleware, exception handlers, router registration.
- `api/routes/chat.py`: Streaming and single-response **LLM chat** endpoints. Uses `core/ai/chat_workflow.py` LangGraph pipeline.
- `api/routes/settings.py`: AI provider settings CRUD.
- `api/routes/system_prompts.py`: System prompt CRUD.
- `api/routes/chat_sessions.py`: Chat session listing with filtering and pagination.
- `api/routes/chat_messages.py`: Chat message listing with filtering and pagination.
- `api/routes/chat_message_files.py`: Chat message to file relation listing.
- `api/routes/token_usage_logs.py`: Token usage log listing with filtering and pagination.
- `api/dependencies.py`: Auth and tenant resolution helpers.
- `api/attributes.py`: Optional tenant and bypass tenant decorators.

### Core AI Layer (`core/ai/`)

Shared logic extracted from route handlers to keep endpoints thin and stable:

| File                          | Responsibility                                                                                                                                                                                                                                                                                |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `core/ai/schemas.py`          | Pydantic request/response models for chat                                                                                                                                                                                                                                                     |
| `core/ai/utils.py`            | `build_litellm_model`, `normalize_model_type`, `extract_user_id`, `estimate_tokens_if_missing`, `parse_response_format`, `map_litellm_exception_to_http`, provider strategy constants (`PROVIDERS_WITHOUT_RESPONSE_FORMAT`, `PROVIDERS_REQUIRING_MAX_TOKENS`, `ANTHROPIC_DEFAULT_MAX_TOKENS`) |
| `core/ai/db_queries.py`       | `get_settings_by_key`, `get_system_prompt_by_key`                                                                                                                                                                                                                                             |
| `core/ai/sessions.py`         | `resolve_or_create_session` — create or validate chat sessions                                                                                                                                                                                                                                |
| `core/ai/persistence.py`      | Background tasks for message persistence and token usage logging                                                                                                                                                                                                                              |
| `core/ai/file_context.py`     | FileManager client singleton (`file_manager_client`) used by the multimodal transform node                                                                                                                                                                                                    |
| `core/ai/multimodal_utils.py` | MIME classification, raw-byte fetcher, Base64 encoder, OpenAI-compatible content block builders (`image_url`, `input_audio`, document text), provider capability sets (`PROVIDERS_SUPPORTING_VISION`, `PROVIDERS_SUPPORTING_AUDIO`), batch processor `build_media_content_blocks`             |
| `core/ai/chat_workflow.py`    | LangGraph chat workflow, `ChatWorkflowState`, `ChatRuntimeContext`, `build_chat_runtime_context`                                                                                                                                                                                              |

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

- `settings` and `system-prompts` endpoints require either:
  - an internal service-to-service request (`X-Service-Secret` and `X-Service-Name`), or
  - a user JWT containing the `SuperAdmin` role.

Authorization behavior for chat and observability endpoints:

- `POST /api/v1/chat/stream` and `POST /api/v1/chat/single` accept LLM chat requests from authenticated users and internal service calls.
- `POST /api/v1/asr/transcribe` accepts audio transcription requests from authenticated users and internal service calls. Requires the `AiProviderSettings.ModelType` to be `Audio`.
- `GET /api/v1/chat-sessions/`, `GET /api/v1/chat-messages/`, `GET /api/v1/chat-message-files/`, and `GET /api/v1/token-usage-logs/` require internal service authentication or `SuperAdmin`.

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
- `GET /api/v1/chat-messages/`
- `GET /api/v1/chat-message-files/`
- `GET /api/v1/token-usage-logs/`

### Chat Response Modes

- `POST /api/v1/chat/stream`: Server-Sent Events streaming mode that emits incremental `content` chunks, then emits a final completion metadata event, and finally ends with `[DONE]`.
- `POST /api/v1/chat/single`: single-response mode that waits for completion and returns one JSON payload.

Shared request fields for both chat endpoints include:

- `settings_key`: AI provider settings key.
- `messages`: ordered chat messages.
- `system_prompt_key` (optional): resolved to `AiSystemPrompt`.
- `file_ids` (optional): FileManager file IDs. The multimodal transform node fetches each file's raw bytes and encodes them as OpenAI-compatible content blocks (`image_url` for images, `input_audio` for audio, text-context for documents). External CDN URL is tried first; internal FileManager URL is used as fallback.
- `max_completion_tokens` (optional): explicit output token cap forwarded to LiteLLM as `max_tokens`.

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

Filter and pagination support on list endpoints:

- `chat-sessions`: `user_id`, `title`, `created_from`, `created_to`, `skip`, `limit`.
- `chat-messages`: `session_id`, `role`, `created_from`, `created_to`, `skip`, `limit`.
- `chat-message-files`: `message_id`, `file_id`, `skip`, `limit`.
- `token-usage-logs`: `user_id`, `model_name`, `endpoint`, `created_from`, `created_to`, `skip`, `limit`.

Settings and prompts use optional tenant behavior:

- With `x-tenant-id` or a JWT `tenantId` claim, item lookups and mutations stay inside that tenant scope.
- Without tenant context:
  - settings item lookups and mutations are not restricted by tenant when called as service or SuperAdmin.
  - prompt item lookups and mutations are restricted to global prompts (`TenantId` is null).

List endpoint behavior for both `GET /api/v1/settings/` and `GET /api/v1/prompts/`:

- `scope=all` (default):
  - With tenant context: returns tenant rows plus global rows where `TenantId` is null.
  - Without tenant context: returns all rows.
- `scope=tenant`:
  - With tenant context: returns only rows for the resolved tenant.
  - Without tenant context: returns all rows where `TenantId` is not null.
- `scope=global`: returns only rows where `TenantId` is null.

## Runtime and Startup

At startup, the service:

1. Ensures the target database exists.
2. Runs Alembic `upgrade head`.
3. Runs schema bootstrap with SQLAlchemy metadata create-all for missing tables.

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
- Chat endpoints automatically route by model type. `ModelType=Audio` uses transcription under the same `/api/v1/chat/*` endpoints, while non-audio types continue to use chat completions.
- For `ModelType=Audio` with DashScope native ASR URL (`/api/v1/services/audio/asr/transcription`), the service calls DashScope directly instead of LiteLLM OpenAI transcription routing to avoid invalid URL composition like `/transcription/audio/transcriptions`.
- DashScope ASR direct calls normalize `ModelName` by removing a provider prefix (such as `openai/`) and converting to lower-case before sending the request.

FileManager context enrichment behavior for chat endpoints:

- Request payload supports `file_ids` as integer FileManager IDs.
- AI service calls shared `FileManagerServiceClient.get_files_by_ids()` with tenant forwarding.
- Resolved file URLs are injected as an additional user context message immediately before the last user message.

## Development and Testing

Run script:

- `run-development-instance.bat`

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
