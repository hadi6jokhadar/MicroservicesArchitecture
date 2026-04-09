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
- `ihsandev_shared/clients/base_client.py`

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
2. Supports common HTTP methods.
3. Supports tenant header forwarding.
4. Handles request and response errors consistently.

## Integration Pattern in Services

A Python service typically:

1. Defines service-specific settings by extending shared base settings.
2. Builds auth dependency using shared security factory.
3. Uses shared exception handlers in FastAPI app registration.
4. Uses shared DB helpers for database bootstrap.
5. Uses shared base client for downstream service calls.

## Versioning and Packaging

Package metadata is in:

- `src/Shared/ihsandev_shared/pyproject.toml`

Current setup uses editable install in service requirements, which keeps service code synced with local shared package changes.

## Best Practices

1. Keep service-specific logic inside each service, not in the shared package.
2. Add only cross-service, generic behavior to shared modules.
3. Keep error contracts stable in shared exceptions module.
4. Add tests in consuming services whenever shared behavior changes.
5. Update this document when shared module contracts change.
