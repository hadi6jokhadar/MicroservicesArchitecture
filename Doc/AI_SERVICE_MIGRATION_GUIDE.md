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
