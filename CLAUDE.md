# .NET Backend — Claude Code Instructions

## Essential Reading Before Any Task

- Read `.claude/instructions/Dotnet.instructions.md` for backend rules
- Start with `Doc/DOCUMENTATION_INDEX.md` as the single entry point to all documentation
- For database strategy (A/B/C/D), read `.claude/instructions/database-strategy.instructions.md`

## Project Overview

.NET 10 microservices with Clean Architecture, DDD, CQRS, optional multi-tenancy, and database-per-tenant support.

## Workflow for Every Task

1. **Read** relevant `Doc/*.md` files first (start with `Doc/DOCUMENTATION_INDEX.md`)
2. **Implement** the requested changes
3. **Update** every affected `Doc/*.md` file in place — this is BLOCKING, not optional
4. **Update** `Doc/DOCUMENTATION_INDEX.md` if you added, removed, or renamed any doc file
5. **Update** this `CLAUDE.md` and `.claude/instructions/` files if you discovered a new pattern, pitfall, or structural change
6. **For Python service work**, use the project-local `venv\Scripts\python.exe` instead of system Python whenever a `venv` folder exists

**Do not report a task as complete until steps 3–5 are done.**

## CRITICAL: No Hardcoded Text — EVER

All user-facing strings in backend code **MUST** use `LocalizationKeys` and `ILocalizationService`. **Never** pass a raw string to an exception, validator, or response message.

- Exceptions: `throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);`
- Validators: `.WithMessage(L(LocalizationKeys.Validation.Required, "Email"))`
- New keys: add to `LocalizationKeys.cs` + `en.json` + `ar.json`
- Domain exceptions must inherit from `AppException` or the global middleware returns HTTP 500
- See `Doc/LOCALIZATION_GUIDE.md` for full rules and key naming conventions

## Architecture Quick Reference

### Multi-Tenancy Modes

**Enabled=true** (x-tenant-id header required):
- Each tenant = separate database (complete isolation)
- Auto-creates tenant DB on first request; fetches config from Tenant Service
- JwtMode: "Shared" (superadmin access all) or "PerTenant" (isolated)

**Enabled=false** (traditional mode):
- Single database from appsettings.json; no tenant header needed

**Optional Tenant** (Identity, FileManager, Notification):
- Works with OR without x-tenant-id header
- Apply `OptionalTenantAttribute` at group level, not per endpoint
- Requires dual database migration (global + tenant)

### src/ Folder Organization

```
src/
├── Services/   # Core platform microservices (foundational — other projects depend on these)
│               # Identity, Tenant, FileManager, Notification, Translation, Category, AI
└── Apps/       # Domain-specific application projects that consume platform Services
                # e.g. src/Apps/Nasheed/
```

**New foundational services** → `src/Services/`
**New domain apps** → `src/Apps/` — must have their own `Doc/` folder

### Service Structure

```
{ServiceName}/
├── {ServiceName}.API/          # Minimal APIs, Program.cs, endpoint handlers
├── {ServiceName}.Application/  # CQRS handlers, DTOs, validators
├── {ServiceName}.Domain/       # Entities, repository interfaces
└── {ServiceName}.Infrastructure/ # EF DbContext, repository implementations
```

### Shared Libraries (src/Shared/)

| Library | Purpose |
|---|---|
| `IhsanDev.Shared.Kernel` | `BaseEntity`, `BaseUser`, `ITenantContext`, `ITenantConfigurationProvider` |
| `IhsanDev.Shared.Application` | CQRS interfaces, FluentValidation behaviors, `AppException`, manual mapping |
| `IhsanDev.Shared.Infrastructure` | Middlewares, `INotificationServiceClient`, DateTime UTC handling |
| `IhsanDev.Shared.Authentication` | JWT generation/validation, service-to-service auth middleware |

## Essential Patterns

### DateTime Standardization

All DateTime properties in DTOs are strings formatted as UTC:

```csharp
Created = entity.Created.ToUniversalTime()
    .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
```

### Manual Mapping — No AutoMapper

```csharp
public static UserDto MapFrom(User user) => new() { Id = user.Id, Email = user.Email };
```

### Database Strategy Decision Tree

```
Stores tenant-specific data?
├─ NO + system registry?            → Strategy A (Single Global DB)       e.g. TenantService
├─ NO + shared with tenant rows?    → Strategy D (Global + TenantId col)  e.g. TranslationService
├─ YES + pure tenant data?          → Strategy B (Per-Tenant DB)          e.g. Identity, FileManager
└─ YES + global queue + tenant log? → Strategy C (Dual DB)                e.g. NotificationService
```

Full patterns (DbContext code, Program.cs pipeline, appsettings) → `.claude/instructions/database-strategy.instructions.md`

### Admin Endpoints with BypassTenant (CRITICAL — Nov 2025)

- **Always** match `MultiTenancy:JwtMode` across ALL services — mismatch causes 401 for tenant users
- DbContext **must** fall back to global database when no tenant context
- **Always** run both `UseDefaultDatabaseMigration` (global) and `UseTenantDatabaseMigration` (tenant) when you have `BypassTenant` endpoints

## Key Files

| File | Purpose |
|---|---|
| `Doc/DOCUMENTATION_INDEX.md` | **START HERE** |
| `.claude/instructions/database-strategy.instructions.md` | Database strategy A/B/C/D patterns |
| `Doc/BYPASS_TENANT_ENDPOINTS_GUIDE.md` | Admin/global endpoints **(CRITICAL)** |
| `Doc/SHARED_IDENTITY_SERVICE_GUIDE.md` | JWT auth pattern |
| `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md` | Creating new services |
| `Doc/DATABASE_PER_TENANT_ARCHITECTURE.md` | Multi-tenancy DB architecture |
| `Doc/MULTI_TENANCY_GUIDE.md` | Multi-tenancy setup |
| `Doc/PERFORMANCE_OPTIMIZATION_GUIDE.md` | Performance optimizations |
| `Directory.Packages.props` | Centralized NuGet package versions |

## Common Pitfalls

| Pitfall | Fix |
|---|---|
| Chaining with `&` | Use `;` or separate lines in PowerShell |
| Controllers | Minimal APIs only — `app.MapPost("/api/...")` |
| Tenant Service as multi-tenant | It uses static config — it's the provider, not the consumer |
| Manual `dotnet ef database update` in prod | System auto-creates DBs on first request |
| Hardcoded text | Always use `LocalizationKeys` |
| AutoMapper | Static `MapFrom()` methods only |
| JWT validation using `ITenantContext` in `OnMessageReceived` | Use `ITenantConfigurationProvider` directly |

## Technology Stack

- **.NET 10**, **C# 14**, **EF Core 10.0**
- **MediatR 12.4** (CQRS), **FluentValidation 12.0**
- **PostgreSQL** (primary), SQL Server, MySQL, SQLite supported
- **Redis 2.7** (distributed cache), **SignalR 8.0** (real-time)

## Auto-Maintenance Rules

After completing ANY task, self-check and update ALL affected files. These are **required** steps — a task is not done until they are complete.

| Change Made | File(s) to Update | Section |
|---|---|---|
| New/deleted/renamed `Doc/*.md` | This file → "Key Files" table | Key Files |
| New/deleted/renamed `Doc/*.md` | `Doc/DOCUMENTATION_INDEX.md` | Index entry |
| New/deleted/renamed `Doc/*.md` | Root `CLAUDE.md` → "Key File Locations" | Key File Locations |
| New service added or port changed | This file + root `CLAUDE.md` | Architecture services table |
| New shared library added to `src/Shared/` | This file | "Shared Libraries" table |
| New endpoint pattern or pitfall discovered | This file | "Common Pitfalls" or "Essential Patterns" |
| New pattern added to `.claude/instructions/` | This file or relevant instructions file | Relevant section |
| New database strategy detail discovered | `.claude/instructions/database-strategy.instructions.md` | Relevant strategy section |

---

@.claude/instructions/Dotnet.instructions.md
@.claude/instructions/database-strategy.instructions.md
