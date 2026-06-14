# API Gateway Guide

**Status:** Implemented  
**Implemented:** June 3, 2026  
**Project:** `src/Gateway/Gateway.API/`  
**Port:** `http://localhost:5000`  
**Technology:** YARP (Yet Another Reverse Proxy) v2.3.0 on .NET 9

---

## Overview

The API Gateway is the single entry point for all client-to-service traffic. It replaces the need for clients to know 8 different base URLs and provides:

- **Unified base URL** — all clients use `http://localhost:5000` (dev) or the production domain
- **Request routing** — path-based forwarding to the correct downstream service
- **Correlation ID injection** — every request gets an `X-Correlation-Id` header before it reaches a service
- **Rate limiting** — 500 requests per minute per IP to protect services from abuse
- **SSE streaming support** — AI chat stream responses forwarded correctly with extended timeout
- **Health check** — lightweight `/health` endpoint on the gateway itself

---

## Service Port Map

| Service      | Port | Gateway route prefix(es)                                                                                                      |
| ------------ | ---- | ----------------------------------------------------------------------------------------------------------------------------- |
| Identity     | 5001 | `/api/auth/...`, `/api/user/...`, `/api/roles/...`, `/api/claims/...`, `/api/device-tokens/...`, `/api/admin/...` (catch-all) |
| Tenant       | 5002 | `/api/tenant/...`, `/api/admin/tenant/...`                                                                                    |
| Notification | 5004 | `/api/notifications/...`                                                                                                      |
| FileManager  | 5005 | `/api/filemanager/...`                                                                                                        |
| Translation  | 5006 | `/api/translations/...`                                                                                                       |
| Category     | 5007 | `/api/categories/...`, `/api/admin/categories/...`                                                                            |
| AI (Python)  | 5008 | `/api/v1/ai/...` → proxied as `/api/v1/...`                                                                                   |
| Nasheed      | 5009 | `/api/artists/...`, `/api/songs/...`, `/api/ingestion/...`, `/api/search/...`, `/api/generation/...`                          |

---

## Admin Endpoint Routing

Several services expose endpoints under `/api/admin/`. YARP resolves these by route priority (lower `Order` number = higher priority):

| Route                        | Order | Forwards to                                                     |
| ---------------------------- | ----- | --------------------------------------------------------------- |
| `/api/admin/tenant/audit-logs`      | 4 | Tenant (5002) — path rewritten to `/api/admin/audit-logs`      |
| `/api/admin/filemanager/audit-logs` | 4 | FileManager (5005) — path rewritten to `/api/admin/audit-logs` |
| `/api/admin/notifications/audit-logs` | 4 | Notification (5004) — path rewritten to `/api/admin/audit-logs` |
| `/api/admin/translations/audit-logs`  | 4 | Translation (5006) — path rewritten to `/api/admin/audit-logs` |
| `/api/admin/categories/audit-logs`  | 4 | Category (5007) — path rewritten to `/api/admin/audit-logs`    |
| `/api/admin/nasheed/audit-logs`     | 4 | Nasheed (5009) — path rewritten to `/api/admin/audit-logs`     |
| `/api/admin/tenant/{**}`     | 5     | Tenant (5002)                                                   |
| `/api/admin/categories/{**}` | 5     | Category (5007)                                                 |
| `/api/admin/{**}`            | 20    | Identity (5001) — catch-all; also serves Identity audit-logs at `/api/admin/audit-logs` |

### Audit Log Endpoints

Every service exposes `GET /api/admin/audit-logs` internally. The gateway maps each service to a unique public path using YARP path rewriting (`PathPattern` transform). Query string parameters (`page`, `pageSize`, `tenantId`, etc.) are forwarded automatically.

| Frontend calls (via gateway)                | Service queried  |
| ------------------------------------------- | ---------------- |
| `GET /api/admin/audit-logs`                 | Identity (5001)  |
| `GET /api/admin/tenant/audit-logs`          | Tenant (5002)    |
| `GET /api/admin/notifications/audit-logs`   | Notification (5004) |
| `GET /api/admin/filemanager/audit-logs`     | FileManager (5005) |
| `GET /api/admin/translations/audit-logs`    | Translation (5006) |
| `GET /api/admin/categories/audit-logs`      | Category (5007)  |
| `GET /api/admin/nasheed/audit-logs`         | Nasheed (5009)   |

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

The gateway injects `X-Correlation-Id` on every inbound request that does not already carry one:

```csharp
// Gateway — Program.cs
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Headers.ContainsKey("X-Correlation-Id"))
        ctx.Request.Headers.Append("X-Correlation-Id", Guid.NewGuid().ToString());
    await next();
});
```

Every downstream service reads the header, stores it in `HttpContext.Items`, echoes it back in the response, and enriches the structured log scope for the entire request lifetime via `CorrelationIdMiddleware` (in `IhsanDev.Shared.Infrastructure`):

```csharp
// Each service — Program.cs
app.UseCorrelationId();   // must be called before UseLocalization/UseGlobalExceptionHandler
```

The frontend `correlationIdInterceptor` (`libs/core/src/lib/interceptors/`) reads the echoed response header and sends the same ID on all subsequent requests, creating a continuous trace chain: browser → gateway → service → logs.

**End-to-end trace example:**

```
Browser sends:   X-Correlation-Id: abc-123
Gateway:         echoes it through (already present — no new ID needed)
Identity logs:   CorrelationId: abc-123  (in every log line for this request)
Response header: X-Correlation-Id: abc-123
Browser stores:  "abc-123" → sent on next request
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

- **GlobalLimiter** — 10,000 requests/minute across all clients combined (platform-wide cap)
- **per-ip** policy — 500 requests/minute per client IP address

Rejection returns HTTP `429 Too Many Requests`.

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

Lightweight — gateway process only. Always fast. Safe to use as a load-balancer liveness probe.

### Aggregate downstream health (`/health/aggregate`)

```
GET http://localhost:5000/health/aggregate
```

Calls all 8 downstream `/health` endpoints in parallel (5-second timeout each). Reads cluster addresses from the YARP `ReverseProxy:Clusters` config, so it stays in sync with the routing table automatically.

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
    "ai":           "healthy"
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
    ├── Program.cs                    # YARP + rate limiter + correlation ID middleware
    ├── appsettings.json              # Full YARP routing table for all 8 services
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
- `Routes[*].Order` — priority (lower = matched first); use `4` for audit-log routes with path rewrite, `5` for specific admin routes, `10` for standard routes, `20` for admin catch-all
- `Routes[*].Match.Path` — path pattern with `{**catch-all}` wildcard
- `Routes[*].Transforms` — path rewrites and header modifications
- `Routes[*].Timeout` — per-route request timeout (used for SSE stream route: 10 minutes)
- `Clusters[*].Destinations.d1.Address` — upstream service base URL

---

## Future Work (from Roadmap)

- [ ] Update all frontend API base URLs to point to `http://localhost:5000` (dev) / production gateway URL
- [x] Wire `/health/aggregate` to probe all downstream `/health` endpoints — **implemented June 2026**
- [ ] Add TLS termination (production)
- [ ] Consider per-service rate limit tiers (AI service may need stricter limits)
