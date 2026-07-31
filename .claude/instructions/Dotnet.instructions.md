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
  `UseTenantResolution` → `UseTenantAwareCors` →
  `UseTenantDatabaseMigration` (if multi-tenancy enabled) →
  `UseAuthentication` → `UseJwtTenantVerification` → `UseAuthorization`
  (`UseJwtTenantVerification` reads `context.User`, so it MUST come after `UseAuthentication()` — see pitfall #19 below; this file previously documented the opposite order, which is what caused the bug.)
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
14. **A MediatR query/command handler that needs to inject the service's `DbContext` directly cannot live in the `.Application` project — it must live in `.Infrastructure`.** `{Service}.Infrastructure` already has a `ProjectReference` to `{Service}.Application` (needed for the command/query/DTO types), so the reverse reference (`.Application` → `.Infrastructure`, to reach the `DbContext` in `Persistence/`) would be a circular project reference and fails to build. This surfaced building the Backup service's admin endpoints: the task brief said "inject `BackupDbContext` directly... in `Backup.Application/Handlers/`", but `BackupDbContext` lives in `Backup.Infrastructure.Persistence`. The fix — and the correct general pattern — mirrors the shared `GetAuditLogsQueryHandler<TDbContext>` (`IhsanDev.Shared.Infrastructure/Handlers/Audit/`): keep commands, queries, DTOs, and validators in `.Application` (no DbContext dependency), but put the handler classes themselves in `.Infrastructure/Handlers/`. `Program.cs`'s `AddMediatR(cfg => ...)` must then `RegisterServicesFromAssembly` on *both* the `.Application` assembly (for the request/notification types) and the `.Infrastructure` assembly (for the handler implementations) — see Backup's `Program.cs`.
15. **A Hangfire job class that needs to be enqueued from an `.Application`-layer command handler must be referenced by an interface defined in `.Application`, not by its concrete `.Infrastructure` type.** `Hangfire.IBackgroundJobClient.Enqueue<T>(...)` needs `T` at the call site; if a command handler calls `Enqueue<RunBackupJob>(...)` directly, `.Application` would need a compile-time reference to `.Infrastructure`, hitting the same circular-reference problem as pitfall #15. Instead, define the job's contract as an interface in `.Application/Interfaces/` (e.g. `IRunBackupJob`), implement it in `.Infrastructure/Jobs/` (`RunBackupJob : IRunBackupJob`), register the mapping (`services.AddTransient<IRunBackupJob, RunBackupJob>()`), and enqueue via `_backgroundJobClient.Enqueue<IRunBackupJob>(j => j.ExecuteAsync(...))`. Hangfire resolves the interface through the ASP.NET Core DI container (`AddHangfire`'s activator) at execution time, same as any other constructor-injected dependency.
16. **Never commit a real secret value into a tracked `appsettings.json` — only `CHANGE_ME_*` placeholders belong there.** Every service's tracked `appsettings.json` is meant to hold placeholders only (`CHANGE_ME_DB_PASSWORD`, `CHANGE_ME_JWT_SECRET`, `CHANGE_ME_SHARED_SECRET`, `CHANGE_ME_HANGFIRE_PASSWORD`, etc.); the real values belong exclusively in `appsettings.Development.json`, which `.gitignore` already excludes via the `*.Development.json` rule. AI.API and FileManager.API broke this pattern (real Postgres password, real Jwt:Secret, real ServiceCommunication:SharedSecret, real Hangfire password committed directly), and because `Jwt:Secret`/`ServiceCommunication:SharedSecret` are literal values shared identically across every service, leaking them from one service's tracked file exposes the trust boundary for the entire platform, not just that service. Confirmed and fixed July 2026 — required a `git filter-repo --replace-text` history rewrite plus a force-push to fully remove the values from the public GitHub repo, since simply fixing HEAD leaves them recoverable from old commits. **Before adding any new key under `Jwt`, `ServiceCommunication`, `DatabaseSettings`, or `Hangfire:Dashboard` in a tracked `appsettings.json`, use a `CHANGE_ME_*` placeholder and put the real value only in `appsettings.Development.json`.**
17. **A "toggle" or "restore" endpoint must look up its entity with a repository method that includes the very state it's about to flip — not one that implicitly filters it out.** Tenant Service's `PATCH /api/v1/admin/tenant/{tenantId}/toggle-archive` looked up the tenant via `ITenantRepository.GetByTenantIdAsync(tenantId)`, which (correctly, for every *other* caller) filters `!t.IsArchived` so archived tenants stay invisible to normal reads. But the toggle handler needs to find the tenant precisely when it *might* already be archived (to unarchive it), so that same filter made every unarchive attempt 404 with "Tenant not found" — the command handler underneath (`ToggleTenantArchivedStatusCommandHandler`) already correctly used the base repository's `GetByIdWithArchivedAsync(int id)`, but the API handler's *own* preliminary lookup (needed to resolve the string `tenantId` to an int before building the command) used the filtered method and never got that far. **Fix:** added `ITenantRepository.GetByTenantIdIncludingArchivedAsync(string tenantId)` (no `IsArchived` filter) and pointed the toggle handler at it instead. When adding any toggle/restore/undo endpoint over an entity with a soft-delete-style flag, audit every repository call in its path for a hidden `!IsSoftDeleted`-style filter, not just the final command handler.
    - **The same filtered lookup breaks any "view/edit an archived entity" admin UI too, not just the toggle button.** `GetTenantConfigQueryHandler` (backing the shared `GET /api/v1/tenant/config/{tenantId}` endpoint, restricted to `Service`/`SuperAdmin` roles) also called the filtered `GetByTenantIdAsync`. That endpoint is deliberately shared: every multi-tenant service's `TenantMiddleware` calls it on every tenant-config cache miss to resolve a *live* tenant (and correctly must keep 404'ing archived tenants — that's what makes "archive" actually disable a tenant's apps), but the Angular admin dashboard's tenant-edit dialog and tenant-configuration sheet *also* call it (via `TenantService.getTenantConfig()`) to preload the tenant's config for editing. Once a tenant is archived, both admin UIs silently got an empty config back (the dialog's `.subscribe({ error: () => ... })` swallowed the 404 as "non-critical, will use empty") — and the edit dialog then submits `data: this.existingConfig()` on save, which would **silently wipe the tenant's real config to `{}`** the moment an admin saved any edit on an archived tenant. **Never widen a shared service-resolution endpoint's filter to fix an admin-UI symptom** — instead add a parallel admin-only query/endpoint (here: `GetTenantConfigForAdminQuery` → `GET /api/v1/admin/tenant/{tenantId}/config`, `SuperAdmin`-only, uses `GetByTenantIdIncludingArchivedAsync`) and repoint only the admin-facing callers at it, leaving the original service-resolution path's filter untouched.
18. **A MediatR command/query with an enum-typed property bound directly from a JSON request body needs `[property: JsonConverter(typeof(JsonStringEnumConverter))]` on that property — there is no global string-enum converter registered anywhere on this platform.** Every service maps enums to strings manually on the *output* side (`Status = entity.Status.ToString()` in every DTO's `MapFrom`), so nothing ever exercised System.Text.Json's default enum *input* binding until `Backup`'s `TriggerBackupCommand(BackupScope Scope, ...)` took an enum straight from the POST body. Every frontend caller (and every one of this platform's own DTOs) sends/renders enum values as their string name (`"scope": "GlobalService"`), but System.Text.Json's default `EnumConverter` expects the underlying `int` unless told otherwise — the request fails to bind with a 400 and `JsonException: The JSON value could not be converted to ... Path: $.scope`, surfacing through `GlobalExceptionHandler` as an "Unexpected exception occurred" rather than a clean validation error. **Fix:** add the attribute directly on the record's positional parameter — `[property: JsonConverter(typeof(JsonStringEnumConverter))] BackupScope Scope` — which applies it to the generated property Minimal API model binding actually reads. When adding any new command/query whose JSON body includes an enum, add this attribute from the start rather than discovering the 400 at runtime.
19. **This file's own documented pipeline order was the bug.** The "Pipeline order (Strategies B/C)" note above (and `.claude/instructions/database-strategy.instructions.md`) used to place `UseJwtTenantVerification` *before* `UseAuthentication()`. That's backwards — `JwtTenantVerificationMiddleware` reads `context.User.FindFirst("tenant_id")`, which is only populated once `UseAuthentication()` has run. Following this doc's old instructions produced a middleware that opens with an unauthenticated-principal early-return on every single request, so the tenant/JWT cross-check it exists to perform never ran — a full cross-tenant break (any authenticated user of Tenant A could reach Tenant B's data by swapping the `x-tenant-id` header) present in Identity, Category, FileManager, Notification, and Nasheed until a July 2026 security audit caught it by reading the middleware and every `Program.cs` directly, rather than trusting this file. **This is exactly the self-correcting-docs failure mode described below** — the doc caused the mistake, not a one-off coding error. Both docs have been corrected to `UseAuthentication → UseJwtTenantVerification → UseAuthorization`; if you ever see the old order again (in a doc or in a new service's `Program.cs` copied from an old one), treat it as the bug and fix the order, not as a valid alternative to follow.
20. **A new service's `appsettings.Development.json` can be missing a `Jwt:Secret` override entirely (not just `ServiceCommunication:SharedSecret`, pitfall #10) and fail completely silently — because JWT *validation* has no equivalent of pitfall #9/#10's whitelist check to fail loudly on.** Found on Nasheed.API: its `appsettings.Development.json` had a `ServiceCommunication` section but no `Jwt` section at all, so it fell back to the tracked `appsettings.json` placeholder (`"Secret": "CHANGE_ME_JWT_SECRET"`) for validating every incoming JWT — meaning tokens issued by Identity (signed with the real, different dev secret) would fail signature validation against Nasheed specifically, with no startup error and no obvious log line pointing at the cause. This is also exactly why pitfall #16's `ValidateSecretStrength` fail-fast check (added to `JwtAuthenticationExtensions`/`ServiceAuthenticationMiddleware`) matters: it turns this class of gap into an immediate, loud startup failure instead of a silent per-request auth mismatch. **When scaffolding or auditing any service, verify `appsettings.Development.json` has both a real `Jwt:Secret` AND a real `ServiceCommunication:SharedSecret` — checking one is not enough.**
21. **`dotnet ef migrations add` cannot run against a service that is currently running as a live process — it needs to build and load the startup project's assembly, and the running process holds that DLL locked.** `dotnet build` alone degrades gracefully in this situation (source compiles fine, only the final copy-to-output-directory step fails with MSB3021/MSB3027 — safe to ignore per this file's own build-verification guidance), but `dotnet ef migrations add --startup-project {Service}.API` needs the actually-loadable output assembly to reflect the model, so it fails outright with a bare "Build failed. Use dotnet build to see the errors" and no further detail. Hit this adding `FailedLoginAttempts`/`LoginLockoutUntil` to Identity's `User` entity (July 2026 security-audit fixes) while Identity.API was intentionally kept running for the user's own testing. **Do not stop a service the user is actively using just to generate a migration.** Instead hand-write the three files EF would have generated: (1) `{Timestamp}_{Name}.cs` with `Up()`/`Down()` using `migrationBuilder.AddColumn<T>(...)`/`DropColumn(...)`, (2) `{Timestamp}_{Name}.Designer.cs` — a full `BuildTargetModel(ModelBuilder)` snapshot of the entity graph *after* the change (copy the current `{Context}ModelSnapshot.cs`'s `BuildModel` body verbatim, only renaming the method and wrapping class/attributes to match the `InitialCreate` migration's Designer pattern), and (3) update `{Context}ModelSnapshot.cs` itself with the same new `b.Property<T>(...)` lines, in alphabetical order among the entity's other properties (EF's own convention). Verify by building `.Infrastructure` directly (not the locked `.API`) — a clean compile there means the hand-written migration/snapshot are structurally consistent with the DbContext model.
22. **`.DisableRateLimiting()` on a gateway/proxy endpoint is not a neutral "opt out of the extra per-IP policy" — it also bypasses the always-on `GlobalLimiter`, which applies to every request by default with no opt-in.** The Gateway's `/health/aggregate` (`src/Gateway/Gateway.API/Program.cs`) called `.DisableRateLimiting()` and fanned out to a 9-way `Task.WhenAll` downstream call per hit — meaning an unauthenticated flood didn't just go unlimited, it multiplied 9x against every downstream service with literally no ceiling. Found in the July 2026 security audit. **Fix pattern:** any endpoint that both (a) has no auth and (b) does non-trivial work per request (fan-out, multiple downstream calls, expensive aggregation) needs its own named, generous-but-nonzero rate-limit policy (`options.AddPolicy("name", ...)` + `.RequireRateLimiting("name")`) — reserve bare `.DisableRateLimiting()` for endpoints that are both unauthenticated *and* cheap (a single in-process check, no fan-out), like the gateway's plain `/health`.
23. **A catch-all route pattern (`{**catch-all}`) at the bottom of a routing table fails open, not closed, for any path nobody else claimed.** The Gateway's `identity-admin-route` (`/api/v1/admin/{**catch-all}`, Order 20) was meant as a fallback for Identity's own admin sub-paths, but because Notification, FileManager, Translation, and Nasheed each only expose a single hardcoded audit-log route under `/admin` (no general admin route of their own), any *other* unmatched `/api/v1/admin/*` path silently forwarded to Identity instead of 404ing at the gateway edge. Found in the July 2026 security audit; fixed by scoping the route to Identity's actual known prefixes (`/api/v1/admin/users/{**catch-all}`, `/roles/{**catch-all}`, `/claims/{**catch-all}` — read off `MapAdminEndpoints`/`MapRoleEndpoints`/`MapClaimEndpoints` in `Identity.API/Extensions/EndpointMappingExtensions.cs`) instead of one bare catch-all. **When adding or auditing any gateway-style catch-all route, verify every other cluster it could shadow actually has nothing real under that catch-all's path space — a catch-all should be scoped to what its own service actually owns, not left as "whatever nobody else claimed."**
24. **A rejected request with no CORS headers is misreported by the browser as a CORS error, hiding the real status code — always `curl -i` the actual request before touching CORS config.** Every service's pipeline runs `UseTenantResolution`/`TenantMiddleware` *before* `UseTenantAwareCors` (required — see the pipeline-order note above), so a request rejected by tenant resolution (missing `x-tenant-id`, unknown tenant, etc.) is written before CORS headers are ever added. The browser then shows `No 'Access-Control-Allow-Origin' header is present`, which looks like a Gateway/origin-allowlist problem but is actually a plain 400 from a different middleware entirely. Hit this with the shared `MapAuditLogEndpoints()` extension (`IhsanDev.Shared.Infrastructure/Extensions/AuditLogEndpointExtensions.cs`) — every service's `/api/admin/audit-logs` route required `x-tenant-id` even though the Angular `AuditLogService` intentionally never sends it (its own code comment assumed the route was already `[BypassTenant]`). `curl`-ing the OPTIONS preflight looked fine (preflight is unconditionally skipped by `TenantMiddleware`), which is what made this look like a CORS issue until the actual GET was curled directly and returned a 400 with no CORS headers at all. **Fixed** by adding `[BypassTenant]` to the group and restricting it to `RequireRole("SuperAdmin")` only (never `"Admin"` — see `Doc/BYPASS_TENANT_ENDPOINTS_GUIDE.md` Pitfall 5). **Whenever a browser reports a CORS error, verify with `curl -i <url> -H "Origin: <origin>"` before changing any CORS config** — a non-2xx response with no `Access-Control-Allow-Origin` header is this pitfall, not a misconfigured allowlist.

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
