# Python Shared Library Guide

## Overview

The shared Python package is located at:

- `src/Shared/ihsandev_shared`

It provides reusable infrastructure for Python services, similar to shared libraries used by .NET services in this repository.

Primary current consumer:

- `src/Services/AI/AI.API`

## Package Structure

- `ihsandev_shared/config.py`
- `ihsandev_shared/database.py`
- `ihsandev_shared/exceptions.py`
- `ihsandev_shared/logger.py`
- `ihsandev_shared/security.py`
- `ihsandev_shared/__init__.py`
- `ihsandev_shared/clients/base_client.py`
- `ihsandev_shared/clients/file_manager_client.py`
- `ihsandev_shared/clients/__init__.py`

## Modules and Responsibilities

### config.py

Provides appsettings loading and typed settings models.

Key features:

1. Reads `appsettings.json`.
2. Merges environment file `appsettings.{ASPNETCORE_ENVIRONMENT}.json`.
3. Exposes common settings models:
   - DatabaseSettings
   - JwtSettings
   - ServiceCommunicationSettings
   - CorsSettings
   - LoggingSettings
4. Uses Pydantic v2 settings configuration with `model_config = SettingsConfigDict(...)` instead of class-based `Config`.

### database.py

Provides shared DB utilities.

Key features:

1. Parses .NET style PostgreSQL connection strings into async SQLAlchemy URL format.
2. Ensures target database exists before service startup work.

### security.py

Provides shared auth helpers.

Key features:

1. JWT verification helper.
2. Dependency factory for dual auth mode:
   - Internal service auth via shared secret headers
   - End-user auth via bearer JWT
3. Optional AllowedServices validation for service-to-service calls.

### exceptions.py

Provides shared ProblemDetails handlers.

Key features:

1. Shared AppException class.
2. Shared handlers for:
   - AppException
   - HTTPException
   - RequestValidationError
   - Unexpected exceptions
3. .NET style response shape:
   - status
   - title
   - detail
   - instance
   - traceId
   - errors (validation cases)

### logger.py

Provides shared logging setup.

Key features:

1. Level mapping compatible with appsettings values.
2. Console logging.
3. File logging with daily rotation.
4. Reusable logger factory.

### clients/base_client.py

Provides base HTTP client for internal service communication.

Key features:

1. Adds service auth headers automatically.
2. Supports common HTTP methods (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`).
3. Supports tenant header forwarding.
4. Handles request and response errors consistently.

### clients/file_manager_client.py

Provides typed FileManager client built on top of `BaseServiceClient`.

Key features:

1. `get_file_by_id(file_id, tenant_id=None)` calls `GET /api/filemanager/internal/files/{file_id}`.
2. `get_files_by_ids(file_ids, tenant_id=None)` calls `GET /api/filemanager/internal/files/batch` using repeated `fileIds` query params.
3. `change_temp_status(file_id, usage_area, row_id, is_new, tenant_id=None)` calls `PATCH /api/filemanager/internal/files/{file_id}/temp-status?usageArea=...&rowId=...&isNew=...`.
   - `is_new=True` → add usage row (file becomes permanent). `is_new=False` → remove usage row (file becomes temp when no usages remain).
   - **Changed in v3.2.0**: added explicit `is_new` flag — replaces the old toggle pattern that caused incorrect results on retries (see `FILE_MANAGER.md` § File Usage Tracking).
4. Mirrors the .NET `IFileManagerServiceClient` service-to-service contract used by backend services.

### **init**.py exports

The package-level exports include:

1. Shared settings, auth, exception, logging, and database helpers.
2. `BaseServiceClient` and `FileManagerServiceClient` for downstream internal HTTP calls.

## Integration Pattern in Services

A Python service typically:

1. Defines service-specific settings by extending shared base settings.
2. Builds auth dependency using shared security factory.
3. Uses shared exception handlers in FastAPI app registration.
4. Uses shared DB helpers for database bootstrap.
5. Uses shared base client for downstream service calls.
6. Uses `FileManagerServiceClient` when file metadata or temp status operations are needed.

## Versioning and Packaging

Package metadata is in:

- `src/Shared/ihsandev_shared/pyproject.toml`

Current setup uses editable install in service requirements, which keeps service code synced with local shared package changes.

## Python Interpreter Rule

When working in a Python service that contains a local virtual environment, always use the interpreter from that service's `venv` folder.

For `src/Services/AI/AI.API`, use:

- `venv\Scripts\python.exe`

Do not use system Python for service commands such as:

1. Running tests
2. Starting the service
3. Running Alembic commands
4. Installing packages into the service environment

Reason:

- The AI service depends on editable local packages such as `src/Shared/ihsandev_shared`, and those imports may fail when commands are run outside the service virtual environment.

## Best Practices

1. Keep service-specific logic inside each service, not in the shared package.
2. Add only cross-service, generic behavior to shared modules.
3. Keep error contracts stable in shared exceptions module.
4. Add tests in consuming services whenever shared behavior changes.
5. Update this document when shared module contracts change.

## AI Service Integration Note

`AI.API` uses `FileManagerServiceClient` during chat orchestration (`POST /api/v1/chat/stream` and `POST /api/v1/chat/single`) to resolve `file_ids`, then builds multimodal content blocks (image, audio, document text context) before provider invocation.

## SQLAlchemy Model Instructions (AI Service)

For AI service data models, follow SQLAlchemy 2.0+ Declarative Mapping standards:

1. Use `Mapped[...]` annotations for all mapped attributes.
2. Use `mapped_column(...)` for all column definitions.
3. Use UUID as the default primary key type for distributed scalability.
4. Use Alembic revision-based migrations for schema evolution (especially for changes to existing tables).

Example:

```python
import uuid
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column

Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
```
