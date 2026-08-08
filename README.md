# MicroservicesArchitecture — .NET 10 Backend Platform

A production-grade microservices backend built with **Clean Architecture**, **DDD**, **CQRS**, and **database-per-tenant multi-tenancy**. Powers a multi-tenant SaaS platform with real-time notifications, AI integration, centralized backups, and full observability.

---

## What's Inside

### Core Platform Services (`src/Services/`)

| Service | Port | Strategy | Responsibility |
|---|---|---|---|
| **Gateway** (YARP) | 5000 | — | API gateway — routing, rate limiting, correlation IDs, aggregate health checks |
| **Identity** | 5001 | B — Per-Tenant | JWT auth, user management, roles/claims, device tokens, phone/OTP login |
| **Tenant** | 5002 | A — Single Global | Tenant provisioning, config management — the config *provider*, not a consumer |
| **Notification** | 5004 | C — Dual DB | SignalR real-time + Firebase FCM push |
| **FileManager** | 5005 | B — Per-Tenant | Cloud storage via Cloudflare R2 (S3-compatible) |
| **Translation** | 5006 | D — Global + Discriminator | i18n service with tenant-specific overrides |
| **Category** | 5007 | B — Per-Tenant | Hierarchical tree, event-driven sync via Transactional Outbox |
| **AI** | 5008 | — (Python FastAPI) | LLM chat, SSE streaming, semantic tasks |
| **Backup** | 5010 | A — Single Global | Centralized PostgreSQL backup/restore for every service's databases (scheduled + manual, retention, Cloudflare R2 upload) |

### Domain Apps (`src/Apps/`)

| App | Port | Strategy | Responsibility |
|---|---|---|---|
| **Nasheed** | 5009 | B — Per-Tenant | Islamic audio library — artists/songs, AI-driven enrichment, semantic search, lyric verification/generation, background ingestion pipeline |
| **PolySnap** | 5011 | B — Per-Tenant | Proof-of-concept spatial boundary engine — currently CRUD scaffolding only, no snapping logic yet. See [`Doc/POLYSNAP_PROJECT_OVERVIEW.md`](Doc/POLYSNAP_PROJECT_OVERVIEW.md) |

`src/Services/` holds foundational platform services; `src/Apps/` holds domain-specific applications that consume them.

---

## Architecture Highlights

**Multi-Tenancy (Database-per-Tenant)**
Each tenant gets a fully isolated PostgreSQL database, created and migrated automatically on first request. Four strategies are implemented depending on data isolation needs — global, per-tenant, dual-DB, and discriminator-based.

**Clean Architecture + CQRS**
Every service is split into four layers: `API` (Minimal APIs only — no controllers), `Application` (MediatR handlers + FluentValidation), `Domain` (entities + repository interfaces), `Infrastructure` (EF Core + external calls). Commands and queries are fully separated.

**Eager Tenant Provisioning — No Restart Required**
A new tenant becomes usable across every subscribed service within seconds, not on next restart. Tenant Service publishes a `tenant:provisioned` Redis Pub/Sub event on creation; each service eagerly migrates + seeds that tenant's database via `AddTenantProvisioningListener<TContext>()`. A parallel `tenant:updated` broadcast lets background workers with no per-request middleware (e.g. Nasheed's ingestion worker) refresh locally-cached tenant config within seconds instead of waiting for a restart.

**Event-Driven Sync**
The Category service uses a Transactional Outbox pattern to publish domain events via Redis Pub/Sub. Other services consume snapshots locally, decoupling them from direct service calls.

**Observability**
OpenTelemetry instruments every service — distributed traces flow into Jaeger, metrics are scraped by Prometheus, and dashboards are served via Grafana. Every request carries a correlation ID from the gateway to the last handler.

**Automatic Audit Logging**
`BaseDbContext.SaveChangesAsync` captures before/after snapshots of every entity change — user, email, tenant, IP — with zero boilerplate in handlers.

**Feature Flags & Tenant Timezones**
Per-tenant feature flags (`TenantConfiguration.AppSettings`) let individual tenants opt in/out of functionality without a deploy. Per-tenant business timezone (`TimeZoneId`) drives scheduling and reporting, with a UTC fallback everywhere else.

**Centralized Backups**
The Backup service (5010) runs scheduled and on-demand PostgreSQL backups across every other service's databases, with configurable retention and Cloudflare R2 upload for offsite storage.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / C# 14 |
| ORM | Entity Framework Core 10 |
| CQRS | MediatR 12.4 |
| Validation | FluentValidation 12 |
| Database | PostgreSQL (+ Redis 2.7) |
| Gateway | YARP 2.3 |
| API Versioning | Asp.Versioning 10 |
| Background Jobs | Hangfire 1.8 |
| Real-Time | ASP.NET Core SignalR 10 + Firebase Admin |
| Tracing | OpenTelemetry 1.15 + Jaeger |
| Metrics | Prometheus + Grafana |
| File Storage | AWS S3 SDK → Cloudflare R2 |
| AI Service | Python FastAPI + SQLAlchemy |

---

## Running the Backend

**Prerequisites:** .NET 10 SDK, Node.js, PostgreSQL, Redis, Windows Terminal (`wt.exe`)

### Option 1 — Start everything at once

```powershell
node src/Services/start-all-services.mjs
```

Opens a dedicated Windows Terminal tab for every service (colour-coded), with a 4-second stagger between each. Starts observability (Jaeger + Prometheus + Grafana via Docker), Redis, then Tenant, Identity, Notification, FileManager, Translation, AI, Category, Backup, and the Gateway, in dependency order.

> This script covers `src/Services/` and the Gateway only — it does **not** start `src/Apps/` projects (Nasheed, PolySnap). Start those individually via their own `run-development-instance.bat`.

### Option 2 — Start a single service

Each service has a `run-development-instance.bat` that sets the correct environment and port:

```powershell
cd src/Services/Identity/Identity.API
run-development-instance.bat
```

Each service reads its connection strings and secrets from `appsettings.Development.json` (gitignored) falling back to `appsettings.json`. The first request to a new tenant auto-creates and migrates its database automatically.

**Hangfire dashboards** (background jobs): `http://localhost:{port}/admin/jobs`

**Health checks**: `GET http://localhost:{port}/health`

### Testing

```powershell
node src/Services/run-all-tests.mjs
```

Runs the test suite across every service in one pass.

---

## API Testing

Every core service has a Postman collection in [`PostmanCollections/`](PostmanCollections/), covering endpoints with example request bodies and auth flows — Identity, Tenant, Notification, FileManager, Translation, Category, AI, Nasheed, and the unified Gateway collection. Backup and PolySnap collections are not yet generated. Import any collection directly into Postman — no manual setup required.

---

## Shared Libraries

| Library | Purpose |
|---|---|
| `IhsanDev.Shared.Kernel` | Base entities, tenant context, domain events |
| `IhsanDev.Shared.Application` | CQRS pipeline, validation behavior, exceptions |
| `IhsanDev.Shared.Infrastructure` | Middleware, health checks, audit logging, caching |
| `IhsanDev.Shared.Authentication` | JWT + service-to-service auth helpers |
| `IhsanDev.Shared.Testing` | Shared test base classes / `CustomWebApplicationFactory` infrastructure |
| `ihsandev_shared` (Python) | Config loading, auth, exceptions, logging, DB utilities for the AI service |

---

## What Makes This Production-Ready

- Automatic multi-tenant DB provisioning with EF migrations and retry/jitter, plus eager Redis-driven provisioning (no restart needed for new tenants)
- End-to-end distributed tracing with X-Correlation-Id propagation
- Rate limiting at the gateway (500 req/min per IP), with measured bottlenecks documented from k6 load testing
- Localized error messages in all validators and exceptions (Arabic + English)
- Isolated Hangfire job schemas per service
- Aggregate health check at the gateway for all downstream services
- AI service integrated as a first-class Python microservice with SSE streaming
- Centralized backup/restore across all tenant databases with offsite (R2) retention
- Docker-based multi-machine deployment (build/push on one machine, pull/run via `docker compose` on another)
- Optional PostgreSQL replication guide for HA setups

### Known Limitations

A July 2026 full-codebase security audit surfaced several items that are deliberate product/architecture decisions rather than pure bugs — still open and tracked in [`Doc/SECURITY_AUDIT_PENDING_DECISIONS.md`](Doc/SECURITY_AUDIT_PENDING_DECISIONS.md): FileManager's unauthenticated file-download model, Identity's JWT lifetime/session-revocation tradeoff, the frontend's localStorage-vs-httpOnly-cookie token storage, storage quota policy, and the Gateway's lack of its own authentication layer. Several related findings from the same audit (JWT/tenant-verification pipeline order, gateway rate-limiting and catch-all routing) have already been fixed — see the "Common Pitfalls" table in [`.claude/instructions/Dotnet.instructions.md`](.claude/instructions/Dotnet.instructions.md).

---

> Full documentation lives in [`Doc/DOCUMENTATION_INDEX.md`](Doc/DOCUMENTATION_INDEX.md) — 45+ guides covering every architectural decision.
