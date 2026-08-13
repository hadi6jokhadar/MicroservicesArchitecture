# Nasheed Service — Overview

**Service Name:** Nasheed  
**Port:** 5009 (`http://localhost:5009`)  
**Path:** `src/Apps/Nasheed/`  
**Category:** `src/Apps/` — domain app that consumes platform Services  
**Last Updated:** August 13, 2026  
**Status:** ✅ Implemented

---

## Purpose

Nasheed is an Islamic audio library service. It manages artists, songs, AI-driven enrichment, semantic search, lyric verification and generation, user interactions (favorites, ratings, play logs), and a background ingestion pipeline.

It does not own raw file storage (that's FileManager), AI model execution (that's AI.API), or authentication (that's Identity). It orchestrates those platform services.

---

## Architecture

Nasheed follows Clean Architecture with DDD and CQRS:

```
src/Apps/Nasheed/
├── Nasheed.API/           # Minimal APIs only. No controllers.
├── Nasheed.Application/   # MediatR commands/queries/handlers, DTOs, validators,
│                          # interfaces (INasheedTenantCache, INasheedUnitOfWork)
├── Nasheed.Domain/        # Entities, enums, repository interfaces
└── Nasheed.Infrastructure/ # EF Core DbContext, repository implementations,
                             # NasheedUnitOfWork, AI HTTP client, background workers,
                             # NasheedTenantCache, NasheedTenantLoaderService
```

> **Interface placement rule:** `INasheedTenantCache` and `INasheedUnitOfWork` live in `Nasheed.Application/Interfaces/` (Application layer), not in Infrastructure. Implementations (`NasheedTenantCache`, `NasheedUnitOfWork`) are in Infrastructure and registered via DI.

---

## Key Design Decisions

### 1. Single-Tenant, DB Config From TenantService

Nasheed serves **exactly one tenant**. The tenant ID is configured in `appsettings.json` under `MultiTenancy:TenantId`.

**No `DatabaseSettings` section exists in appsettings.json.** The database connection string is loaded at startup from TenantService via `ITenantConfigurationProvider`, cached in the singleton `INasheedTenantCache`, and used for all DB operations.

This means:

- The DB connection string is managed exclusively in TenantService
- Rotating credentials only requires updating TenantService — no Nasheed redeploy
- `appsettings.Development.json` has a `DatabaseSettings` block **only for EF migration tooling** — it is NOT loaded at runtime

### 2. Database Strategy B — Per-Tenant DB

### 3. Startup Script

A `run-development-instance.bat` is available in [Nasheed.API](Nasheed.API/run-development-instance.bat) for local development. It sets `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_URLS=http://localhost:5009`.

`NasheedDbContext` uses Strategy B. Connection resolution priority in `OnConfiguring`:

1. **HTTP request** — `ITenantContext` (set by `UseTenantResolution` middleware)
2. **Background / startup** — `INasheedTenantCache` (set by `NasheedTenantLoaderService`)
3. **EF migration tooling** — `DatabaseSettings:ConnectionString` from config (design-time only)
4. Throws if none of the above are available

### 4. No Global Database — No `UseDefaultDatabaseMigration`

Unlike other services that have a global fallback DB, Nasheed has no global database at all. `UseDefaultDatabaseMigration<NasheedDbContext>()` is intentionally not called in `Program.cs`. Migration runs once at startup inside `NasheedTenantLoaderService`.

### 5. Tenant-First Startup Sequence

```
App starts
  → NasheedTenantLoaderService (IHostedService)
      → reads MultiTenancy:TenantId from config
      → calls ITenantConfigurationProvider.GetTenantConfigurationAsync (retries up to 12×, 5s delay)
      → on success: INasheedTenantCache.SetTenant() + runs DB migration (MigrateWithRecoveryAsync)
      → on repeated failure (all 12 fast retries exhausted): logs an error, then keeps retrying
        indefinitely in the background every 1 minute (RetryTenantLoadInBackgroundAsync) —
        StartAsync itself returns, but the worker stays blocked only until this background retry
        eventually succeeds, not forever
  → NasheedIngestionWorker (BackgroundService)
      → awaits INasheedTenantCache.WaitUntilReadyAsync() before starting poll loop
  → HTTP requests served normally (UseTenantResolution sets ITenantContext per request)
```

See `Doc/INGESTION_PIPELINE.md` "Startup Sequence" for the full detail on the fast-retry vs. background-fallback split, and "Background Tenant Config Refresh" for how `INasheedTenantCache` stays fresh after the initial load (push via Redis Pub/Sub, plus a 5-minute poll fallback).

### 6. AI Processing via AI.API

AI work delegates to AI.API using `IAiApiClient`. Nasheed uses one chat key pair for enrichment, verification, and generation flows, while embedding uses its own embedding settings key. A second, feature-flagged pipeline (`FeatureFlags.NasheedNewLyricsExtractionEnabled`) instead calls a dedicated ASR transcription key (`TranscriptionSettings`, `ModelType=Audio`) for real audio-aligned lyrics timing, followed by a text-only enrichment key pair (`EnrichmentSettings`/`EnrichmentPrompt`) — see `Doc/AI_INTEGRATION.md`. AI.API keys are hardcoded in `NasheedAiKeys` and must exist in AI.API's database for the tenant.

### 7. Semantic Search With pgvector

Embeddings are stored as vector-literal JSON text in `EmbeddingJson` and queried server-side using PostgreSQL `pgvector` distance operators. The search repository executes `ORDER BY EmbeddingJson::vector <=> queryVector::vector` and returns top-N ranked matches. If pgvector is unavailable or fails at runtime, the repository automatically falls back to in-memory cosine similarity.

### 8. Unit of Work for Transactional Deletes

`INasheedUnitOfWork` (Application layer) wraps `NasheedDbContext.SaveChangesAsync()` behind a single `CommitAsync()` method. It is used in handlers that perform multi-step deletes (e.g. `DeleteArtistCommandHandler`) to ensure all related rows are removed atomically in a single transaction.

- **Interface:** `Nasheed.Application/Interfaces/INasheedUnitOfWork.cs`
- **Implementation:** `Nasheed.Infrastructure/Persistence/NasheedUnitOfWork.cs`
- **Registered as:** scoped in `InfrastructureServiceExtensions`

Handlers that delete a single entity and immediately persist can still call `_repository.DeleteAsync()` + `SaveChangesAsync()` directly. Only multi-step cascades require `INasheedUnitOfWork`.

---

### 9. Content-Editor Permission Claims (Admin + Narrower "Data Entry" Role)

Most write endpoints are `AdminOnly` (`RequireRole("Admin","Superadmin","SuperAdmin")`). Three endpoints instead use a narrower content-editor policy that also admits a non-admin user holding a matching `Permission` claim — defined in `Nasheed.API/Program.cs`'s `AddAuthorization` block via `RequireAssertion(ctx => IsAdmin(ctx) || ctx.User.HasClaim("Permission", "..."))`:

- **`SongsCreate`** (`POST /api/songs`) — claim `nasheed.songs.create`
- **`SongsEdit`** (`PUT /api/songs/{id}`) — claim `nasheed.songs.edit`, plus an ownership check (a non-admin may only edit a song whose `CreatedBy` matches their own user id)
- **`ArtistsCreate`** (`POST /api/artists`) — claim `nasheed.artists.create`

Delete endpoints and artist update remain Admin-only — no claim grants delete or artist-edit, by design. The `NasheedDataEntry` role and its claims are seeded automatically (scoped to the `anashid` tenant only) via Identity's `SystemPermissionCatalog` — nothing to create by hand. See `Doc/API_ENDPOINTS.md` "Content-Editor Permission Claims" for the full mechanism and `Doc/SHARED_IDENTITY_SERVICE_GUIDE.md` "Permission Claims" for the platform-wide pattern.

**`UpdateSongCommandHandler` lives in `Nasheed.Infrastructure/Handlers/UpdateSong/`, not `Nasheed.Application`** — it needs `ICurrentUserService` (only available via `IhsanDev.Shared.Infrastructure`, which `.Application` doesn't reference) for the ownership check above. `UpdateSongCommand`/its validator stay in `.Application`; `Program.cs` registers MediatR against both the `.Application` and `.Infrastructure` assemblies so this handler is still discovered. See `Dotnet.instructions.md` pitfall #37 for the general pattern.

---

### 10. FileManager Calls Always Include Tenant Context

Nasheed file lifecycle operations call FileManager internal endpoints through `IFileManagerServiceClient`.

For these calls, Nasheed always passes `tenantId` from `MultiTenancy:TenantId` as a query parameter.

This is required because FileManager resolves database context from tenant information. If `tenantId` is omitted, FileManager may query the wrong database.

**Usage tracking (v3.2.0+):** `ChangeTempStatusAsync` uses `usageArea` + `rowId` + `isNew` flag to explicitly add or remove a usage row in `FileManagerUsage`, then auto-recalculates `Temp` on the file:

- `Artist` operations: `usageArea: "Artist"`, `rowId: artistId.ToString()`, `isNew: true` (create) / `isNew: false` (delete)
- `Song` operations: `usageArea: "Song"`, `rowId: songId.ToString()`, `isNew: true` (create) / `isNew: false` (delete)
- `isNew=true` → adds usage row → `Temp=false`; `isNew=false` → removes usage row → `Temp=true` when count reaches 0

---

## Platform Services Consumed

| Service                       | How used                                                                                                              |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| **TenantService** (5002)      | Fetch DB connection string + JWT settings on startup via `ITenantConfigurationProvider`                               |
| **AI.API** (5008)             | Chat (`/api/v1/chat/single`) for enrichment, verification, and generation; Embed (`/api/v1/embedding`) for embeddings; Transcribe (`/api/v1/transcription`) for real audio-aligned lyrics timing (new pipeline, flag-gated) |
| **FileManagerService** (5005) | Songs and artist images are stored as FileManager file IDs                                                            |
| **IdentityService** (5001)    | JWT auth — tokens validated per-tenant using TenantService JWT config                                                 |

---

## Configuration

Key `appsettings.json` sections:

| Section                               | Purpose                                                       |
| ------------------------------------- | ------------------------------------------------------------- |
| `MultiTenancy.TenantId`               | The single tenant this Nasheed instance serves                |
| `MultiTenancy.TenantServiceUrl`       | URL of TenantService to fetch DB config from                  |
| `MultiTenancy.JwtMode`                | `"PerTenant"` — JWT validated using tenant's own secret       |
| `Jwt.*`                               | Bootstrap JWT key used as fallback before per-tenant override |
| `Services.AiService.BaseUrl`          | AI.API URL for ingestion and generation calls                 |
| `Services.FileManagerService.BaseUrl` | FileManager URL for internal file lifecycle calls             |
| `ServiceCommunication.SharedSecret`   | Service-to-service auth secret                                |

Request contract notes:

- `CreateSongCommand.FileId` is `int`
- `CreateArtistCommand.ImageFileId` is `int?`
- interaction endpoints accept explicit numeric `userId` in request body for favorites, ratings, and play logs

---

## Technology Stack

| Technology          | Version | Usage               |
| ------------------- | ------- | ------------------- |
| .NET                | 10      | Runtime             |
| EF Core             | 10.0    | ORM + migrations    |
| Npgsql              | 10.0    | PostgreSQL driver   |
| MediatR             | 12.4    | CQRS                |
| FluentValidation    | 12      | Input validation    |
| StackExchange.Redis | 2.7     | Tenant config cache |
