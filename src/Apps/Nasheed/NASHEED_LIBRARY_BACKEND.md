# Nasheed Library Backend

**Purpose:** Backend architecture and implementation plan for adding the nasheed library service to the current microservices platform.  
**Last Updated:** May 28, 2026  
**Status:** ⚠️ Proposed Design

---

## Overview

This document defines how the nasheed library should fit into the existing backend platform. The goal is to add the feature set without introducing a parallel architecture. The implementation should reuse the same .NET patterns already in production: Minimal APIs, MediatR, FluentValidation, shared authentication, shared tenant resolution, FileManager for file storage, Notification for real-time updates, and AI.API for AI execution.

The Nasheed service should own domain logic and orchestration for artists, songs, ingestion, semantic search metadata, and lyric generation. It should not duplicate shared platform capabilities that already exist in other services.

---

## Best Fit In This Repository

### Recommended Service Shape

Create a new backend microservice, for example `Nasheed`, with the standard project layout:

```text
src/Services/Nasheed/
├── Nasheed.API/
├── Nasheed.Application/
├── Nasheed.Domain/
└── Nasheed.Infrastructure/
```

Use the same conventions already applied by Identity, FileManager, and Notification:

- Minimal APIs only in `Nasheed.API`
- Commands, queries, handlers, validators, and DTOs in `Nasheed.Application`
- Entities and repository interfaces in `Nasheed.Domain`
- DbContexts, repositories, HTTP clients, and background workers in `Nasheed.Infrastructure`

### Locked Architecture Decisions

The following decisions are fixed for this service:

- The service is tenant-specific
- Database strategy is **Strategy B: Per-Tenant DB**
- `x-tenant-id` is required for this service
- Tenant bypass is not allowed
- Optional tenant behavior is not allowed
- All AI extraction calls must provide both AI settings and system prompt keys
- Song ingestion is background-job driven
- A management table is required for ingestion job lifecycle and retry control
- A dedicated per-tenant vector database connection is required
- `pgvector` will be used

### Recommended Responsibility Split

The new service should own:

- Artists catalog
- Songs catalog
- Ingestion workflow state
- Verified lyrics and LRC storage
- Favorites, ratings, and play logs
- Ingestion job management
- Search index metadata and vector persistence
- Generation requests for new nasheed lyrics

The new service should not own:

- Raw file storage
- Generic AI model execution
- Shared authentication
- Shared tenant resolution
- Shared real-time infrastructure

Those capabilities already exist elsewhere in the platform.

---

## Reuse From Existing Platform

### Authentication and Tenant Infrastructure

Reuse the shared authentication and tenant pipeline already used by backend services.

Existing reusable capabilities:

- Shared JWT authentication through Identity Service
- Multi-tenancy registration through `AddMultiTenancy`
- Tenant-aware CORS middleware
- JWT tenant verification middleware
- Automatic default and tenant database migration
- Service-to-service shared-secret authentication

This means the Nasheed service should follow the same `Program.cs` structure already used in Identity and FileManager instead of creating a custom authentication or tenant stack.

For this service, tenant handling is strict:

- Register `AddMultiTenancy`
- Use Strategy B `DbContext` behavior with `ITenantContext`
- Require `x-tenant-id` on every business endpoint
- Do not expose `[BypassTenant]` endpoints
- Do not expose `[OptionalTenant]` endpoints
- Use the middleware order required by the database strategy guide

This service should be treated as a pure tenant-data service, not a hybrid service.

### File Storage

Reuse FileManager for all uploaded audio files.

Recommended pattern:

- The frontend audio editor prepares the song file
- The frontend uploads the resulting file through FileManager
- The backend receives `FileId` and `Title` only when a song is submitted
- The backend stores the returned `FileId` on the song record
- AI analysis starts from that stored `FileId`

Do not create a separate storage subsystem inside the new service unless there is a hard requirement FileManager cannot satisfy.

### AI Execution

Reuse AI.API for all model interactions.

AI.API already provides:

- Service-to-service chat requests
- Tenant-scoped settings and prompts with global fallback support inside AI.API
- File attachment support through `file_ids`
- Audio-capable Qwen-compatible request handling
- Provider settings records and system prompt records in its own database
- Token usage logging patterns already used by AI.API

Recommended pattern:

- Create dedicated AI provider settings records in AI.API for extraction, verification, embedding, and generation tasks
- Create dedicated AI system prompt records in AI.API for extraction, verification, and generation tasks
- Store hardcoded keys in the Nasheed service code for the required AI settings and system prompts
- Resolve those keys from AI.API data at runtime
- Call AI.API from the new .NET service using service authentication
- Always provide AI settings and system prompt keys for extraction and verification requests

For embeddings, add a dedicated AI.API endpoint that:

- accepts the embedding input text
- requires an AI settings key in the request
- uses the selected embedding model for execution
- writes token usage through the AI token usage log flow

The current repository includes documentation for this integration, but not a shared .NET typed client implementation yet. That client should be added in the new service or extracted into shared infrastructure later if more services need it.

### Real-Time Updates

Reuse Notification and SignalR patterns for ingestion progress.

Recommended events:

- Song uploaded
- AI extraction started
- Metadata extracted
- Lyrics verified
- Search index completed
- Generation request completed

If the user-facing application needs live progress, use Notification style real-time delivery instead of inventing a second real-time stack.

### Background Processing

The current platform uses `BackgroundService` and hosted workers rather than Hangfire.

Existing examples:

- Notification queue processing
- Notification cleanup
- Tenant cache refresh
- FileManager temp file cleanup

Recommendation:

- Use hosted services and database-backed work queues
- Add a dedicated ingestion management table for queue state, retry count, last error, next retry time, and remove or cancel behavior
- Keep retry and remove operations explicit through management endpoints or admin commands
- Do not introduce Hangfire for the first implementation

---

## What Does Not Exist Yet

The following capabilities are not currently implemented in the .NET services and would need to be built for this feature:

- Song and artist domain model
- Upload to analysis orchestration workflow
- Ingestion management table and worker flow
- A typed .NET client for AI.API chat integration
- A typed .NET client for the new AI embedding endpoint
- Vector database settings in tenant configuration
- Vector persistence and similarity search in application code
- Search query pipeline that converts user text to embeddings and ranks songs
- Admin tooling to re-run ingestion or rebuild search vectors

The repository contains AI service support for embeddings at the provider-settings level, but there is no application-level semantic search implementation in the existing .NET services and there is no current tenant vector database connection contract for this feature.

---

## Recommended Data Design

### Primary Catalog Tables

Recommended relational model:

All non-relation tables should inherit from the shared base entity. Relation tables may stay as lightweight join tables when that is the better relational fit.

#### Artists

- `Id`
- `Name`
- `ImageFileId` nullable
- `SongCount`
- `IsArchived`
- audit fields from shared base entity

Inherits from base entity.

#### Songs

- `Id`
- `ArtistId`
- `Title`
- `FileId`
- `DurationSeconds`
- `LanguageCode`
- `LyricsRaw`
- `LyricsVerifiedLrc`
- `LyricsPlainText`
- `Summary`
- `VocalStyle`
- `SongState`
- `SearchIndexStatus`
- `PublishedAt` nullable
- `IsArchived`
- audit fields

Inherits from base entity.

`Title` and `FileId` are user-provided on submit. The rest of the descriptive metadata is generated by AI.

Recommended `SongState` values:

- `Uploaded`
- `InQueue`
- `Pending`
- `Done`
- `Failed`

#### SongMoodTags

- `SongId`
- `Tag`

This can be a normalized child table or a PostgreSQL `jsonb` column. Normalization is easier to filter and index consistently.

#### PlayLogs

- `Id` bigint
- `SongId`
- `UserId`
- `TenantId` if the chosen tenancy model requires it
- `PlayedAt`

Inherits from base entity.

#### Favorites

- composite key on `UserId` and `SongId`
- `TenantId`

Relation table. Does not need to inherit from base entity.

#### Ratings

- composite uniqueness on `UserId` and `SongId`
- `Value` from 1 to 5
- `TenantId`

Relation table. Does not need to inherit from base entity.

#### SongIngestionJobs

- `Id`
- `SongId`
- `FileId`
- `JobType`
- `JobStatus`
- `RetryCount`
- `MaxRetries`
- `LastError`
- `NextRetryAt` nullable
- `StartedAt` nullable
- `CompletedAt` nullable
- `RemovedAt` nullable
- audit fields

Inherits from base entity.

This table is required for background-job management. It should support queue inspection, retry, remove, and failure analysis.

### Search Storage

Add a dedicated search table or store for semantic search:

#### SongSearchDocuments

- `SongId`
- `SearchText`
- `Embedding`
- `EmbeddingModelKey`
- `EmbeddedAt`
- `IndexVersion`

Inherits from base entity.

The vector data should use a dedicated vector database connection, separate from the service's normal business database connection.

Recommended tenant configuration extension:

- `VectorDatabaseSettings:Provider`
- `VectorDatabaseSettings:ConnectionString`

This vector connection should be stored in tenant settings the same way the normal database connection is stored for other tenant services.

Implementation requirement:

- Use PostgreSQL with `pgvector`
- Keep vector persistence behind an interface in infrastructure
- Resolve the tenant-specific vector database connection through tenant configuration

### Soft Delete Convention

Use `IsArchived`, not `IsDeleted`, to match the rest of the codebase.

Rationale:

- Tenant, Translation, and Identity already use archive semantics
- It keeps play logs, ratings, and favorites referentially valid
- It aligns with existing admin restore patterns

---

## Tenancy Recommendation

This decision is already locked.

### Locked Choice

Use **Strategy B: Per-Tenant DB**.

Implications:

- The service stores tenant-specific business data only
- The service must use a tenant-aware `DbContext`
- `x-tenant-id` is required
- No tenant bypass endpoints are allowed
- No optional-tenant endpoints are allowed
- The global fallback database exists only as a Strategy B infrastructure requirement, not as a business-mode endpoint pattern

### Program.cs and Middleware Rules

The service should follow Strategy B from the database strategy instructions:

1. `AddMultiTenancy(builder.Configuration)`
2. tenant-aware `DbContext` with `ITenantContext`
3. `UseTenantResolution(builder.Configuration)`
4. `UseTenantAwareCors()`
5. `UseJwtTenantVerification(builder.Configuration)`
6. `UseDefaultDatabaseMigration<TContext>()`
7. `UseTenantDatabaseMigration<TContext>(builder.Configuration)`
8. `UseAuthentication()`
9. `UseAuthorization()`

Even though Strategy B requires the default migration as infrastructure fallback, the Nasheed service should not expose business endpoints that bypass tenant context.

---

## AI Workflow Recommendation

### Ingestion Pipeline

Recommended pipeline for a newly uploaded song:

1. Frontend audio editor prepares the audio file
2. Frontend uploads the file through FileManager
3. Backend receives `FileId` and `Title` only
4. Nasheed service creates the song record with `SongState = Uploaded`
5. Nasheed service creates an ingestion job row and moves the song to `InQueue`
6. Background worker picks up the queued job and moves the song to `Pending`
7. Worker asks AI.API for metadata extraction using hardcoded AI settings key and hardcoded system prompt key
8. Worker stores generated metadata such as language, summary, mood tags, vocal style, raw lyrics, and derived artist information
9. Worker asks AI.API for verification and LRC formatting using hardcoded AI settings key and hardcoded system prompt key
10. Worker stores `LyricsVerifiedLrc` and derived plain-text lyrics
11. Worker builds semantic search text
12. Worker calls the new AI embedding endpoint with the required AI settings key
13. Worker stores search document and vector data in the tenant vector database
14. Worker marks the job complete and moves the song to `Done`

---

## Integration Tests

**Project:** `src/Apps/Nasheed/Nasheed.API.Tests/`  
**Pattern:** MediatR handler tests (bypasses HTTP layer, avoids .NET 9 PipeWriter bug)  
**Database:** PostgreSQL (`nasheed_testdb`) — matches production FK/migration behaviour

### What is tested

| File                                | Coverage                                                                                                                                            |
| ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Endpoints/ArtistEndpointsTests.cs` | CreateArtist, GetArtistById, GetArtistList, UpdateArtist, DeleteArtist (happy path + validation + not-found)                                        |
| `Endpoints/SongEndpointsTests.cs`   | CreateSong (with side-effects: artist count, ingestion job), GetSongById, GetSongList, UpdateSong, DeleteSong (happy path + validation + not-found) |

### What is stubbed / excluded

| Excluded component           | Why                                                                       |
| ---------------------------- | ------------------------------------------------------------------------- |
| `NasheedTenantLoaderService` | Calls TenantService which is not running in tests                         |
| `NasheedIngestionWorker`     | Polls AI API which is not available in tests                              |
| `IAiApiClient`               | Replaced with `Mock<IAiApiClient>` returning no-op responses              |
| Search / lyric generation    | Require live AI service; add as a separate integration category if needed |

### Running tests

```powershell
dotnet test src\Apps\Nasheed\Nasheed.API.Tests\Nasheed.API.Tests.csproj
```

See `src/Apps/Nasheed/Nasheed.API.Tests/README.md` for full testing documentation.

### Prompt Management

All prompts must be stored in AI.API system prompts.

All model selections must be stored in AI.API provider settings.

The Nasheed service should use hardcoded keys in code to fetch the required records from AI.API-backed data.

Recommended key pattern:

- extraction system prompt key
- extraction AI settings key
- verification system prompt key
- verification AI settings key
- generation system prompt key
- generation AI settings key
- embedding AI settings key

This keeps prompts editable in the database while keeping the workflow contract explicit in code.

---

## Search Architecture Recommendation

### Semantic Search Input

Construct the semantic document from:

- Title
- Artist name
- Mood tags
- Vocal style
- Summary
- Verified lyrics with timestamps removed

This matches the uploaded proposal and is the correct direction for semantic retrieval.

### Missing Platform Capability

There is no existing application-level vector search implementation in current .NET services.

This means the Nasheed service must introduce:

- embedding generation orchestration
- tenant vector database connection resolution
- vector persistence
- nearest-neighbor ranking query logic
- index rebuild workflow

### Recommended Search Stack

If PostgreSQL remains the primary database, use:

- PostgreSQL
- `pgvector`
- indexed vector column on song search documents

The embedding generation flow should call AI.API through a dedicated endpoint that requires an AI settings key and logs token usage.

If database portability matters more than implementation speed, abstract search behind an interface and keep the first implementation PostgreSQL-specific.

---

## Proposed API Surface

### Catalog Endpoints

- `POST /api/artists`
- `GET /api/artists`
- `GET /api/artists/{id}`
- `POST /api/songs` accepts `fileId` and `title` only
- `GET /api/songs`
- `GET /api/songs/{id}`
- `PUT /api/songs/{id}`
- `DELETE /api/songs/{id}` archive first

### Ingestion Endpoints

- `GET /api/ingestion/jobs`
- `GET /api/ingestion/jobs/{id}`
- `POST /api/ingestion/jobs/{id}/retry`
- `POST /api/ingestion/jobs/{id}/remove`
- `POST /api/songs/{id}/reindex`
- `GET /api/songs/{id}/analysis-status`

### Interaction Endpoints

- `POST /api/songs/{id}/favorite`
- `DELETE /api/songs/{id}/favorite`
- `POST /api/songs/{id}/rating`
- `POST /api/songs/{id}/play-log`

### Search Endpoints

- `GET /api/search?query=...`
- `GET /api/search/similar/{songId}`

### Generation Endpoints

- `POST /api/generation/lyrics`

All business endpoints require tenant context for this service.

---

## Implementation Order

Recommended delivery order:

1. Create the new service skeleton using the standard backend structure
2. Implement Strategy B per-tenant `DbContext` and middleware order
3. Extend tenant settings contract with vector database connection settings
4. Add artist, song, ingestion-job, favorite, rating, and play-log entities
5. Ensure all non-relation tables inherit from base entity
6. Integrate FileManager for file reference storage
7. Add a typed AI.API client for extraction and verification requests
8. Add a dedicated AI embedding endpoint with AI settings requirement and token usage logging
9. Build extraction, verification, and indexing workflow with a hosted background worker
10. Add pgvector-backed tenant vector persistence
11. Add semantic search endpoint
12. Add real-time progress updates

---

## Risks and Design Notes

### AI Determinism

JSON extraction should use low-temperature provider settings and prompt contracts that enforce strict machine-readable output. Retry logic is required when parsing fails because the ingestion flow is job-based and recoverable.

### Vector Index Cost

Reindexing all songs can become expensive. Keep an `IndexVersion` and support incremental rebuilds.

### AI Key Contract

The Nasheed workflow depends on hardcoded AI settings keys and system prompt keys. Those database records must exist for each tenant scope or have a valid fallback strategy in AI.API.

### Uploaded Proposal Versus Current Platform

The uploaded proposal assumes one service may handle many responsibilities. In this repository, the better design is split responsibility:

- FileManager stores files
- AI.API performs AI work
- Notification handles real-time patterns
- Identity handles auth
- The new Nasheed service owns domain logic and orchestration

This is the main architectural correction required before implementation.

---

## Final Recommendation

Implement this feature as a new .NET microservice that orchestrates existing platform capabilities rather than replacing them.

### Keep

- FileManager for audio files
- AI.API for extraction, verification, embedding, and generation
- Shared authentication and tenant middleware
- Notification-style background and real-time patterns

### Add

- Nasheed domain model
- Strategy B per-tenant database implementation
- AI orchestration client in .NET
- Ingestion management table and worker flow
- Dedicated tenant vector database settings
- Search document and vector persistence
- `pgvector` support
- Semantic search endpoints

### Avoid

- Duplicating file storage logic
- Duplicating generic AI provider logic
- Introducing a separate authentication model
- Introducing tenant bypass or optional-tenant behavior
- Adding Hangfire before hosted workers have been exhausted as an option

This approach gives the feature a clean fit inside the current platform and minimizes architectural drift.
