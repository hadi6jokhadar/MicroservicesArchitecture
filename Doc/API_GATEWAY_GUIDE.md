# API Gateway Guide

**Status:** Implemented  
**Implemented:** June 3, 2026  
**Project:** `src/Gateway/Gateway.API/`  
**Port:** `http://localhost:5000`  
**Technology:** YARP (Yet Another Reverse Proxy) v2.3.0 on .NET 8

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
| `/api/admin/tenant/{**}`     | 5     | Tenant (5002)                                                   |
| `/api/admin/categories/{**}` | 5     | Category (5007)                                                 |
| `/api/admin/{**}`            | 20    | Identity (5001) — catch-all for user/role/claim admin endpoints |

This means `/api/admin/tenant/123` routes to Tenant, `/api/admin/categories/` routes to Category, and anything else under `/api/admin/` routes to Identity.

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

The gateway injects `X-Correlation-Id` on every inbound request that does not already carry one. Downstream services should read this header and include it in log entries (see `PLATFORM_CAPABILITIES_ROADMAP.md` — Tier 1 item 2: Distributed Tracing).

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

## Health Check

```
GET http://localhost:5000/health
```

Returns:

```json
{ "status": "healthy", "service": "Gateway.API", "timestamp": "..." }
```

This is a gateway-local health check only. It does **not** probe downstream services. A downstream-aggregating health check is planned (see roadmap checklist).

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
- `Routes[*].Order` — priority (lower = matched first); use `5` for specific admin routes, `10` for standard routes, `20` for admin catch-all
- `Routes[*].Match.Path` — path pattern with `{**catch-all}` wildcard
- `Routes[*].Transforms` — path rewrites and header modifications
- `Routes[*].Timeout` — per-route request timeout (used for SSE stream route: 10 minutes)
- `Clusters[*].Destinations.d1.Address` — upstream service base URL

---

## Future Work (from Roadmap)

- [ ] Update all frontend API base URLs to point to `http://localhost:5000` (dev) / production gateway URL
- [ ] Wire `/health` endpoint to aggregate all 8 downstream `/health` responses
- [ ] Add TLS termination (production)
- [ ] Consider per-service rate limit tiers (AI service may need stricter limits)
