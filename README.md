# MicroservicesArchitecture — .NET 8 Backend Platform

A production-grade microservices backend built with **Clean Architecture**, **DDD**, **CQRS**, and **database-per-tenant multi-tenancy**. Powers a multi-tenant SaaS platform with real-time notifications, AI integration, and full observability.

---

## What's Inside

| Service | Port | Responsibility |
|---|---|---|
| **Gateway** (YARP) | 5000 | API gateway — routing, rate limiting, correlation IDs |
| **Identity** | 5001 | JWT auth, user management, device tokens |
| **Tenant** | 5002 | Tenant provisioning, config management |
| **Notification** | 5004 | SignalR real-time + Firebase FCM push |
| **FileManager** | 5005 | Cloud storage via Cloudflare R2 (S3-compatible) |
| **Translation** | 5006 | i18n service with tenant-specific overrides |
| **Category** | 5007 | Hierarchical tree, event-driven sync |
| **AI** | 5008 | Python FastAPI — LLM chat, SSE streaming |

---

## Architecture Highlights

**Multi-Tenancy (Database-per-Tenant)**
Each tenant gets a fully isolated PostgreSQL database, created and migrated automatically on first request. Four strategies are implemented depending on data isolation needs — global, per-tenant, dual-DB, and discriminator-based.

**Clean Architecture + CQRS**
Every service is split into four layers: `API` (Minimal APIs only — no controllers), `Application` (MediatR handlers + FluentValidation), `Domain` (entities + repository interfaces), `Infrastructure` (EF Core + external calls). Commands and queries are fully separated.

**Event-Driven Sync**
The Category service uses a Transactional Outbox pattern to publish domain events via Redis Pub/Sub. Other services consume snapshots locally, decoupling them from direct service calls.

**Observability**
OpenTelemetry instruments every service — distributed traces flow into Jaeger, metrics are scraped by Prometheus, and dashboards are served via Grafana. Every request carries a correlation ID from the gateway to the last handler.

**Automatic Audit Logging**
`BaseDbContext.SaveChangesAsync` captures before/after snapshots of every entity change — user, email, tenant, IP — with zero boilerplate in handlers.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 / C# 12 |
| ORM | Entity Framework Core 9 |
| CQRS | MediatR 12.4 |
| Validation | FluentValidation 12 |
| Database | PostgreSQL (+ Redis 2.7) |
| Gateway | YARP 2.3 |
| Background Jobs | Hangfire 1.8 |
| Real-Time | SignalR 8 + Firebase Admin |
| Tracing | OpenTelemetry + Jaeger |
| Metrics | Prometheus + Grafana |
| File Storage | AWS S3 SDK → Cloudflare R2 |
| AI Service | Python FastAPI + SQLAlchemy |

---

## Running the Backend

**Prerequisites:** .NET 8 SDK, PostgreSQL, Redis

```powershell
# Start a specific service (example: Identity)
cd src/Services/Identity/Identity.API
dotnet run

# Start the gateway
cd src/Gateway/Gateway.API
dotnet run
```

Each service reads its connection strings and secrets from `appsettings.json` / environment variables. The first request to a new tenant auto-creates and migrates its database.

**Hangfire dashboards** (background jobs): `http://localhost:{port}/admin/jobs`

**Health checks**: `GET http://localhost:{port}/health`

---

## Shared Libraries

| Library | Purpose |
|---|---|
| `IhsanDev.Shared.Kernel` | Base entities, tenant context, domain events |
| `IhsanDev.Shared.Application` | CQRS pipeline, validation behavior, exceptions |
| `IhsanDev.Shared.Infrastructure` | Middleware, health checks, audit logging |
| `IhsanDev.Shared.Authentication` | JWT + service-to-service auth helpers |

---

## What Makes This Production-Ready

- Automatic multi-tenant DB provisioning with EF migrations and retry/jitter
- End-to-end distributed tracing with X-Correlation-Id propagation
- Rate limiting at the gateway (500 req/min per IP)
- Localized error messages in all validators and exceptions (Arabic + English)
- Isolated Hangfire job schemas per service
- Aggregate health check at the gateway for all downstream services
- AI service integrated as a first-class Python microservice with SSE streaming

---

> Full documentation lives in [`Doc/DOCUMENTATION_INDEX.md`](Doc/DOCUMENTATION_INDEX.md) — 40+ guides covering every architectural decision.
