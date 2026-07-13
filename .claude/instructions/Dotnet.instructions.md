# .NET Backend Workflow & Rules

## Agent Mindset

You must act as a **Senior .NET Backend Engineer** specializing in:

1. **Microservices Architecture (Clean, DDD, CQRS)**
2. **Minimal APIs (.NET 8)**
3. **Strict Multi-Tenancy (Database-per-tenant)**
4. **Documentation-First Development**

## MANDATORY PRE-CHECKLIST

Before modifying ANY backend code, you MUST:

1. **Read Documentation:** Start with `MicroservicesArchitecture/Doc/DOCUMENTATION_INDEX.md`. Find the RELEVANT guide.
2. **New Services:** Read `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md` first.
3. **Database Strategy:** Read `.claude/instructions/database-strategy.instructions.md` — choose A/B/C/D before writing any DbContext or Program.cs.
4. **Authentication:** Read `Doc/SHARED_IDENTITY_SERVICE_GUIDE.md`.
5. **Admin Endpoints:** If creating global/admin APIs, read `Doc/BYPASS_TENANT_ENDPOINTS_GUIDE.md` CRITICALLY.

## Architectural Rules

### 1. Minimal APIs Only

- **Structure:** `Services/{ServiceName}.API/Endpoints/`.
- **Handlers:** Use `IRequestHandler` with MediatR.
- **Controllers:** **PROHIBITED.** Do not use `[ApiController]`.
- **Endpoints:** `app.MapPost("/api/...")`. Use `IMediator` injection.

### 2. CQRS Pattern (MediatR)

- **Commands:** `public record MyCommand : IRequest<Result>;`
- **Queries:** `public record MyQuery : IRequest<Result>;`
- **Handlers:** Place in `{ServiceName}.Application/Handlers/`.
- **Validation:** FluentValidation (`AbstractValidator<MyCommand>`).

### 3. Data Mapping (Strict Manual)

- **Library:** **NO AUTOMAPPER.**
- **Pattern:** Use static `MapFrom` methods on DTOs.
  ```csharp
  public static UserDto MapFrom(User user) => new() { ... };
  ```
- **DateTime:** Standardize on UTC string: `entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)`.

### 4. Multi-Tenancy Strictness

- **Strategy:** Choose A/B/C/D from `.claude/instructions/database-strategy.instructions.md` BEFORE writing DbContext.
  - A: Single Global DB (Tenant Service pattern)
  - B: Per-Tenant DB — `ITenantContext` in DbContext + full middleware chain (Identity, FileManager pattern)
  - C: Dual DB — global queue context + per-tenant history context (Notification pattern)
  - D: Global DB + `TenantId` discriminator column (Translation pattern)
- **Context:** Tenant is resolved via `ITenantContext` (middleware), injected into DbContext `OnConfiguring()`.
- **Database:** Two-layer migration pattern — both layers are required:
  1. **`await app.Services.InitializeDatabaseAsync<TContext>(applyMigrations: true)` before `app.Run()`** — migrates the global DB at startup before hosted services start. Never guard with `IsDevelopment()` or `!MultiTenancy:Enabled`. Has built-in retry with jitter for concurrent-startup locking scenarios.
  2. **`UseTenantDatabaseMigration`** — migrates per-tenant DBs lazily on each tenant's first request. Only viable approach since tenants are provisioned dynamically.
  - `UseDefaultDatabaseMigration` remains as a safety-net fallback in the middleware pipeline.
- **Pipeline order (Strategies B/C) — ORDER IS CRITICAL:**
  `InitializeDatabaseAsync` (before app.Run) →
  `UseDefaultDatabaseMigration` →
  `UseTenantResolution` → `UseTenantAwareCors` → `UseJwtTenantVerification` →
  `UseTenantDatabaseMigration` (if multi-tenancy enabled) →
  `UseAuthentication` → `UseAuthorization`
- **Why `UseDefaultDatabaseMigration` must precede `UseTenantResolution`:** When multi-tenancy is on, `AddDatabaseContext` leaves `IsConfigured=false` so `OnConfiguring` uses `ITenantContext` at resolution time. Running after tenant resolution causes the static `_isMigrated` flag to fire against the tenant DB, permanently skipping the global fallback DB.
- **BypassTenant:**
  - Use `[BypassTenant]` attribute sparingly.
  - MUST ensure `UseDefaultDatabaseMigration` is registered so the fallback global DB is available.
  - MUST handle fallback to global connection string if tenant context is missing.

### 5. Audit Logging — Automatic, No Handler Code Required

Every service registers `AddAuditService()` in `Program.cs`. Once registered, `BaseDbContext.SaveChangesAsync` automatically intercepts the EF `ChangeTracker` before each save and writes a row per entity change to the `audit_log` table inside the same transaction:

- `EntityType.Created` — for `Added` entities (captures `after` snapshot)
- `EntityType.Updated` — for `Modified` entities (captures `before` and `after`)
- `EntityType.Deleted` — for soft-deleted entities (`IsArchived` changed `false → true`)
- `EntityType.HardDeleted` — for EF `Deleted` state (captures `before` snapshot)

**Do NOT inject `IAuditService` into handlers** and call `.Record()` manually — the auto-capture in `BaseDbContext` handles everything. Manual calls would produce duplicate audit rows.

Each audit row includes: `UserId`, `UserEmail`, `TenantId`, `IpAddress` (resolved automatically via `DbAuditService`).

After adding `AddAuditService()`, also register the query handler and endpoint, then run the migration:

```csharp
// Program.cs DI
builder.Services.AddAuditService();
builder.Services.AddAuditLogQueries<YourServiceDbContext>();

// Program.cs endpoint mapping
app.MapAuditLogEndpoints();
```

```powershell
dotnet ef migrations add AddAuditLog --project {Service}.Infrastructure --startup-project {Service}.API
```

`MapAuditLogEndpoints()` exposes `GET /api/admin/audit-logs` (Admin/SuperAdmin only) with query params: `tenantId`, `entityType`, `action`, `userId`, `userEmail`, `fromDate`, `toDate`, `sortBy`, `sortDesc`, `page`, `pageSize`. Returns `PaginatedList<AuditLogDto>`.

### 6. Service Communication

- **Protocol:** HTTP with `X-Service-Secret` header using `INotificationServiceClient`.
- **Injection:** Inject `INotificationServiceClient` (infrastructure layer).
- **Authentication:** Service-to-service calls bypass JWT.

## CRITICAL: No Hardcoded Text — EVER

Every user-facing string (exception messages, validation errors, response messages, notification text) **MUST** use `LocalizationKeys` and `ILocalizationService`. Hardcoded text is **PROHIBITED**.

❌ **FORBIDDEN:**

```csharp
throw new NotFoundException("User not found");                     // hardcoded
throw new BadRequestException("File is empty or null.");           // hardcoded
.WithMessage("Email is required");                                  // hardcoded
```

✅ **REQUIRED:**

```csharp
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);
throw new BadRequestException(LocalizationKeys.Exceptions.FileEmpty);
.WithMessage(L(LocalizationKeys.Validation.Required, "Email"));
```

**Rules:**

1. Always throw `AppException` subclasses (`NotFoundException`, `BadRequestException`, `UnauthorizedException`, `ForbiddenException`, `ConflictException`, `GeneralException`) — never plain `Exception` or `InvalidOperationException` for user-facing errors.
2. Always pass a `LocalizationKeys.*` constant as the message — never a raw string.
3. When adding a new key: add it to `LocalizationKeys.cs`, `en.json`, AND `ar.json`.
4. Domain exceptions (e.g., custom `FileValidationException`) must inherit from `AppException` or they will NOT be handled by `GlobalExceptionHandlingMiddleware` and will return HTTP 500.
5. Read `Doc/LOCALIZATION_GUIDE.md` for the full guide and key naming conventions.

---

## Common Pitfalls to Avoid

1. **Controllers:** Attempting to create `ExampleController.cs`. Instant failure.
2. **Chaining Commands:** Running `dotnet build & dotnet run` (PowerShell error). Use `;` or separate lines.
3. **Date Formats:** Returning raw DateTime objects instead of formatted strings.
4. **Tenant Leak:** Accessing global data without checking `ITenantContext`.
5. **Assuming AutoMapper:** Trying `_mapper.Map<UserDto>(user)`. It doesn't exist.
6. **Hardcoded Text:** Passing raw strings to exceptions or validators. Always use `LocalizationKeys`.
7. **Editing an already-applied migration file:** EF Core only checks whether a migration ID is recorded in `__EFMigrationsHistory` — it never re-diffs the actual schema. If you edit a migration file after it has run against any real database (dev tenant included), that database silently keeps the old schema forever while EF reports it as "up to date." This caused Category's `ihsandev` tenant DB to be missing `icon_file_id`/`image_file_id`/`icon_name`/`uri` while `InitialCreate` showed as applied. **Always add a new migration for schema changes — never edit a migration file that has already run anywhere.**
8. **Generous resilience timeouts on inline cross-service calls:** A service-to-service `HttpClient` call made *inline* inside a hot request path (e.g. `ProfilePictureHelper` calling `FileManagerServiceClient` on every `/api/v1/user/profile` request) must fail fast, not just eventually recover. `FileManagerServiceExtensions.cs` allowed up to 15s total (3 retries × up to 4s attempts) despite its own comment saying "keep retries tight" — under concurrent load, many simultaneous callers all blocked for up to 15-18s each is what turned a FileManager slowdown into the *caller* service (Identity) becoming unresponsive to everything, not just the endpoint that depends on FileManager. If the call already degrades gracefully on failure (try/catch around it), the resilience policy should be tuned for "fail in ~1-3s" not "eventually succeed within 15s." Check `AttemptTimeout`/`TotalRequestTimeout` on any `AddStandardResilienceHandler()` call whose comment claims the dependency is "fast" or "internal."
9. **Forgetting to whitelist a new multi-tenant service in Tenant Service's `AllowedServices`:** Every service that calls `AddMultiTenancy()` automatically calls Tenant Service's `/config/{tenantId}` on every tenant-config cache miss via the shared `TenantServiceClient` (`MultiTenancyExtensions.cs`). If the new service's `ServiceCommunication:ServiceName` isn't added to Tenant Service's `ServiceCommunication:AllowedServices` (in both `appsettings.json` and `appsettings.Development.json`), `ServiceAuthenticationMiddleware` silently skips granting the `Service`/`SuperAdmin` claims — no error at startup, no error in the request itself, just a plain 401 from the endpoint's own role check. This is exactly the kind of bug the 30-minute tenant-config cache can hide for hours: it only surfaces on a cold cache (first request for a tenant, cache flush, Redis restart). Confirmed missing for both `CategoryService` and `NasheedService` (July 2026) — see `SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md`. **When scaffolding a new multi-tenant service, immediately add it to Tenant Service's `AllowedServices` — don't wait for a cache-miss bug report.**
10. **A missing `ServiceCommunication:SharedSecret` override in a new service's `appsettings.Development.json` fails identically to pitfall #9:** `ServiceAuthenticationMiddleware` treats "wrong secret" and "not whitelisted" the same way — silent skip, no startup error. If a new app under `src/Apps/` only inherits the placeholder `SharedSecret: "CHANGE_ME_..."` from its base `appsettings.json` with no real value in `appsettings.Development.json`, every `TenantServiceClient` call fails auth exactly like an `AllowedServices` gap, surfacing as the same 401/"tenant not found" symptom. Confirmed for `NasheedService` (July 2026) — its `appsettings.Development.json` had no `ServiceCommunication` section at all. **When scaffolding a new service, check both halves: the whitelist entry in Tenant Service AND the real secret override in the new service's own `appsettings.Development.json`.**
11. **Never manually restart a service with a bare `dotnet run` — always use its `run-development-instance.bat`:** every service (`src/Services/*` and `src/Apps/*` alike) relies on that script to `set ASPNETCORE_ENVIRONMENT=Development` before `dotnet run --no-launch-profile` — none of them ship a `launchSettings.json`, so a raw `dotnet run` from a different terminal/tool silently defaults to Production and skips `appsettings.Development.json` entirely, for every key, with no startup error. This produced a confusing false-positive during Nasheed's restart (July 2026): an unauthenticated request returned a normal-looking `401` (actually the framework's standard "no JWT" challenge, reached because tenant resolution happened to degrade gracefully) while a request that reached deeper business logic crashed on the base config's placeholder log path (`C:\Users\YOUR_USERNAME\...`), mis-mapped by the exception handler into a misleading `401 Unauthorized access` body. Swagger returning `404` instead of `200` is the fastest way to notice a service is stuck in Production when it shouldn't be.
12. **Never mutate `JwtBearerOptions`/`TokenValidationParameters` from inside a `JwtBearerEvents` handler (`OnMessageReceived`, etc.) to implement per-request/per-tenant logic — that object is a shared singleton, not per-request.** `JwtAuthenticationExtensions.cs`'s old `CreatePerTenantJwtEvents` did exactly this: it resolved the tenant-specific signing key inside `OnMessageReceived` (via a blocking `.GetAwaiter().GetResult()` call, itself a separate anti-pattern) and assigned it to `context.Options.TokenValidationParameters` — the single `JwtBearerOptions` instance shared by every concurrent request. Under load, one request's validation could run against a different, concurrently in-flight request's freshly-overwritten parameters, intermittently rejecting valid tokens with `"signature key was not found"`. Found via k6 load testing (July 2026): failure rate scaled from ~0.05% to ~22% purely as a function of concurrent load with zero code changes in between — see `Doc/MULTI_TENANCY_GUIDE.md`'s Troubleshooting section and `Doc/LOAD_TESTING_GUIDE.md`. **Fixed by using `TokenValidationParameters.IssuerSigningKeyResolver`/`IssuerValidator`/`AudienceValidator` instead** — these are pure, stateless, per-validation callbacks with no shared mutable state, registered once via `services.AddOptions<JwtBearerOptions>(scheme).Configure<IHttpContextAccessor>(...)`. They read the already-resolved, request-scoped `ITenantContext` (populated earlier in the same request by `UseTenantResolution`) instead of re-fetching tenant config, so there's no extra I/O and no blocking call either. **Any future per-tenant/dynamic JWT customization must use these resolver/validator delegates, never direct `Options` mutation in an event handler.**
13. **Never do blocking, lock-serialized I/O (file writes, console writes) inside a shared logging/telemetry component called on every request.** `LoggerManager.cs` (`IhsanDev.Shared.Infrastructure/Services/Logging/`) used to take a single `lock` and, while holding it, synchronously open a `FileStream` in append mode, write one line, and close it — for *every* call, on the calling thread. It's a singleton, and `LoggingBehavior` (the MediatR pipeline logger) calls it twice per request — so every authenticated request in every service serialized on that one lock. Under low concurrency this is invisible; under sustained load it becomes a self-reinforcing bottleneck (slower turnaround → more requests pile up concurrently → more lock contention → slower still) that shows up as **high latency with deceptively low CPU and healthy DB/Redis** — easy to misdiagnose as a database or cache problem when it's actually blocking I/O inside the process itself. Found via k6 load testing (July 2026): full 5-minute `PEAK_RATE=500` runs showed p95 latency of 5-6s despite 100% correctness and idle Postgres/Redis. **Fixed by making the logger non-blocking**: calling code just formats a message and enqueues it (`System.Threading.Channels.Channel`, no lock, no I/O), and a single background task drains the queue and does the actual console/file I/O, keeping the file's `StreamWriter` open across calls instead of reopening per line. Verified: p95 dropped from 5-6s to 4.73ms at the same load. **Any shared component invoked on every request (logging, metrics, tracing) must be non-blocking from the caller's perspective — queue-and-return, with I/O done off the request thread.** A follow-up review caught two more issues in the same class, both worth their own note:
    - **A single bounded channel shared across all severities drops indiscriminately.** The first async version of `LoggerManager` put Information/Debug and Warning/Error/Critical through the same bounded/dropping channel — meaning a sustained incident (DB outage, failing downstream dependency) that produces an Error per request at full request rate could drop error entries just as easily as routine debug noise, at precisely the moment errors matter most. Fixed by splitting into two channels: a small one for Information/Debug (drops newest when full) and a much larger one for Warning/Error/Critical (evicts oldest when full instead of dropping newest, and is bounded at all specifically so a real incident can't turn the logger's own memory into part of the outage).
    - **`Channel<T>.Writer.TryWrite` returns `true` unconditionally under `BoundedChannelFullMode.DropWrite`/`DropOldest`/`DropNewest`, even when it silently discards the item.** Only `BoundedChannelFullMode.Wait` makes `TryWrite` reliably return `false` when full. Code that uses the built-in Drop* modes and checks `if (!channel.Writer.TryWrite(item))` to count drops is dead code — it will never fire. `LoggerManager`'s drop counters were briefly, silently broken this way. **If you need to know when a bounded channel drops something, use `FullMode = Wait` and implement the drop/evict policy manually** (e.g. `if (!writer.TryWrite(item)) { /* drop, or reader.TryRead(out _) then retry to evict-oldest */ }`) — don't trust a Drop* mode's `TryWrite` return value for anything.
14. **Never commit a real secret value into a tracked `appsettings.json` — only `CHANGE_ME_*` placeholders belong there.** Every service's tracked `appsettings.json` is meant to hold placeholders only (`CHANGE_ME_DB_PASSWORD`, `CHANGE_ME_JWT_SECRET`, `CHANGE_ME_SHARED_SECRET`, `CHANGE_ME_HANGFIRE_PASSWORD`, etc.); the real values belong exclusively in `appsettings.Development.json`, which `.gitignore` already excludes via the `*.Development.json` rule. AI.API and FileManager.API broke this pattern (real Postgres password, real Jwt:Secret, real ServiceCommunication:SharedSecret, real Hangfire password committed directly), and because `Jwt:Secret`/`ServiceCommunication:SharedSecret` are literal values shared identically across every service, leaking them from one service's tracked file exposes the trust boundary for the entire platform, not just that service. Confirmed and fixed July 2026 — required a `git filter-repo --replace-text` history rewrite plus a force-push to fully remove the values from the public GitHub repo, since simply fixing HEAD leaves them recoverable from old commits. **Before adding any new key under `Jwt`, `ServiceCommunication`, `DatabaseSettings`, or `Hangfire:Dashboard` in a tracked `appsettings.json`, use a `CHANGE_ME_*` placeholder and put the real value only in `appsettings.Development.json`.**

## Documentation Protocol

### Before starting

Read `Doc/DOCUMENTATION_INDEX.md` and every doc file relevant to the task. State which files you read.

### After every change — BLOCKING REQUIREMENT

A task is **not complete** until:

1. Every `Doc/*.md` that describes changed behavior has been updated in place
2. `Doc/DOCUMENTATION_INDEX.md` reflects any added, removed, or renamed doc files
3. If a new pattern or pitfall was discovered: it is added to this file or to `MicroservicesArchitecture/CLAUDE.md`
4. No stale information remains in any doc you touched during the task

### Self-correcting docs

If you make a mistake caused by incorrect or misleading documentation:

1. **Stop.** Acknowledge the mistake.
2. **Fix** the offending doc immediately with correct information.
3. **Add** a warning or clarification to prevent repeating it.
4. **Proceed** with the correct architectural pattern.
