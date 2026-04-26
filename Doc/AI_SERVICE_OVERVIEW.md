# AI Service Overview

## Purpose

The AI service is a Python FastAPI microservice that acts as an AI gateway inside the MicroservicesArchitecture solution.
It centralizes model access, tenant-aware settings, prompt management, and streaming chat responses.

Service path:

- `src/Services/AI/AI.API`

Default local URL:

- `http://localhost:5008`

## What the Service Does

1. Accepts chat requests and streams model responses.
2. Reads provider settings per tenant, with global fallback.
3. Stores and retrieves system prompts.
4. Logs token usage for auditing and cost tracking.
5. Supports internal service calls using shared-secret headers.
6. Supports end-user requests using JWT bearer tokens.
7. Orchestrates chat request preparation using LangGraph before calling LiteLLM.
8. Enforces request and payload validation using Pydantic models.
9. Resolves attached FileManager file IDs into URL context before model invocation.
10. Exposes read endpoints for sessions, messages, message files, and token usage logs.

## High-Level Architecture

### API Layer

- `main.py`: FastAPI app startup, middleware, exception handlers, router registration.
- `api/routes/chat.py`: Streaming chat endpoint with Pydantic request models and LangGraph orchestration pipeline.
- `api/routes/settings.py`: AI provider settings CRUD.
- `api/routes/system_prompts.py`: System prompt CRUD.
- `api/routes/chat_sessions.py`: Chat session listing with filtering and pagination.
- `api/routes/chat_messages.py`: Chat message listing with filtering and pagination.
- `api/routes/chat_message_files.py`: Chat message to file relation listing.
- `api/routes/token_usage_logs.py`: Token usage log listing with filtering and pagination.
- `api/dependencies.py`: Auth and tenant resolution helpers.
- `api/attributes.py`: Optional tenant and bypass tenant decorators.

## Framework, Orchestration, and Validation

### FastAPI

- API framework is FastAPI (`main.py` app initialization and route registration).
- Route dependencies and exception handlers use FastAPI dependency injection and middleware pipeline.

### LangGraph

- Chat request orchestration uses a compiled LangGraph workflow in `api/routes/chat.py`.
- Current workflow nodes prepare message payloads and resolve provider model identifiers before LiteLLM invocation.

### Pydantic Validation

- Request and response contracts are Pydantic models across chat, settings, and prompt routes.
- Chat endpoint enforces message role values (`system`, `user`, `assistant`, `tool`) and non-empty content.
- Empty message collections are rejected by validation and return standardized validation error responses.

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

- `POST /api/v1/chat/stream` accepts authenticated user calls and internal service calls.
- `GET /api/v1/chat-sessions/`, `GET /api/v1/chat-messages/`, `GET /api/v1/chat-message-files/`, and `GET /api/v1/token-usage-logs/` require internal service authentication or `SuperAdmin`.

## Tenant Handling

Tenant ID convention in AI service is string.

Resolution order:

1. `x-tenant-id` header.
2. `tenantId` claim from JWT.

For endpoints decorated with optional tenant behavior, missing tenant does not fail and route logic can operate in global scope.

## Main Endpoints

- `GET /health`
- `POST /api/v1/chat/stream`
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

Provider setting behavior for chat streaming:

- `Provider` is handled case-insensitively before calling LiteLLM.
- Known aliases are normalized (example: `OpenAI` becomes `openai`, `AzureOpenAI` becomes `azure`).
- If `ModelName` already includes a provider prefix (`provider/model`), the value is used as-is.

FileManager context enrichment behavior for chat streaming:

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
- `tests/test_chat.py` includes coverage for LangGraph orchestration and chat request validation.

## Related Docs

- `Doc/AI_SERVICE_MIGRATION_GUIDE.md`
- `Doc/PYTHON_SHARED_LIBRARY_GUIDE.md`
- `Doc/SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md`
- `Doc/SHARED_IDENTITY_SERVICE_GUIDE.md`
