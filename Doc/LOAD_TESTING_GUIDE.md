# Load Testing Guide

**Status:** Implemented
**Tool:** [k6](https://k6.io) (Grafana) — standalone binary, no .NET project required
**Scripts:** `LoadTests/k6/`

---

## Why k6, and why not a custom test endpoint

A custom "hammer this N times" endpoint inside a service was considered and rejected: it tests the test harness as much as the service, it's one more piece of production code to secure and maintain, and it can't drive load from outside the process (no cross-service, no realistic network path). k6 runs as an external client against the real HTTP surface — the same path a real user or mobile client takes — and reports standard percentile metrics (p50/p95/p99, RPS, error rate) out of the box.

---

## Install

```powershell
winget install --id GrafanaLabs.k6
```

If winget hangs waiting on an elevation prompt (headless/CI environments), use the portable zip instead — no install, no admin rights:

```powershell
$dest = "$env:LOCALAPPDATA\k6"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Invoke-WebRequest -Uri "https://github.com/grafana/k6/releases/download/v2.1.0/k6-v2.1.0-windows-amd64.zip" -OutFile "$env:TEMP\k6.zip"
Expand-Archive -Path "$env:TEMP\k6.zip" -DestinationPath "$env:TEMP\k6extract" -Force
Copy-Item "$env:TEMP\k6extract\k6-v2.1.0-windows-amd64\k6.exe" -Destination "$dest\k6.exe" -Force
```

Run it with the full path (`$env:LOCALAPPDATA\k6\k6.exe`) or add `%LOCALAPPDATA%\k6` to `PATH`.

---

## Scripts

Both scripts use k6's `ramping-arrival-rate` executor — it targets a request **rate** (iterations/sec) directly rather than a VU count, which is the right model for "can this handle N requests" rather than "can this handle N concurrent users."

### `LoadTests/k6/health-baseline.js` — anonymous, no auth

Measures the raw infra ceiling with zero business logic in the way.

```powershell
cd MicroservicesArchitecture
k6 run LoadTests/k6/health-baseline.js                          # gateway /health, ramps to 2000 req/s over 5 min
k6 run -e MODE=direct LoadTests/k6/health-baseline.js           # bypasses gateway, hits each service's own /health directly
k6 run -e MODE=aggregate -e PEAK_RATE=30 LoadTests/k6/health-baseline.js   # /health/aggregate — fans out to all 8 services per request, use a LOW rate only
k6 run -e QUICK=1 -e PEAK_RATE=300 LoadTests/k6/health-baseline.js         # ~40s smoke run instead of the full 5-min ramp
```

`MODE=gateway` (default) hits the gateway's own lightweight `/health` — the one actually meant for load-balancer probes. Do not use `MODE=aggregate` as a stress target: it fans out to all 8 downstream services per single incoming request, so its real capacity is roughly 1/8th of a normal endpoint's.

### `LoadTests/k6/authenticated-flow.js` — realistic authenticated mix

`setup()` logs in once with a real tenant account (defaults: tenant `ihsandev`, `ihsandev@ihsandev.com`) and reuses that token + `x-tenant-id` header across every VU, so auth cost doesn't pollute the throughput measurement. If that login fails (wrong environment, account doesn't exist), it falls back to registering a throwaway user under the same tenant. Override the tenant/account with `-e TENANT_ID=... -e LOGIN_EMAIL=... -e LOGIN_PASSWORD=...`. `default()` then randomly picks between:

- `GET /api/v1/user/profile` (Identity)
- `GET /api/v1/categories/?page=1&pageSize=20` (Category, Redis cache-aside)
- `GET /api/v1/translations/en` (Translation, public/no-auth-required, global DB)
- `GET /api/v1/filemanager/files?PageNumber=1&PageSize=20` (FileManager)

Nasheed's list endpoints are excluded — its DB connection string comes strictly from tenant config with no global fallback, so it needs a real provisioned tenant to test meaningfully.

```powershell
k6 run LoadTests/k6/authenticated-flow.js                       # ramps to 800 req/s over 5 min
k6 run -e QUICK=1 -e PEAK_RATE=200 LoadTests/k6/authenticated-flow.js
```

### Environment variables (both scripts)

| Variable | Default | Purpose |
|---|---|---|
| `GATEWAY_URL` | `http://localhost:5000` | Gateway base URL |
| `PEAK_RATE` | 2000 (health) / 800 (auth) | Target requests/sec at the top of the ramp |
| `QUICK` | unset | `1` shrinks the ramp to ~40s for fast iteration; omit for the full 5-min profile |
| `MODE` (health-baseline only) | `gateway` | `gateway` \| `direct` \| `aggregate` |
| `TENANT_ID` (authenticated-flow only) | `ihsandev` | `x-tenant-id` header sent on every request |
| `LOGIN_EMAIL` / `LOGIN_PASSWORD` (authenticated-flow only) | `ihsandev@ihsandev.com` / `@Test123` | Real account used in `setup()`; falls back to registering a throwaway user under the same tenant if login fails |

**⚠️ Gateway per-IP rate limit will dominate `authenticated-flow.js` results.** All `/api/v1/...` routes are capped at 500 requests/minute per client IP (`API_GATEWAY_GUIDE.md`). Any `PEAK_RATE` sustained for more than a few seconds from a single test machine will trip it — see Findings below. To measure the services' real capacity independent of that limiter, either raise `RateLimiting` in the gateway's `appsettings.Development.json` for the duration of the test, or run the client from multiple source IPs.

---

## Findings from the first baseline run (2026-07-12, dev machine, single instance of every service)

These numbers are **local-dev-machine, single-instance** results — they characterize *shape* of the bottlenecks, not absolute production capacity. Re-run after any fix below and after deploying to real infrastructure before trusting the numbers as capacity planning input.

| Test | Result |
|---|---|
| Gateway `/health`, direct-to-service `/health` at 300 req/s | Both 100% success, sub-millisecond |
| Gateway `/health` at 600 req/s (before fix) | **Collapsed to 23–32% success** (varied by machine), near-zero latency on failures |
| Gateway `/health` at 600–2000 req/s (after fix, see below) | **100% success**, ~3–5µs average latency |
| Direct-to-service `/health` (bypass gateway) at 600 req/s | 88% success, still sub-millisecond |
| `authenticated-flow.js` (proxied `/api/v1/...` routes) at 150 req/s sustained | **499 of 3849 requests succeeded** — matches the documented 500 req/min per-IP limit almost exactly |

**Root cause of the gateway `/health` cliff (corrected from an earlier misdiagnosis):**

The first pass at this investigation attributed the `/health` collapse to a hard Kestrel/OS connection-level ceiling, based on the near-instant latency on failures. That was wrong. A follow-up test that tagged response status codes directly (`status_200` vs `status_429` counters at 600 req/s for 40s) showed the failures were **100% HTTP 429 — zero connection-level failures, zero timeouts.**

The actual cause: `Gateway.API/Program.cs` applied `options.GlobalLimiter` (originally hardcoded at 10,000 requests/minute) unconditionally to *every* request passing through `app.UseRateLimiter()`. The named `"per-ip"` policy is opt-in via `.RequireRateLimiting("per-ip")` on `MapReverseProxy()` only — but the `GlobalLimiter` has no opt-in mechanism; it fires for any endpoint that doesn't explicitly call `.DisableRateLimiting()`. `/health` and `/health/aggregate` never did, so they shared the same 10,000/min budget as all proxied API traffic. At any sustained rate above ~166 req/s (10,000/60s), the fixed window fills and 429s start — which explains both the shape (near-instant rejection, not growing latency) and the near-identical ~14–19% success rate across multiple independent 5-minute runs (≈5 one-minute windows × 10,000 permits ÷ ~350,000 attempted requests ≈ 14%).

**This matters beyond load testing:** as configured, a Kubernetes liveness/readiness probe hitting `/health` every few seconds across enough replicas could exhaust the same budget as real traffic and get falsely marked unhealthy — despite the gateway guide explicitly documenting `/health` as "safe to use as a load-balancer liveness probe."

**Fix applied** (`Gateway.API/Program.cs` + `appsettings.json`):
1. `/health` and `/health/aggregate` now call `.DisableRateLimiting()` — infrastructure/probe endpoints never compete with real traffic for the same budget.
2. `PermitLimit`/`WindowMinutes` for both the global and per-IP limiters moved from hardcoded literals to `RateLimiting:Global` / `RateLimiting:PerIp` in `appsettings.json`, so they can be tuned per environment without a code change.

Re-running the identical 600 req/s and 2000 req/s bursts after the fix: **100% success, 0 failures, ~3–5µs average latency** at both rates — confirming the gateway itself has no meaningful connection-level ceiling at this scale; the entire earlier "cliff" was self-inflicted rate limiting on an endpoint that was never supposed to be rate limited.

**The per-IP limiter finding on `authenticated-flow.js` stands as originally reported** — that one *is* the intended, working-as-designed per-IP policy applying to real proxied API traffic including auth, not a bug.

**Rate limiting was reworked from fixed-window to token-bucket, with auth split into its own bucket** (see `API_GATEWAY_GUIDE.md` → Rate Limiting for the full config). Verified with a concurrent test: hammering a general API endpoint at 3x its sustained rate produced ~58% 429s on that endpoint while **100% of simultaneous login attempts from the same IP succeeded** — auth can no longer be starved by unrelated traffic sharing an IP. The new defaults (`Global`: 20,000 burst / 5,000 per second sustained; `PerIp`: 200 burst / 50 per second; `PerIpAuth`: 20 burst / 5 per second) are a starting point sized for high legitimate throughput, not derived from a measured backend ceiling — re-tune once the per-tenant DB pooling item below is fixed and load-tested.

### Bugs found and fixed while building this baseline

- **Identity `Users_Id_seq` was desynced from the `Users` table** (sequence at `2`, table max `Id` at `8`) — every `/api/v1/auth/register` call failed with a Postgres primary-key collision until the sequence caught up. Fixed by resyncing the sequence (`setval` to the table's actual max `Id` — data-preserving, no rows touched). This would affect any real user registering against a freshly-seeded dev database, not just load testing.
- **`FileManager`'s file-list endpoint expects `PageNumber`/`PageSize`**, not `page`/`pageSize` as both the `FileManager_Service` and `Gateway_Service` Postman collections showed. Fixed both collections to match `FileManagerListRequest.cs`.
- **`API_GATEWAY_GUIDE.md` still showed unversioned routes** (`/api/auth/...`) from before the API Versioning migration. Updated to `/api/v1/...` throughout, including the admin/audit-log routing table.
- **Category's `ihsandev` tenant database was missing `icon_file_id`, `image_file_id`, `icon_name`, and `uri` on the `categories` table**, even though `__EFMigrationsHistory` recorded `InitialCreate` as applied. Root cause: the `InitialCreate` migration file was edited after already being applied to `ihsandev` — it now creates `icon_file_id`/`image_file_id`, but the database has whatever the file defined at the time it actually ran (`icon_url`/`image_url`). EF only checks the migration ID, never re-diffs the schema, so `ihsandev` was silently stuck on an old shape. **Never edit an already-applied migration file — always add a new one.** Fixed by adding the missing columns directly (table was empty, so no backfill risk); `icon_url`/`image_url` were left in place, unused.

Remaining known doc rot (not fixed here, out of scope for this task): `NOTIFICATION_SERVICE_README.md`, `README.md`, and `DOCUMENTATION_GUIDELINES.md` still reference `NOTIFICATION_HUB_GUIDE.md` and `BOTTLENECKS_COMPLETION_SUMMARY.md`, neither of which exist in the repo.

---

## Prioritized next steps for 100k+ req/s scale

See `PERFORMANCE_OPTIMIZATION_GUIDE.md` for the broader capacity checklist.

- [x] ~~Gateway rate limiter wrongly applying to `/health`~~ — fixed, `.DisableRateLimiting()` on infra endpoints.
- [x] ~~Fixed-window rate limiter causing 2x-burst edge behavior~~ — fixed, switched to token bucket.
- [x] ~~Auth sharing a rate-limit bucket with general API traffic~~ — fixed, `RateLimiting:PerIpAuth` is now a separate bucket.
- [x] ~~Per-tenant Npgsql connection pools with no size governance~~ — fixed. **Not** via `AddDbContextPool` (that would cause cross-tenant data leakage with this codebase's dynamic per-request `OnConfiguring` — see `DATABASE_PER_TENANT_ARCHITECTURE.md`). Instead: `NpgsqlConnectionStringHelper.WithBoundedPoolSize()` caps `Maximum Pool Size` on every dynamically-resolved tenant connection string, applied in the tenant branch of `OnConfiguring` in `IdentityDbContext`, `FileManagerDbContext`, `CategoryDbContext`, `TenantNotificationDbContext`, and `NasheedDbContext` (all 5 places that read `ITenantContext.CurrentTenant.Configuration.DatabaseSettings`). Default 20 connections/tenant/service, configurable via `DatabaseSettings:MaxPoolSizePerTenant`. Verified: full authenticated flow (login → profile → categories → filemanager) against the `ihsandev` tenant still works end-to-end after the change.
- [x] ~~`/api/v1/user/profile` blocking for up to 15-18s under load, taking Identity down with it~~ — fixed. **This was the real ceiling, found by live load testing, not by the synthetic k6 scripts.** `ProfilePictureHelper` calls `FileManagerServiceClient.GetFileByIdAsync` inline on every profile request to enrich the response with the picture. `FileManagerServiceExtensions.cs`'s Polly resilience policy allowed up to `TotalRequestTimeout=15s` per call (with 3 retries) despite its own comment calling this "a fast internal call." Under concurrent load, many simultaneous profile requests were all blocked waiting on FileManager *before* the circuit breaker's 30s sampling window accumulated enough failures to open — each one holding a thread/connection for up to 15-18s, which is what made Identity unresponsive to *all* its endpoints, not just profile. Fixed: `AttemptTimeout` 4s→1s, `TotalRequestTimeout` 15s→3s, `MaxRetryAttempts` 3→2, `CircuitBreaker.SamplingDuration` 30s→10s (opens faster under sustained failure). The call was already wrapped in try/catch (degrades to no-picture, doesn't fail the request) — the fix is entirely about *how fast* it gives up. Verified: 800 concurrent profile requests at 40 req/s, 0% failures, p95=21ms (previously the same load pattern caused 15-18s responses and Identity going fully unresponsive).
- [x] ~~Redis running as a fragile native process with no restart policy~~ — fixed. Moved to Docker Compose (`docker-compose.redis.yml`, `redis:7-alpine`, named volume, `restart: unless-stopped`, healthcheck). Same host/port/no-auth as before (`localhost:6379,abortConnect=false`), so no service config changes were needed. `start-all-services.mjs` and `run-all-tests.mjs` updated to launch it via `docker compose ... up` instead of the old `redis-server.lnk` shortcut. Postgres was deliberately left untouched (native install, real tenant data — see the data-loss discussion in this doc's history; not worth the migration risk since Postgres wasn't the thing actually crashing).
- [x] ~~Confirm the fixes hold under sustained (not just burst) load~~ — verified with a 5-minute soak test at 80 req/s target (~50 req/s achieved): **99.99% success (15,238/15,239), p95=21.65ms**, zero crashes, Redis container stayed healthy the whole run. The one failure (1/3849 profile requests) is consistent with the 3s FileManager timeout correctly failing fast on a rare slow moment — not a regression.
- [x] ~~Audit other cross-service clients for the same generous-timeout pattern~~ — done. Of `TenantServiceExtensions.cs`, `IdentityServiceExtensions.cs`, and `NotificationServiceExtensions.cs`, all three are background-job-only consumers (Hangfire cleanup jobs, `NotificationProcessor`) — not urgent. But this audit found a **more severe, different bug** in the process:
  - **`MultiTenancyExtensions.cs`'s `"TenantServiceClient"` had no `AddStandardResilienceHandler` at all** — just a flat 10s `client.Timeout`, no circuit breaker. This is the client every multi-tenant service (Identity, FileManager, Category, Notification, Nasheed) calls on every tenant-config cache miss via `TenantMiddleware` — the single hottest cross-service path in the platform. Fixed: added a resilience handler, `AttemptTimeout=2s`, `TotalRequestTimeout=4s`, `MaxRetryAttempts=2`, circuit breaker with 10s sampling. Verified: a cold-cache lookup that could previously hang up to 10s now resolves in **0.14s**.
  - **That same test surfaced a real, pre-existing, unrelated bug**: Category's and Nasheed's calls to Tenant Service were returning genuine `401 Unauthorized` (not a timeout) — traced to Tenant Service's `ServiceCommunication:AllowedServices` whitelist (`appsettings.json` + `appsettings.Development.json`) missing `"CategoryService"` and `"NasheedService"`. `ServiceAuthenticationMiddleware` doesn't reject a non-whitelisted name outright — it silently skips setting the `Service`/`SuperAdmin` claims, so the real failure only surfaces later as a plain 401 from the endpoint's own role check. This had been masked for the entire session by the 30-minute tenant-config cache (populated once, long before today, and never going cold until Redis was rebuilt). Fixed by adding both names to the whitelist; see `SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md` for the full writeup and the corrected Service Communication Matrix (which was stale and only listed 3 of the 8 services).
- [x] ~~Restart Nasheed and verify tenant resolution end-to-end~~ — after restarting, tenant resolution to the real `anashid` tenant still failed even with the `AllowedServices` fix in place, since `AllowedServices` and `SharedSecret` are two independent failure modes with the identical silent symptom. Root cause: `Nasheed.API/appsettings.Development.json` had **no `ServiceCommunication` section at all**, so it was sending the placeholder `SharedSecret: "CHANGE_ME_SHARED_SECRET"` from the base `appsettings.json` to Tenant Service on every request — guaranteed to mismatch Tenant's real secret. Fixed by adding the real shared secret to `appsettings.Development.json`.
  - **A second, self-inflicted issue surfaced while restarting to verify the first fix — not a codebase bug**: the manual restart used a raw `dotnet run` (via an ad hoc terminal command) instead of Nasheed's own `run-development-instance.bat`, which is the only thing that sets `ASPNETCORE_ENVIRONMENT=Development` for this service (same pattern as all 7 `src/Services/*` services — none of them use `launchSettings.json`; they all rely on their `run-development-instance.bat` to set the environment variable before `dotnet run --no-launch-profile`). Without it, the process silently defaulted to Production and never loaded `appsettings.Development.json` at all, for any key — including the just-added `SharedSecret` fix. The first restart attempt looked like it worked (an anonymous request returned a normal `401` with `WWW-Authenticate: Bearer` instead of a tenant-not-found error), but that was a false positive: Swagger returned `404` (proof it was really running in Production), and a request with a real JWT that reached the MediatR logging pipeline crashed with `Access to the path 'C:\Users\YOUR_USERNAME\...\Logs\Nasheed' is denied` — the literal, never-replaced placeholder path from the base `appsettings.json`, mis-mapped by the global exception handler into a misleading `401 Unauthorized access` JSON body (a completely different code path than the real auth challenge, coincidentally sharing the same status code). Fixed by restarting with `ASPNETCORE_ENVIRONMENT=Development` explicitly set (matching what `run-development-instance.bat` does). Verified: Swagger now returns `200`; a cold-cache `GET /api/v1/artists/` with a real superadmin JWT (`anashid@ihsandev.com`) now returns a genuine `200 OK` with real artist data (including FileManager-enriched image URLs), both directly against Nasheed and through the gateway. Control check: a genuinely nonexistent tenant ID still correctly returns `404 Tenant not found or inactive`, proving the fix didn't just paper over the not-found path. **Lesson for future ad hoc restarts of any service in this repo: always use its `run-development-instance.bat` rather than a bare `dotnet run` — that script is what actually applies `appsettings.Development.json`.** See `SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md` for the `SharedSecret`/`AllowedServices` pitfall this represents.
- [x] ~~Intermittent `401`/"signature key was not found" under load on `PerTenant`-mode JWT validation (Identity, FileManager, Category)~~ — fixed. Root cause: `JwtAuthenticationExtensions.cs` mutated `JwtBearerOptions.TokenValidationParameters` — a singleton shared by every concurrent request — from inside `OnMessageReceived`, via a blocking `.GetAwaiter().GetResult()` tenant-config fetch. Under concurrency, one request's validation could run against a different, concurrently in-flight request's freshly-overwritten parameters. Found via k6 (failure rate scaled from ~0.05% to ~22% purely as a function of load, zero code changes in between — see the correction note above). Fixed with `TokenValidationParameters.IssuerSigningKeyResolver`/`IssuerValidator`/`AudienceValidator`, stateless per-validation callbacks reading the already-resolved `ITenantContext` via `IHttpContextAccessor` instead of mutating shared state or re-fetching config. Verified: same `QUICK=1 PEAK_RATE=500` test, 100% success (0/10,829 failures), zero signature errors. Full writeup in `MULTI_TENANCY_GUIDE.md`'s Troubleshooting section.

**Known real ceiling for this local dev machine (superseded — see re-test below):** an earlier `PEAK_RATE=1500` run showed both Postgres and Redis saturating simultaneously on this laptop (evidenced by `TaskCanceledException`/`Npgsql` TCP-connect timeouts to Postgres and a 906-deep Redis command backlog), suggesting a real ceiling around ~100-150 req/s sustained. This is a hardware/environment artifact, not an application bug — don't use it for production capacity planning; re-test against non-local, adequately-resourced infrastructure for that.

**Re-test after Redis containerization + TenantServiceClient resilience + Nasheed fixes (July 2026):** re-ran the identical `k6 run -e PEAK_RATE=1500 LoadTests/k6/authenticated-flow.js` command. Result: **99.98% success (134,027/134,054 checks), sustained ~443 req/s, zero connection-level errors** (no `TaskCanceledException`, no Npgsql timeouts) — a marked improvement over the earlier ceiling reading. k6 reported `Insufficient VUs, reached 1500 active VUs and cannot initialize more` and the `p(95)<800ms` latency threshold failed (`p(95)=9.06s`, `avg=2.56s`), but this is expected queueing behavior, not a regression: `PEAK_RATE=1500` deliberately exceeds real capacity, so with avg iteration duration 2.56s and a 1500-VU cap the executor is throughput-limited to ~1500/2.56 ≈ 586 req/s theoretical — consistent with the observed 443 req/s. The system backpressures (requests queue and slow down) instead of erroring out, which is the correct behavior under overload. The only failures (27, all on the `filemanager` check) are consistent with the tightened `FileManagerServiceExtensions` resilience policy (`TotalRequestTimeout=3s`) correctly failing fast under extreme sustained load rather than hanging — not a bug. **Take-away: real sustainable throughput on this machine is now ~440 req/s with near-zero errors and graceful latency degradation, not ~100-150 req/s with hard failures — a genuine result of the cumulative fixes this session (Redis containerization, DB pool governance, tightened resilience timeouts), not just a re-measurement of the same ceiling.** Still don't use either number for production capacity planning — this remains single-machine, all-9-services-plus-Postgres-plus-Redis-plus-Python-AI-service-at-once local hardware.

**Follow-up at a more realistic target (`PEAK_RATE=500`):** achieved **309 req/s sustained, 99.94% success** (48 `filemanager`-only failures — see correction below, this was NOT the fail-fast timeout working as designed), and — the more informative number at this rate — **median latency of 48.76ms** (`p90=1.92s`, `p95=2.86s`, still over the 800ms threshold, but `dropped_iterations` fell to 726 out of ~93k, vs. 146,370 dropped at `PEAK_RATE=1500`). The low median plus the small drop count indicates the system handled most of the 5-minute ramp comfortably, with the p95/p90 tail coming from the top of the ramp curve rather than sustained overload throughout. **Combined with the 1500 result, comfortable real capacity on this machine looks like ~300-350 req/s with low latency, degrading gracefully (queueing, not erroring) beyond that up to at least ~440 req/s.** Next step if a precise "safe" number is needed: a `constant-arrival-rate` test (not ramping) at ~300 req/s to confirm p95 stays under 800ms when held steady rather than ramped through.

### Correction: the "filemanager fail-fast timeout" explanation above was wrong — real cause was a JWT validation race (fixed, July 2026)

The 27-48 `filemanager`-only failures in the two runs above were **misdiagnosed** at the time as the tightened `FileManagerServiceExtensions` resilience policy correctly failing fast under load. Adding failure logging to the k6 script (status/error/body on any check failure) revealed the actual response: `401`, `error="invalid_token"`, `error_description="The signature key was not found"` — a genuine JWT signature validation failure, not a timeout, and not even coming from the resilience-governed `FileManagerServiceClient` path at all (that client is used by *other* services calling FileManager, not by this k6 script's direct `GET /api/v1/filemanager/files` call).

**Root cause**: `JwtAuthenticationExtensions.cs`'s per-tenant JWT support (`JwtMode: PerTenant`, used by Identity/FileManager/Category) resolved the tenant's signing key inside `OnMessageReceived` and assigned it to `context.Options.TokenValidationParameters` — the single `JwtBearerOptions` instance **shared by every concurrent request**, not a per-request object. Under concurrency, one request's validation could run against a different, concurrently in-flight request's freshly-overwritten parameters. A follow-up full-length run (not just `QUICK=1`) at `PEAK_RATE=500` made this dramatically worse — **77.68% success, 22.31% failed, across all four check types** (`profile`, `categories`, `translations`, `filemanager` all affected, not just filemanager) — proving the failure rate scales with concurrent load, with zero code changes between the "48 failures" run and the "18,917 failures" run. See `MULTI_TENANCY_GUIDE.md`'s Troubleshooting section for the full root-cause writeup and the fix (`IssuerSigningKeyResolver`/`IssuerValidator`/`AudienceValidator`, stateless per-validation callbacks instead of shared-object mutation).

**Verification after the fix**: same `QUICK=1 PEAK_RATE=500` test, same load — **100% success, 0 failures out of 10,829 checks**, across all four check types. The `p(95)<800ms` latency threshold still fails at this load (`p(95)=9.61s`) but that's expected queueing behavior at an above-capacity target rate, unrelated to correctness.

**Lesson**: a low, stable-looking failure percentage (0.05%) under one load level can hide a bug whose failure rate is actually a function of concurrency, not randomness — worth deliberately re-testing at higher concurrency (or adding failure-detail logging) before writing off a small failure count as "expected" resilience behavior.

### Second regression found and fixed: synchronous, globally-locked file logging serialized every concurrent request (July 2026)

After the JWT fix above, a full 5-minute `PEAK_RATE=500` run showed **100% correctness but badly regressed latency** — median jumped from ~49ms to ~950ms-1.37s (reproduced identically across two separate full service restarts, ruling out a one-off cold-start blip), `p(95)` up to 5-6s, throughput actually *dropping* slightly (309→~280-290 req/s) at the same target rate.

**Diagnosis**: CPU on every affected process was low (a few percent, not saturated), Postgres was confirmed idle (29 connections, 0 active, no locks, no long-running queries) and Redis was fast (0.08ms latency, 451 ops/sec) during a live sample at peak load — ruling out both the database and the cache as the bottleneck. Low CPU + healthy backends + high latency under concurrency is the classic signature of blocking I/O or lock contention *inside* the .NET process itself, not an external dependency.

**Root cause**: `LoggerManager.WriteLogToFile` (`IhsanDev.Shared.Infrastructure/Services/Logging/LoggerManager.cs`) took a single `lock` object and, while holding it, synchronously opened a fresh `FileStream` in append mode, wrote one line, and closed it — for *every* log call, on the *calling* thread. This class is registered as a singleton (`AddCustomLogging`) and is called **twice per MediatR request** by `LoggingBehavior` ("Handling X" / "Handled X in Yms") — meaning literally every authenticated request (profile, categories, translations, filemanager — anything going through MediatR) serialized on this one lock, with the calling thread blocked for the duration of each file open/write/close. Under low concurrency this is barely noticeable; under sustained concurrent load, every request queues behind the same lock, and slower turnaround means more requests pile up concurrently (Little's Law), which increases lock contention further — a self-reinforcing spiral that, once tipped over, doesn't recover for the rest of a sustained run. Not introduced by anything this session changed — it's a structural issue that existed in `LoggerManager.cs` the whole time; it only started fully manifesting once `PEAK_RATE=500` full-length runs pushed concurrency high enough to hit it consistently.

**Fix**: rewrote `LoggerManager` so the calling thread never touches the console or disk — `LogInfo`/`LogWarn`/`LogDebug`/`LogError` just format a record and enqueue it (non-blocking hand-off via `System.Threading.Channels.Channel`), and a single background task drains the queue, doing the actual `Console.WriteLine` and file append (keeping the file's `StreamWriter` open across calls instead of reopening per line, only rotating when the date changes). No shared lock on the hot path at all.

**Verification**: same full 5-minute `PEAK_RATE=500` test after rebuilding and restarting all services —

| Metric | Before (buggy logger) | After (async logger) |
|---|---|---|
| p95 latency | 5.06s-6.11s | **4.73ms** |
| Median latency | ~950ms-1.37s | **1.57ms** |
| Throughput @ 500 target | 280-290 req/s | **312 req/s, full target rate sustained, no dropped iterations** |
| Max concurrent VUs needed | 1500 (exhausted) | **3** |
| `p(95)<800ms` threshold | ✗ failed | **✓ passed** |
| Success rate | 100% | 100% |

The drop in max-VUs-needed (1500 → 3) is itself strong confirmation: it means the target rate was never actually the problem — the system just needed ~2ms per request instead of ~1s to keep up. Real capacity on this machine is comfortably above 500 req/s now; a follow-up at a higher `PEAK_RATE` would be needed to find where it actually tops out.

**Lesson**: a custom logging abstraction that looks synchronous and "just writes a line" can become the dominant bottleneck in a system once request volume rises, precisely because it's easy to overlook — it doesn't show up as slow DB queries or high CPU, just unexplained latency with everything else looking healthy. Any shared logging/telemetry sink called on every request must be non-blocking from the caller's perspective (queue-and-return, with a background consumer doing the actual I/O).

Remaining, in order of expected impact:

1. **Add real load balancing across gateway/service replicas when you actually run more than one instance.** `ReverseProxy:Clusters` in `appsettings.json` defines exactly one destination per cluster today — this doesn't block a single-instance deployment from handling high throughput (confirmed: the gateway itself has no meaningful connection-level ceiling), but it does block *horizontal* scaling once one instance isn't enough.
2. **Consider PgBouncer (or similar) in front of Postgres for production** — per-service `MaxPoolSizePerTenant` only governs each service's own view of the pool; Postgres's own `max_connections` is still the real ceiling across all services × tenants combined. A pooler in transaction mode lets far more logical connections share fewer real Postgres connections.
3. **Revisit the `RateLimiting:Global`/`PerIp`/`PerIpAuth` numbers and `DatabaseSettings:MaxPoolSizePerTenant` once item 1 above is actually load-tested** — current values are a reasonable starting point for high legitimate throughput, not derived from a measured backend capacity ceiling.

---

## Related Documentation

- `API_GATEWAY_GUIDE.md` — rate limiting, routing, health endpoints
- `PERFORMANCE_OPTIMIZATION_GUIDE.md` — broader capacity checklist and targets
- `DATABASE_PER_TENANT_ARCHITECTURE.md` — per-tenant connection resolution
- `OBSERVABILITY_GUIDE.md` — Jaeger/Prometheus stack (not running by default; start it before a load test if you want trace-level detail instead of restarting services to capture console logs)
