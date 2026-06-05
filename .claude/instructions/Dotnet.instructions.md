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

### 5. Service Communication

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

## Documentation Protocol

If you encounter a discrepancy between these rules and the codebase:

1. **Stop.**
2. **Analyze** the `Doc/` folder.
3. **Fix** the documentation if it led you astray (Self-Correcting Documentation).
4. **Proceed** with the correct architectural pattern.
