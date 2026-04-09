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

## High-Level Architecture

### API Layer

- `main.py`: FastAPI app startup, middleware, exception handlers, router registration.
- `api/routes/chat.py`: Streaming chat endpoint.
- `api/routes/settings.py`: AI provider settings CRUD.
- `api/routes/system_prompts.py`: System prompt CRUD.
- `api/dependencies.py`: Auth and tenant resolution helpers.
- `api/attributes.py`: Optional tenant and bypass tenant decorators.

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

### End User Mode

Header:

- `Authorization: Bearer <token>`

JWT validation uses `Jwt` settings from `appsettings.json`.

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
- `POST /api/v1/settings/`
- `GET /api/v1/prompts/`
- `POST /api/v1/prompts/`

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

## Development and Testing

Run script:

- `run-development-instance.bat`

Virtual environment setup:

- `setup_venv.py`

Tests:

- `tests/` directory

## Related Docs

- `Doc/AI_SERVICE_MIGRATION_GUIDE.md`
- `Doc/PYTHON_SHARED_LIBRARY_GUIDE.md`
- `Doc/SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md`
- `Doc/SHARED_IDENTITY_SERVICE_GUIDE.md`
