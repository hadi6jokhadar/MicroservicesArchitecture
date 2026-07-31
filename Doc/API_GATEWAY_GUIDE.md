# API Gateway Guide

**Status:** Implemented  
**Implemented:** June 3, 2026  
**Project:** `src/Gateway/Gateway.API/`  
**Port:** `http://localhost:5000`  
**Technology:** YARP (Yet Another Reverse Proxy) v2.3.0 on .NET 10

---

## Overview

The API Gateway is the single entry point for all client-to-service traffic. It replaces the need for clients to know 9 different base URLs and provides:

- **Unified base URL** — all clients use `http://localhost:5000` (dev) or the production domain
- **Request routing** — path-based forwarding to the correct downstream service
- **Correlation ID injection** — every request gets an `X-Correlation-Id` header before it reaches a service
- **Rate limiting** — 500 requests per minute per IP to protect services from abuse
- **SSE streaming support** — AI chat stream responses forwarded correctly with extended timeout
- **Health check** — lightweight `/health` endpoint on the gateway itself
- **Request timeouts** — a conservative default timeout on every proxied route, plus an extended override for the AI streaming route
- **HTTPS redirection / HSTS** — enforced on the public-facing listener outside of Development

---

## Service Port Map

| Service      | Port | Gateway route prefix(es)                                                                                                      |
| ------------ | ---- | ----------------------------------------------------------------------------------------------------------------------------- |
| Identity     | 5001 | `/api/v1/auth/...`, `/api/v1/user/...`, `/api/v1/roles/...`, `/api/v1/claims/...`, `/api/v1/device-tokens/...`, `/api/v1/admin/users/...`, `/api/v1/admin/roles/...`, `/api/v1/admin/claims/...` |
| Tenant       | 5002 | `/api/v1/tenant/...`, `/api/v1/admin/tenant/...`                                                                              |
| Notification | 5004 | `/api/v1/notifications/...`                                                                                                   |
| FileManager  | 5005 | `/api/v1/filemanager/...`                                                                                                     |
| Translation  | 5006 | `/api/v1/translations/...`                                                                                                    |
| Category     | 5007 | `/api/v1/categories/...`, `/api/v1/admin/categories/...`                                                                      |
| AI (Python)  | 5008 | `/api/v1/ai/...` → proxied as `/api/v1/...`                                                                                   |
| Nasheed      | 5009 | `/api/v1/artists/...`, `/api/v1/songs/...`, `/api/v1/ingestion/...`, `/api/v1/search/...`, `/api/v1/generation/...`           |
| Backup       | 5010 | `/api/v1/admin/backup-targets/...`, `/api/v1/admin/backups/...`, `/api/v1/admin/restores/...`                                |

**Note:** All routes are versioned (`/api/v1/...`) per the API Versioning Standard (Section 7 of `PLATFORM_CAPABILITIES_ROADMAP.md`). Unversioned service-to-service internal endpoints (e.g. `/api/filemanager/internal/...`) and the audit-log endpoint (`/api/admin/audit-logs`) are the only exceptions — see the Admin Endpoint Routing section below.

---

## Admin Endpoint Routing

Several services expose endpoints under `/api/v1/admin/`. YARP resolves these by route priority (lower `Order` number = higher priority):

| Route                        | Order | Forwards to                                                     |
| ---------------------------- | ----- | --------------------------------------------------------------- |
| `/api/v1/admin/tenant/audit-logs`      | 4 | Tenant (5002) — path rewritten to `/api/admin/audit-logs`      |
| `/api/v1/admin/filemanager/audit-logs` | 4 | FileManager (5005) — path rewritten to `/api/admin/audit-logs` |
| `/api/v1/admin/notifications/audit-logs` | 4 | Notification (5004) — path rewritten to `/api/admin/audit-logs` |
| `/api/v1/admin/translations/audit-logs`  | 4 | Translation (5006) — path rewritten to `/api/admin/audit-logs` |
| `/api/v1/admin/categories/audit-logs`  | 4 | Category (5007) — path rewritten to `/api/admin/audit-logs`    |
| `/api/v1/admin/nasheed/audit-logs`     | 4 | Nasheed (5009) — path rewritten to `/api/admin/audit-logs`     |
| `/api/v1/admin/backup/audit-logs`      | 4 | Backup (5010) — path rewritten to `/api/admin/audit-logs`       |
| `/api/v1/admin/tenant/{**}`     | 5     | Tenant (5002)                                                   |
| `/api/v1/admin/categories/{**}` | 5     | Category (5007)                                                 |
| `/api/v1/admin/backup-targets/{**}` | 5 | Backup (5010)                                                  |
| `/api/v1/admin/backups/{**}`    | 5     | Backup (5010)                                                   |
| `/api/v1/admin/restores/{**}`   | 5     | Backup (5010)                                                   |
| `/api/v1/admin/users/{**}`      | 20    | Identity (5001)                                                 |
| `/api/v1/admin/roles/{**}`      | 20    | Identity (5001)                                                 |
| `/api/v1/admin/claims/{**}`     | 20    | Identity (5001)                                                 |

**July 2026 security audit fix:** `/api/v1/admin/{**catch-all}` used to be a single bare catch-all covering everything not matched above. Because Notification, FileManager, Translation, and Nasheed each only expose a single audit-log route under `/admin` (Order 4) with no general admin route of their own, any *other* unclaimed `/api/v1/admin/*` path (e.g. `/api/v1/admin/notifications/anything-else`) silently fell through and forwarded to Identity instead of 404ing at the gateway edge. Fixed by scoping the Identity route to its three actual admin prefixes — `users`, `roles`, `claims` — matching `MapAdminEndpoints`/`MapRoleEndpoints`/`MapClaimEndpoints` in `Identity.API/Extensions/EndpointMappingExtensions.cs`. Any other `/api/v1/admin/*` path now 404s at the gateway.

### Audit Log Endpoints

Every service exposes `GET /api/admin/audit-logs` internally (unversioned — shared infrastructure endpoint, stays stable). The gateway maps each service to a unique versioned public path using YARP path rewriting (`PathPattern` transform). Query string parameters (`page`, `pageSize`, `tenantId`, etc.) are forwarded automatically.

| Frontend calls (via gateway)                   | Service queried  |
| ----------------------------------------------- | ---------------- |
| `GET /api/v1/admin/audit-logs`                 | Identity (5001)  |
| `GET /api/v1/admin/tenant/audit-logs`          | Tenant (5002)    |
| `GET /api/v1/admin/notifications/audit-logs`   | Notification (5004) |
| `GET /api/v1/admin/filemanager/audit-logs`     | FileManager (5005) |
| `GET /api/v1/admin/translations/audit-logs`    | Translation (5006) |
| `GET /api/v1/admin/categories/audit-logs`      | Category (5007)  |
| `GET /api/v1/admin/nasheed/audit-logs`         | Nasheed (5009)   |
| `GET /api/v1/admin/backup/audit-logs`          | Backup (5010)    |

All routes require `Admin` or `SuperAdmin` role. See `NEW_SERVICE_INTEGRATION_GUIDE.md` for the full query parameter reference.

---

## AI Service Path Rewriting

The AI Python service (FastAPI) uses paths like `/api/v1/chat/single` and `/api/v1/chat/stream` internally. The gateway exposes them under `/api/v1/ai/...` and rewrites the path before forwarding:

| Client calls                  | Gateway forwards to AI service |
| ----------------------------- | ------------------------------ |
| `POST /api/v1/ai/chat/single` | `POST /api/v1/chat/single`     |
| `POST /api/v1/ai/chat/stream` | `POST /api/v1/chat/stream`     |

The **stream route** (`ai-stream-route`) has additional configuration:

- `Timeout`: 10 minutes (SSE connections stay open while model generates)
- `ResponseHeaderRemove: Content-Length` (SSE responses have no fixed length)

---

## Request Timeouts

`Program.cs` calls `AddRequestTimeouts(options => options.DefaultPolicy = ...)` with a **100-second `DefaultPolicy`** applied to every proxied route that doesn't declare its own YARP `Timeout` value. Before the July 2026 security audit, only `ai-stream-route`'s per-route `Timeout` was set — every other route relied solely on Kestrel's minimum-data-rate heuristics with no explicit request-level ceiling. The `DefaultPolicy` does not affect `ai-stream-route`'s own 10-minute override (per-route YARP `Timeout` values take precedence over the middleware default).

```csharp
// Gateway — Program.cs
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(100)
    };
});
```

`app.UseRequestTimeouts()` must sit between routing and endpoint execution (already the case) for both the default policy and the per-route YARP `Timeout` values to be enforced.

---

## HTTPS Redirection / HSTS

Outside of `Development`, the gateway calls `UseHsts()` and `UseHttpsRedirection()` on its own public-facing listener — gated the same way every other service in this repo gates it (see `Notification.API`'s `Program.cs`):

```csharp
// Gateway — Program.cs
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
```

This only affects the gateway's own inbound listener. It does **not** change the YARP `Destinations` in `appsettings.json` — those stay `http://` intentionally, since they're internal, plain-HTTP calls to other services on the same Docker network, not public-facing.

---

## Service-to-Service Calls — Do NOT Use the Gateway

Internal calls between .NET services must go **direct** to the target service port, bypassing the gateway entirely. Using the gateway for internal calls adds latency and ties internal availability to gateway availability.

```json
// Correct — service appsettings.json points direct:
"AiService": { "BaseUrl": "http://localhost:5008" }
"NotificationService": { "BaseUrl": "http://localhost:5004" }
// NOT:
"AiService": { "BaseUrl": "http://localhost:5000" }  // ❌ Never route internal calls through gateway
```

---

## Correlation ID

The gateway **always generates a fresh, server-side** `X-Correlation-Id` and overwrites whatever the client sent — it never trusts a client-supplied value:

```csharp
// Gateway — Program.cs
app.Use(async (ctx, next) =>
{
    ctx.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();
    await next();
});
```

**July 2026 security audit fix:** this used to only fill in the header when absent (`if (!ctx.Request.Headers.ContainsKey(...))`), which let a client-supplied `X-Correlation-Id` pass straight through unverified to every backend's logs/traces — allowing log/trace forgery (a malicious or buggy client could plant an arbitrary ID, or collide with another request's real ID). The gateway is the single point where the real client boundary is crossed, so it now always mints its own ID here, regardless of what the client sent.

Every downstream service reads the header, stores it in `HttpContext.Items`, echoes it back in the response, and enriches the structured log scope for the entire request lifetime via `CorrelationIdMiddleware` (in `IhsanDev.Shared.Infrastructure`):

```csharp
// Each service — Program.cs
app.UseCorrelationId();   // must be called before UseLocalization/UseGlobalExceptionHandler
```

The frontend `correlationIdInterceptor` (`libs/core/src/lib/interceptors/`) reads the echoed response header and sends the same ID on all subsequent requests, creating a continuous trace chain: browser → gateway → service → logs. Since the gateway now always overwrites the header, any ID the frontend previously stored and resent is replaced with a new one at the gateway on every hop — the chain is still continuous end-to-end for a single request/response pair, it's just no longer client-controlled across requests.

**End-to-end trace example:**

```
Browser sends:   X-Correlation-Id: abc-123 (or nothing)
Gateway:         overwrites with a fresh server-generated ID — xyz-789
Identity logs:   CorrelationId: xyz-789  (in every log line for this request)
Response header: X-Correlation-Id: xyz-789
Browser stores:  "xyz-789" → sent on next request (gateway will overwrite it again)
```

---

## Rate Limiting — Split Responsibility

Rate limiting is split by concern: the gateway owns what only it can enforce, services own what requires JWT context.

| Limit type              | Lives at         | Reason                                                                                 |
| ----------------------- | ---------------- | -------------------------------------------------------------------------------------- |
| Global (total requests) | **Gateway only** | Single chokepoint for the entire platform                                              |
| PerIP                   | **Gateway only** | Only the gateway sees the real client IP — services see the gateway's loopback address |
| PerTenant               | **Service only** | Requires the `tenant_id` JWT claim — gateway has no JWT context                        |
| PerUser                 | **Service only** | Requires the `sub` JWT claim — same reason                                             |

### Gateway policies (`src/Gateway/Gateway.API/Program.cs`)

All three gateway-level limiters use a **token bucket** algorithm, not a fixed window — a fixed window lets a client spend its whole quota in the last instant of one window and again in the first instant of the next (up to 2x the intended rate in a short burst). A token bucket allows an immediate burst up to `TokenLimit`, then throttles to a steady `TokensPerPeriod` / `ReplenishmentSeconds` rate — better behavior for real traffic, which is spiky, not uniform.

| Policy | Partition | `TokenLimit` (burst) | `TokensPerPeriod` (sustained rate) | Config key |
|---|---|---|---|---|
| GlobalLimiter | platform-wide, single bucket | 20,000 | 5,000/s | `RateLimiting:Global` |
| per-ip (general API) | per client IP | 200 | 50/s | `RateLimiting:PerIp` |
| per-ip (auth) | per client IP, **separate** bucket from general API | 20 | 5/s | `RateLimiting:PerIpAuth` |
| health-aggregate | per client IP | 60 | 20/s | `RateLimiting:HealthAggregate` |

Rejection returns HTTP `429 Too Many Requests`.

**`GlobalLimiter` applies to every request through `UseRateLimiter()` by default — there is no opt-in required, unlike a named policy.** An endpoint that should never compete with real API traffic for that budget must explicitly call `.DisableRateLimiting()`. Only the lightweight `/health` endpoint does this. A load test in July 2026 found that without it, `/health` shared the same global budget as proxied API traffic and started returning 429s above a moderate sustained rate — see `LOAD_TESTING_GUIDE.md` for the full investigation.

**`/health/aggregate` does NOT use `.DisableRateLimiting()`** — it uses the named `health-aggregate` policy instead (`.RequireRateLimiting("health-aggregate")`, still `.AllowAnonymous()`). A July 2026 security audit found the previous `.DisableRateLimiting()` bypassed both the `GlobalLimiter` and the `per-ip` policy for this one route, and since it fans out to a 9-way `Task.WhenAll` downstream call per hit, an unauthenticated flood multiplied into a 9x amplification against every downstream service with no ceiling at all. The `health-aggregate` policy is generous (60 burst / 20 per second per IP) — enough for normal monitoring/LB polling — but no longer unlimited. When adding any new infrastructure/probe endpoint to the gateway, decide per-endpoint whether `.DisableRateLimiting()` (truly cheap, no fan-out) or a dedicated named policy (any fan-out or non-trivial cost per hit) is appropriate — don't default to `.DisableRateLimiting()` just because the endpoint is unauthenticated.

**Auth has its own per-IP bucket, separate from general API traffic** (`RateLimiting:PerIpAuth`), keyed by `{ip}:auth` vs `{ip}:api` inside the same `"per-ip"` named policy (branches on whether `context.Request.Path` starts with `/api/v1/auth`). This means a burst of unrelated API calls from a shared NAT/office IP can throttle general traffic without ever affecting that IP's ability to log in — verified by a concurrent load test: hammering a general endpoint at 3x its sustained rate produced ~58% 429s on that endpoint while 100% of simultaneous login attempts from the same IP succeeded.

The `Global`/`PerIp`/`PerIpAuth` numbers above are a starting point sized to comfortably absorb high legitimate traffic (the Global burst alone covers 20,000 requests instantly, with 5,000/s sustained after) while still bounding a genuine runaway/DDoS scenario — they are not derived from a measured backend capacity ceiling. Re-tune after load-testing the per-tenant database connection pooling fix (see `LOAD_TESTING_GUIDE.md`), which is currently the more likely real ceiling once rate limiting stops masking it.

### Service policies (Identity, Category, FileManager, Tenant, Notification)

- **PerTenant** — partitioned by `x-tenant-id` header (populated from JWT `tenant_id` claim by auth middleware)
- **PerUser** — partitioned by `sub` claim from the authenticated user

Limits are configurable via `appsettings.json` `RateLimiting:PerTenant:PermitLimit` / `RateLimiting:PerUser:PermitLimit`.

### Services with no service-level rate limiting (Nasheed, Translation)

These services had only Global + PerIP which have been removed (gateway handles them). No `AddRateLimiter`/`UseRateLimiter` in their pipelines.

---

## Health Checks

### Gateway liveness (`/health`)

```
GET http://localhost:5000/health
```

```json
{ "status": "healthy", "service": "Gateway.API", "timestamp": "2026-06-05T..." }
```

Lightweight — gateway process only. Always fast. Safe to use as a load-balancer liveness probe — explicitly exempt from `GlobalLimiter` via `.DisableRateLimiting()`, so LB/k8s probes never compete with real API traffic for the rate-limit budget.

### Aggregate downstream health (`/health/aggregate`)

```
GET http://localhost:5000/health/aggregate
```

Calls all 9 downstream `/health` endpoints in parallel (5-second timeout each). Reads cluster addresses from the YARP `ReverseProxy:Clusters` config, so it stays in sync with the routing table automatically. Rate-limited via the dedicated `health-aggregate` policy (60 burst / 20 per second per IP, see Rate Limiting section above) rather than `.DisableRateLimiting()` — this endpoint fans out to 9 downstream calls per single incoming request, so an unbounded/unauthenticated caller could multiply into a 9x amplification against every downstream service. Still `.AllowAnonymous()` — health checks remain reachable without auth — just no longer unlimited.

```json
{
  "status": "healthy",
  "gateway": "healthy",
  "timestamp": "2026-06-05T...",
  "services": {
    "identity":     "healthy",
    "tenant":       "healthy",
    "notification": "healthy",
    "filemanager":  "healthy",
    "translation":  "healthy",
    "category":     "healthy",
    "nasheed":      "healthy",
    "ai":           "healthy",
    "backup":       "healthy"
  }
}
```

`status` is `"healthy"` when all services are healthy, `"degraded"` if any service is `"unhealthy"` or `"unreachable"`.

### Service-level health (each service)

All services expose:

| Endpoint | Purpose | Auth |
|---|---|---|
| `GET /health` | JSON report: DB check + service liveness, with durations | Anonymous |
| `GET /health/ready` | Simple readiness probe for load balancers | Anonymous |

Example response from any service:

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "identity-database", "status": "Healthy", "description": null, "duration": 2.1 },
    { "name": "identity-service",  "status": "Healthy", "description": "Identity service is running", "duration": 0.0 }
  ],
  "totalDuration": 2.1
}
```

**Nasheed** only includes a service-level check (no DB probe) because its database connection string comes from the tenant config, not `appsettings.json`.

---

## Running the Gateway

**Development (standalone):**

```powershell
cd "src\Gateway\Gateway.API"
run-development-instance.bat
```

**Via start-all-services:**

```powershell
cd "src\Services"
node start-all-services.mjs
```

The gateway is included at the end of the startup sequence (labeled **Gateway API**, red tab).

---

## Project Structure

```
src/Gateway/
└── Gateway.API/
    ├── Program.cs                    # YARP + rate limiter + request timeouts + correlation ID + HTTPS redirection/HSTS
    ├── appsettings.json              # Full YARP routing table for all 9 services
    ├── appsettings.Development.json  # Dev overrides (empty by default)
    ├── Gateway.API.csproj
    ├── run-development-instance.bat
    └── Properties/
        └── launchSettings.json       # Port 5000
```

---

## Configuration Reference — appsettings.json

The `ReverseProxy` section follows the [YARP configuration documentation](https://microsoft.github.io/reverse-proxy/articles/config-files.html).

Key fields:

- `Routes[*].ClusterId` — which cluster (service) to forward to
- `Routes[*].Order` — priority (lower = matched first); use `4` for audit-log routes with path rewrite, `5` for specific admin routes, `10` for standard routes, `20` for Identity's scoped admin routes (`users`/`roles`/`claims`)
- `Routes[*].Match.Path` — path pattern with `{**catch-all}` wildcard
- `Routes[*].Transforms` — path rewrites and header modifications
- `Routes[*].Timeout` — per-route request timeout (used for SSE stream route: 10 minutes)
- `Clusters[*].Destinations.d1.Address` — upstream service base URL

---

## Future Work (from Roadmap)

- [ ] Update all frontend API base URLs to point to `http://localhost:5000` (dev) / production gateway URL
- [x] Wire `/health/aggregate` to probe all downstream `/health` endpoints — **implemented June 2026**
- [x] Bound `/health/aggregate`'s rate limit instead of `.DisableRateLimiting()` — **implemented July 2026 (security audit)**
- [x] Scope the Identity admin catch-all to its real prefixes (`users`/`roles`/`claims`) instead of a bare catch-all — **implemented July 2026 (security audit)**
- [x] Always regenerate `X-Correlation-Id` server-side instead of trusting a client-supplied value — **implemented July 2026 (security audit)**
- [x] Add a default request-timeout policy for routes with no per-route `Timeout` — **implemented July 2026 (security audit)**
- [x] Gate `UseHsts()`/`UseHttpsRedirection()` on the gateway's own listener outside Development — **implemented July 2026 (security audit)**
- [ ] Add TLS termination (production) — the July 2026 HSTS/HTTPS-redirection fix assumes TLS is already terminated in front of (or by) the gateway; actual certificate/TLS setup for the public listener is still open
- [ ] Consider per-service rate limit tiers (AI service may need stricter limits)
- [ ] Design and add gateway-level JWT authentication for anonymous-by-default routes — deliberately out of scope for the July 2026 security-audit pass pending a design discussion enumerating every route that must stay anonymous (login, register, OTP flows, forgot-password, public tenant lookup, health, refresh)
