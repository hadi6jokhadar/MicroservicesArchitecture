# PolySnap Service Guide

## Overview

PolySnap (`src/Apps/PolySnap/`, port **5011**) is a proof-of-concept product app — the backend for an automated spatial boundary engine. The intended end product converts freehand-drawn map shapes into precise polygons snapped to real infrastructure (roads, parcels, buildings) using PostGIS/OSM data.

**Current status: CRUD scaffold only.** This service currently exposes standard create/read/update/delete operations against a `SnapRequestEntity` row. It does **not** yet perform any geometry processing, PostGIS spatial queries, or OSM data lookups — `SnappedGeometryGeoJson` is a plain nullable text column populated by whichever process implements the snapping logic in a later phase. Nothing in this service currently parses, validates, or interprets the GeoJSON strings it stores; they are opaque text as far as this scaffold is concerned.

Database Strategy: **B — Per-Tenant DB** (see `.claude/instructions/database-strategy.instructions.md`), the same strategy used by Nasheed. `PolySnapDbContext` resolves its connection string per-request from `ITenantContext` (set by `UseTenantResolution` middleware) when multi-tenancy is enabled and a tenant is present, falling back to the global `DatabaseSettings:ConnectionString` otherwise (design-time EF tooling, or multi-tenancy disabled).

Placement note: unlike foundational platform services (`src/Services/`), PolySnap is a domain-specific product app and therefore lives under `src/Apps/PolySnap/`, mirroring `src/Apps/Nasheed/`'s placement exactly.

---

## Architecture

PolySnap follows Clean Architecture with DDD and CQRS, identical in shape to Nasheed and Category:

```
src/Apps/PolySnap/
├── PolySnap.API/            # Minimal APIs only. No controllers.
├── PolySnap.Application/    # MediatR commands/queries/handlers, DTOs, validators
├── PolySnap.Domain/         # SnapRequestEntity, ISnapRequestRepository
└── PolySnap.Infrastructure/ # EF Core DbContext, repository implementation, migrations
```

---

## Entities & Data Model

### `SnapRequestEntity`

A single freehand-drawn shape submitted for snapping, plus (once a later phase implements the actual engine) its computed precise result.

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Id` | `int` | — | `BaseEntity.Id` |
| `Name` | `string` | Yes, max 200 | Human-readable label for the request |
| `RawGeometryGeoJson` | `string` | Yes | The user's rough freehand-drawn shape, as GeoJSON text |
| `SnappedGeometryGeoJson` | `string?` | No | The computed precise result; `null` until a later-phase snapping process populates it |
| `Threshold` | `double` | No, default `0.5` | The block-overlap ratio to be used by the (not-yet-implemented) snapping algorithm |

Plus the standard `BaseEntity` audit fields: `IsArchived`, `Status`, `Created`, `CreatedBy`, `LastModified`, `LastModifiedBy`.

Table name: `SnapRequests`.

---

## API Endpoints

All routes are versioned (`/api/v{version:apiVersion}/...`) via `Asp.Versioning.Http`, matching the platform-wide API versioning convention.

| Method | Route | Purpose | Auth |
| --- | --- | --- | --- |
| POST | `/api/v1/snap-requests` | Create a `SnapRequest` | Bearer, tenant user |
| GET | `/api/v1/snap-requests/{id}` | Get a single `SnapRequest` by id | Bearer, tenant user |
| GET | `/api/v1/snap-requests` | Paginated list (`textFilter`, `pageNumber`, `pageSize`) | Bearer, tenant user |
| PUT | `/api/v1/snap-requests/{id}` | Partial update (any field null = unchanged) | Bearer, tenant user |
| DELETE | `/api/v1/snap-requests/{id}` | Delete | Bearer, tenant user |

Audit log endpoints are also mapped (`app.MapAuditLogEndpoints()`), consistent with every other service on the platform.

---

## Configuration

Key `appsettings.json` sections (`PolySnap.API/appsettings.json`):

| Section | Purpose |
| --- | --- |
| `Urls` | `http://localhost:5011` |
| `DatabaseSettings.ConnectionString` | Global/fallback connection string, used before a tenant is resolved and by EF migration tooling |
| `MultiTenancy.TenantId` | `"polysnap"` — a fixed placeholder tenant id for this deployment (mirrors Nasheed's fixed `MultiTenancy:TenantId: "anashid"`). Unlike Nasheed, PolySnap does not currently pin its `DbContext` to a single tenant via a dedicated tenant-loader hosted service — tenant resolution still runs per-HTTP-request via the standard `UseTenantResolution` middleware, the same as Category/Identity/FileManager. This key is reserved for a later phase if PolySnap is deployed as a single-tenant-per-instance service the way Nasheed is. |
| `ServiceCommunication.ServiceName` | `"PolySnapService"` |
| `Jwt.*` | Bootstrap JWT key, overridden per-tenant when multi-tenancy resolves a tenant |
| `Swagger.Title` | `"PolySnap API"` |

`appsettings.Development.json` only ever needs to hold the EF-tooling `DatabaseSettings` connection string plus local dev secrets — it is gitignored dev-only config.

---

## Program.cs Pipeline

Middleware order (critical — do not reorder):

```
InitializeDatabaseAsync<PolySnapDbContext>()   (before app.Run, awaited)
  → WarmTenantConfigCacheAsync / WarmTenantDatabaseMigrationsAsync (startup warm-up, no-op if MultiTenancy disabled)
app.UseDefaultDatabaseMigration<PolySnapDbContext>()
app.UseTenantResolution(configuration)
app.UseTenantAwareCors()
app.UseTenantDatabaseMigration<PolySnapDbContext>(configuration)   (only if MultiTenancy:Enabled)
app.UseServiceAuthentication()
app.UseAuthentication()
app.UseJwtTenantVerification(configuration)   (MUST be after UseAuthentication — reads context.User)
app.UseAuthorization()
```

This is the standard Strategy B pipeline shared by Category/Identity/FileManager (`UseAuthentication()` before `UseJwtTenantVerification()`, per the July 2026 pipeline-order fix applied platform-wide).

---

## What's explicitly out of scope for this scaffold

- No PostGIS extension, spatial column types, or spatial queries
- No OSM data ingestion or lookup
- No actual snapping/geometry algorithm — `SnappedGeometryGeoJson` is only ever set via the plain `Update` command, never computed
- No background worker/job to process pending `SnapRequest`s

These are all planned for a later phase once the spatial engine itself is designed.

---

## Technology Stack

| Technology | Version | Usage |
| --- | --- | --- |
| .NET | 10.0 | Runtime |
| EF Core | 10.0 | ORM + migrations |
| Npgsql | 10.0 | PostgreSQL driver |
| MediatR | 12.4 | CQRS |
| FluentValidation | 12 | Input validation |
| Asp.Versioning.Http | 10.0 | API versioning |
| StackExchange.Redis | 2.7 | Tenant config cache |
