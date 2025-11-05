# Notification Service — Implementation Guide (Phase 2)

This document contains the concrete implementation steps for the Notification Service (Phase 2).
It assumes Phase 1 scaffolding (domain model, DbContexts and configuration) is in place as described in `NOTIFICATION_SERVICE_INTEGRATION_GUIDE.md`.

## Goals

- Implement a queue-first, multi-tenant Notification Service with:
  - Global queue DB (Postgres) and per-tenant Notifications persisted to tenant databases.
  - SignalR Hub for realtime delivery with tenant scoping via `x-tenant-id` header.
  - Firebase (FCM) push delivery (optional, fire-and-forget).
  - Reliable processing: retries, expiry, and idempotency.
  - Tenant-aware JWT authentication matching Identity service patterns.

## Prerequisites

- PostgreSQL available for global DB and tenant DBs (or adequate connection strings).
- Tenant Service and Identity Service running and reachable (urls configured in appsettings).
- Shared libraries from `src/Shared/*` available as project references or NuGet packages.

## High-level contract

- Inputs: SendNotificationRequest (tenantId, recipients, title, body, payload, deliveryOptions).
- Outputs: immediate 202 Accepted with queue item id for waitable=false; 200 OK with notification result for waitable=true (if wait mode implemented via polling or long-poll).
- Error modes: transient DB/HTTP errors cause retry with backoff; malformed requests return 400; unauthorized requests return 401.

## Edge cases

- Missing or invalid tenantId header on SignalR connections — reject connection (HTTP 401/400) and log.
- Recipient user not found in tenant DB — mark delivery as Failed with reason; still persist notification record.
- Device tokens stale — handle FCM responses, remove or mark tokens as invalid via Identity API.

## Implementation steps (ordered, incremental)

### 1) Project scaffolding (create projects)

- Create solution projects under `src/Services/Notification/`:
  - `Notification.API` (ASP.NET Core Web API + SignalR)
  - `Notification.Application` (MediatR, DTOs, Commands, Handlers)
  - `Notification.Domain` (Entities, value objects)
  - `Notification.Infrastructure` (EF Core DbContexts, migrations, repository implementations, processing worker)
  - `Notification.BackgroundWorkers` (hosted services for queue processing and cleanup)

### 2) Shared references & packages

- Add project references to shared libs (IhsanDev.Shared.\*).
- NuGet packages:
  - Microsoft.AspNetCore.SignalR
  - Microsoft.AspNetCore.Authentication.JwtBearer
  - Npgsql.EntityFrameworkCore.PostgreSQL
  - MediatR.Extensions.Microsoft.DependencyInjection
  - FluentValidation
  - FirebaseAdmin (optional)
  - Polly (optional, for retries)

### 3) Security & multi-tenancy wiring

- Follow Identity.API `Program.cs` pattern for JWT configuration:
  - If `MultiTenancy:Enabled` and `MultiTenancy:JwtMode == "PerTenant"`, configure JwtBearer events to call Tenant Service for per-tenant signing key.
  - Ensure tenant resolution middleware runs before authentication so JwtBearerEvents/OnMessageReceived has access to resolved tenant.

### 4) SignalR Hub (NotificationHub)

- Implement `NotificationHub : Hub` in `Notification.API`:
  - OnConnectedAsync: read `x-tenant-id` header and verify it matches resolved tenant; if missing, reject connection with Context.Abort().
  - Use ClaimsPrincipal to get authenticated user id (sub claim). Add connection to groups:
    - `tenant:{tenantId}` for all connections of the tenant
    - `tenant:{tenantId}:user:{userId}` for per-user routing
  - Provide client-to-server method `AcknowledgeDelivery(Guid messageId)` (or `Received`) which calls a backend API to mark queue item delivered.
  - For scale-out, consider Azure SignalR or a backplane. Keep group names deterministic.

### 5) Global Queue — persistence & processing

- Global `NotificationDbContext` with `NotificationQueueItems` table (jsonb payload, status, attempts, expires_at, created_at).
- Add index on Status + CreatedAt for efficient dequeue and cleanup.
- Create `NotificationProcessor` (background service) in `Notification.BackgroundWorkers`:
  - Polls queue for items with Status = Pending (or Enqueued) and created_at <= now.
  - Marks item InProgress (optimistic concurrency via row version or update where status = Pending) to claim work.
  - For each item:
    - Resolve tenant DB connection string using Tenant Service cache (follow shared pattern, cache TTL 5 min).
    - Persist Notification to tenant DB (`TenantNotificationDbContext`).
    - Deliver via SignalR if recipient is connected to tenant:user group — use IHubContext to send message and await ack if waitable.
    - Fire-and-forget to Firebase for push tokens (call Identity service to fetch `UserDeviceTokens` for recipients) — handle FCM responses.
    - Update queue item status to Delivered or Failed with attempt metadata.
  - Implement retries: exponential backoff; update Attempts; set next visible time or rely on CreatedAt and Attempts logic.

### 6) Received API / Acknowledge

- Implement API controller `NotificationsController` with endpoints:
  - POST /api/notifications/send — accepts SendNotificationRequest, enqueues item in Global DB, returns queue id (and optionally wait handle id)
  - POST /api/notifications/received — used by clients (SignalR or REST) to acknowledge receipt (body: messageId, receivedAt, deliveredToChannel)
  - GET /api/notifications/{id} — fetch status and tenant notification id

### 7) Waitable notifications

- For "waitable: true" behavior: allow the API to accept a wait=true query param. Implement either:
  - Long-poll: keep HTTP connection open until delivered or timeout (complex at scale).
  - Polling: return queue id and let caller poll GET /api/notifications/{id} for final status.
  - Webhook: accept callback URL in request and POST to it when delivery finishes.

### 8) Firebase integration (optional)

- Add `FirebaseClient` in `Notification.Infrastructure`:
  - Initialize FirebaseApp using credentials configured in appsettings (service account JSON path or env var).
  - Provide method `SendToDeviceTokensAsync(IEnumerable<string> tokens, NotificationPayload payload)` that handles FCM responses. Remove invalid tokens by calling Identity API to revoke tokens for that user.

### 9) Identity changes — UserDeviceToken

- Add `UserDeviceToken` entity in Identity Domain (per earlier user request):
  - Fields: Id (guid), UserId, TenantId, DeviceToken, Platform, LastSeenAt, IsActive, CreatedAt.
  - Endpoints: POST /api/users/{id}/devices (register), DELETE /api/users/{id}/devices/{deviceId} (revoke), GET /api/users/{id}/devices (list).
  - Notification service will call Identity API to query tokens for a user: GET /api/users/{id}/device-tokens (tenant-aware).
  - Secure these endpoints: only service-to-service calls or authenticated users.

### 10) Migrations

- Create EF Core migrations for Global NotificationDbContext and TenantNotificationDbContext. Keep tenant migrations separate: tenant DB migrations may be applied per-tenant or via automation.

### 11) Tests

- Add unit tests for:
  - SendNotificationCommandHandler (happy path, missing tenant, invalid recipient)
  - NotificationProcessor (mock DbContexts, HubContext, Identity client)
  - SignalR Hub group join/abort behavior (integration-like tests using TestServer)

### 12) Observability & metrics

- Add logging (structured) for critical events: enqueue, dequeue, deliver success/failure, FCM errors, invalid tokens.
- Add health checks for Tenant Service, Identity Service, and DB connectivity.

### 13) Cleanup and expiry

- Implement a cleanup worker to delete/mark queue items older than expiry window (configurable, default 1 day) and tenant notifications older than tenant-defined retention.

### 14) Scalability notes

- For high throughput:
  - Use Azure SignalR or Redis backplane.
  - Use partitioning by tenant or hash key when polling queue to avoid contention.
  - Consider batching FCM sends per project/app to respect rate limits.

### 15) Deploy & run

- Add `Notification.API` to solution and configure appsettings.Development.json with local TenantServiceUrl and IdentityServiceUrl.
- Run migrations for global DB.
- For tenant DBs, either run migrations per tenant or run a tenant provisioning script that applies migrations when a new tenant is created.

## Verification checklist (before PR)

- All endpoints compile and pass unit tests.
- SignalR accepts connections with `x-tenant-id` header and places connection in correct groups.
- Queue items are processed and tenant notifications persisted.
- Firebase responses handled and invalid tokens removed via Identity API.
- Basic load smoke test (enqueue 1000 small notifications) to ensure processing throughput.

## Next steps for me (if you approve)

- Implement the projects and files above in small commits (one logical area per commit):
  1. Project creation + DI + configuration wiring
  2. Global DbContext + migrations + queue enqueue API
  3. SignalR Hub + OnConnected group logic + Received endpoint
  4. NotificationProcessor background worker + tests
  5. Firebase client + integration + Identity device token endpoints
  6. Cleanup worker, metrics, and docs

If you'd like, I can start with step 1 and create the project skeleton now and then proceed incrementally. Let me know which step to start with and whether to implement `UserDeviceToken` in Identity in the same PR or a separate one.
