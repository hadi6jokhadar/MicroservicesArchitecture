# AI Service Migration Guide

## Scope

This document explains how the AI Python service manages database creation, migration execution, and schema bootstrap.

Service path:

- `src/Services/AI/AI.API`

## Migration Stack

The AI service uses:

1. PostgreSQL
2. SQLAlchemy async engine
3. Alembic for migration execution
4. Metadata bootstrap for missing tables

## Model Authoring Standard (Required)

When creating or updating AI service ORM models, use SQLAlchemy 2.0+ Declarative Mapping style.

Required rules:

1. Use `Mapped[...]` type annotations for every mapped attribute.
2. Use `mapped_column(...)` instead of legacy `Column(...)` in model classes.
3. Prefer UUID primary keys (`UUID(as_uuid=True)`) for distributed scalability and cross-service consistency.
4. Use Alembic revisions for schema evolution on existing databases.

Example pattern:

```python
import uuid
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column

Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
```

## Startup Flow

On service startup, the lifecycle does the following in order:

1. Ensure database exists
2. Run Alembic upgrade to head
3. Ensure schema exists using metadata create-all

Implementation entry points:

- `main.py` lifespan startup block
- `core/database.py`
- `alembic/env.py`

## What Each Step Means

### 1) Ensure database exists

`ensure_database_exists()` creates the PostgreSQL database if it does not exist yet.

Use case:

- Fresh local machine with no `ai` database.

### 2) Alembic upgrade head

Alembic applies pending revisions to reach the latest migration head.

Important behavior:

- Runs every startup.
- Applies only pending revisions.
- Does not reapply already applied revisions.
- Startup resolves the Alembic executable relative to the current Python interpreter and falls back to `alembic` from PATH.
- If migration execution fails, the error is logged and startup continues to schema bootstrap.

### 3) Schema bootstrap create-all

`ensure_schema_exists()` runs `Base.metadata.create_all`.

Important behavior:

- Creates missing tables only.
- Does not alter existing columns.
- Does not replace true migration files for structural changes.

## Why Both Alembic and create-all Are Used

Alembic is the source of truth for schema evolution.
Metadata create-all is a safety net for first run scenarios where revision files are missing or incomplete.

This avoids runtime errors like relation does not exist on insert.

## When You Must Create New Alembic Revisions

Create a new revision whenever model changes affect existing schema, for example:

1. Column type change
2. Column rename
3. New constraints
4. Index changes
5. Table or column removals

create-all will not safely perform these upgrades on existing tables.

## Recent Schema Update

Current AI schema stores user identity references as integer IDs in:

1. `AiChatSession.UserId`
2. `AiTokenUsageLog.UserId`

This change is tracked by Alembic revision:

- `d27084ec4fea_change_userid_to_integer.py`

If a local environment was created before this revision, ensure `alembic upgrade head` runs successfully before testing chat session or token usage queries.

### August 2026 — Two Additional Columns on `AiProviderSettings`/`AiChatSession`/`AiTokenUsageLog`

1. `AiProviderSettings.NoSpeechProbThreshold` (nullable `Float`, only meaningful when `ModelType = Audio`) — lets an admin override the caller's default hallucination-filter threshold for ASR word-level timestamps per settings row instead of it being hardcoded in the calling service. Tracked by revision `a4d8f2c6e1b9_add_no_speech_prob_threshold_to_ai_provider_settings.py`.
2. `AiChatSession.PipelineRunId` and `AiTokenUsageLog.PipelineRunId` (both nullable `String(200)`, indexed) — caller-supplied correlation id grouping otherwise-unrelated AI calls from one batch/pipeline run, so they can be found together later (`GET /api/v1/chat-sessions/?pipeline_run_id=<id>`). Tracked by revision `b6e1a9c3d7f2_add_pipeline_run_id_to_chat_session_and_token_usage_log.py`.

**Pitfall found during this change — confirmed twice, not a one-off:** `api/routes/chat_sessions.py`'s `ChatSessionResponse` is a Pydantic response schema **separate** from the `AiChatSession` SQLAlchemy model. Adding `PipelineRunId` to the model alone did not make it appear in `GET /api/v1/ai/chat-sessions/`'s JSON output — FastAPI silently drops any field a response model doesn't explicitly declare, even when the underlying ORM object has the attribute populated. `PipelineRunId` had to be added to `ChatSessionResponse` as a second, separate edit, plus a `pipeline_run_id` query filter on `list_chat_sessions`. A follow-up consistency sweep found the **identical bug independently** in the sibling file `api/routes/token_usage_logs.py`: `TokenUsageLogResponse` also never declared `PipelineRunId`, silently dropping it from `GET /api/v1/token-usage-logs/` too — fixed the same way (field added to the response schema, `pipeline_run_id` filter added to `list_token_usage_logs`). **Whenever a new column is added to `AiChatSession`, `AiTokenUsageLog`, or any model with its own hand-written response schema (e.g. `AiProviderSettings` ↔ `ProviderSettingsResponse` in `api/routes/settings.py`), check for and update that route's response schema too — it is not automatically kept in sync with the ORM model, and this codebase has now hit this exact mistake in two separate route files.**

If a local environment was created before these two revisions, ensure `alembic upgrade head` runs successfully before testing `NoSpeechProbThreshold` overrides or `pipeline_run_id` filtering.

## Standard Migration Commands

From `src/Services/AI/AI.API`:

```powershell
.\venv\Scripts\python.exe -m alembic revision --autogenerate -m "describe_change"
.\venv\Scripts\python.exe -m alembic upgrade head
```

## Alembic Environment Notes

- `alembic/env.py` includes path setup so service modules can be resolved during migration commands.
- Keep model imports discoverable so autogenerate can detect metadata changes.

## Troubleshooting

### Error: relation does not exist

Cause:

- Table missing in DB.

Actions:

1. Confirm startup completed successfully.
2. Run one-time bootstrap if needed:

```powershell
.\venv\Scripts\python.exe -c "import asyncio; from core.database import ensure_database_exists, ensure_schema_exists; asyncio.run(ensure_database_exists()); asyncio.run(ensure_schema_exists())"
```

3. If table should come from Alembic revision, verify revision files exist and are applied.

### Error after model type change

Cause:

- Existing table shape differs from updated model.

Action:

- Create and apply an Alembic revision for that change.

## Recommended Practice

1. Keep Alembic revisions for all structural changes.
2. Keep startup create-all safety net enabled for developer experience.
3. Validate startup logs after schema-related changes.
4. Add tests for any migration-impacting model updates.
