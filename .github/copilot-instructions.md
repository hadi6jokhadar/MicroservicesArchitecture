# GitHub Copilot Instructions for MicroservicesArchitecture

## 🎯 Essential Reading First

**CRITICAL:** Before ANY task:

- Read `.github/instructions/Dotnet.instructions.md` for backend rules
- Read `../.github/instructions/terminal.instructions.md` for terminal rules (root workspace)
- Start with `Doc/DOCUMENTATION_INDEX.md` as the single entry point to all documentation
- For Postman sync tasks, use `.github/prompts/generate_postman_collections.prompt.md`

## Project Overview

.NET 8 microservices with Clean Architecture, DDD, CQRS, optional multi-tenancy, and database-per-tenant support.

## CRITICAL: Workflow for Every Task

1. **Read** relevant `Doc/*.md` files first (start with `Doc/DOCUMENTATION_INDEX.md`)
2. **Implement** the requested changes
3. **Update** affected documentation in `Doc/` folder
4. **Update** `Doc/DOCUMENTATION_INDEX.md` if you added/modified doc files

## Architecture Quick Reference

### Multi-Tenancy Modes

**Enabled=true** (x-tenant-id header required):

- Each tenant = separate database (complete isolation)
- Auto-creates tenant DB on first request
- Fetches config from Tenant Service
- JwtMode: "Shared" (superadmin access all) or "PerTenant" (isolated)

**Enabled=false** (traditional mode):

- Single database from appsettings.json
- No tenant header needed

**Optional Tenant** (Identity, FileManager, Notification):

- Works with OR without x-tenant-id header
- Apply `OptionalTenantAttribute` at group level, not per endpoint
- Requires dual database migration (global + tenant)

## Project Structure & Layer Responsibilities

### Service Structure (Identity, Tenant, Notification)

```
Services/{ServiceName}/
├── {ServiceName}.API/          # Minimal APIs, Program.cs, endpoint handlers
├── {ServiceName}.Application/  # CQRS handlers (Commands, Queries), DTOs, validators
├── {ServiceName}.Domain/       # Entities, repository interfaces, domain logic
└── {ServiceName}.Infrastructure/ # EF DbContext, repository implementations, external integrations
```

### Shared Libraries (src/Shared/)

- **IhsanDev.Shared.Kernel**: Base entities (`BaseEntity`, `BaseUser`), tenant interfaces (`ITenantContext`, `ITenantConfigurationProvider`)
- **IhsanDev.Shared.Application**: CQRS interfaces, FluentValidation behaviors, `AppException`, manual mapping patterns
- **IhsanDev.Shared.Infrastructure**: Middlewares (`TenantMiddleware`, `DatabaseMigrationMiddleware`), `INotificationServiceClient`, DateTime UTC handling
- **IhsanDev.Shared.Authentication**: JWT generation/validation, service-to-service auth middleware
- **IhsanDev.Shared.Testing**: `TenantTestHelper`, WebApplicationFactory setups

## Essential Patterns & Conventions

### DateTime Standardization (ISO 8601 UTC)

**ALL DateTime properties in DTOs are strings formatted as UTC:**

```csharp
public static MyDto MapFrom(MyEntity entity)
{
    return new MyDto
    {
        Created = entity.Created.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
```

- ✅ **ALWAYS** use `.ToUniversalTime()` before `.ToString()`
- ✅ Format: `"yyyy-MM-ddTHH:mm:ssZ"` with `CultureInfo.InvariantCulture`

### Manual Mapping Pattern (No AutoMapper)

**All DTOs use static MapFrom methods:**

```csharp
public class UserDto
{
    public static UserDto MapFrom(User user)
    {
        return new UserDto { Id = user.Id, Email = user.Email };
    }
}
```

### CQRS with MediatR

```csharp
// Command
public record LoginCommand(string Email, string Password) : IRequest<UserDtoIncludesToken>;

// Handler in Application/Handlers/{Feature}/
public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDtoIncludesToken>
{
    public async Task<UserDtoIncludesToken> Handle(LoginCommand request, CancellationToken ct) { }
}

// Endpoint (Minimal APIs)
app.MapPost("/api/auth/login", async (LoginCommand cmd, IMediator mediator)
    => await mediator.Send(cmd));
```

### Database Context Registration

```csharp
// Program.cs - supports PostgreSQL, SQL Server, MySQL, SQLite
builder.Services.AddDatabaseContext<IdentityDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(IdentityDbContext).Assembly.GetName().Name);

// Auto-migration
if (multiTenancyEnabled)
    app.UseTenantDatabaseMigration();
else
    app.UseDefaultDatabaseMigration();
```

### Service-to-Service Communication

- Services call each other with `X-Service-Secret` header (no JWT)
- Inject `INotificationServiceClient` from Shared.Infrastructure
- See: `Doc/SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md`

### AI Python ORM Model Standard

- For `src/Services/AI/AI.API/models/*`, use SQLAlchemy 2.0+ Declarative Mapping
- Use `Mapped[...]` and `mapped_column(...)` for all mapped attributes
- Prefer UUID primary keys for distributed scalability
- Use Alembic revisions for schema evolution on existing tables
- See: `Doc/AI_SERVICE_MIGRATION_GUIDE.md`

### Caching Strategy

- `Redis:Enabled=true` (production) or `false` (dev - auto in-memory fallback)
- Used for tenant configs, SignalR backplane
- See: `Doc/CACHING_STRATEGY_COMPARISON.md`

## Key Files to Reference

- `Doc/DOCUMENTATION_INDEX.md` - **START HERE** - Complete documentation index
- `.github/instructions/database-strategy.instructions.md` - **Database strategy decision (A/B/C/D) — read before any new service**
- `Doc/AI_SERVICE_OVERVIEW.md` - AI service architecture and runtime behavior
- `Doc/AI_SERVICE_MIGRATION_GUIDE.md` - AI service migration and schema bootstrap behavior
- `Doc/PYTHON_SHARED_LIBRARY_GUIDE.md` - Shared Python package usage and contracts
- `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md` - Creating new services
- `Doc/DATABASE_PER_TENANT_ARCHITECTURE.md` - Multi-tenancy DB architecture
- `Doc/MULTI_TENANCY_GUIDE.md` - Multi-tenancy setup
- `Doc/BYPASS_TENANT_ENDPOINTS_GUIDE.md` - Admin/global endpoints
- `Doc/PERFORMANCE_OPTIMIZATION_GUIDE.md` - Performance optimizations

## Quick Decision Tree — Database Strategy for New Services

```
Stores tenant-specific data?
├─ NO + system registry?            → Strategy A (Single Global DB)   e.g. TenantService
├─ NO + shared with tenant rows?    → Strategy D (Global + TenantId column) e.g. TranslationService
├─ YES + pure tenant data?          → Strategy B (Per-Tenant DB)      e.g. Identity, FileManager
└─ YES + global queue + tenant log? → Strategy C (Dual DB)            e.g. NotificationService
```

Full patterns (DbContext code, Program.cs pipeline, appsettings):
→ `.github/instructions/database-strategy.instructions.md`

## Common Pitfalls & Solutions

### ❌ DON'T: Chain commands with & in PowerShell/cmd

```bash
# WRONG (& is reserved)
dotnet build & dotnet run
```

✅ **DO**: Run sequentially or use semicolons (PowerShell only)

```bash
dotnet build
dotnet run
# Or in PowerShell: dotnet build; dotnet run
```

See: `.github/instructions/Terminal.instructions.md`

### ❌ DON'T: Create tenant service as multi-tenant

- **Tenant Service itself uses static config** (appsettings.json, single DB)
- It provides configs to OTHER services, doesn't consume them
- See: `Doc/DATABASE_PER_TENANT_ARCHITECTURE.md`

### ❌ DON'T: Manually provision databases

- System auto-creates and migrates databases on first request
- Manual `dotnet ef database update` only needed for development testing
- See: `Doc/AUTOMATIC_DATABASE_MIGRATION.md`

### ❌ DON'T: Use controllers for new endpoints

- Project migrated to **Minimal APIs** (Grouped pattern)
- New endpoints: Use endpoint handlers in `{ServiceName}.API/Endpoints/`
- See: `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md`

### ⚠️ CRITICAL: Admin Endpoints with BypassTenant

**NEW (Nov 2025)** - Special handling required for endpoints that bypass tenant context:

#### JWT Mode Consistency

- **ALWAYS** match `MultiTenancy:JwtMode` across ALL services
- If Identity Service uses `"PerTenant"`, your service MUST use `"PerTenant"`
- Mismatch causes 401 Unauthorized for tenant users

#### JWT Validation Pattern

```csharp
// ❌ WRONG - ITenantContext not populated during OnMessageReceived
var tenantContext = context.HttpContext.RequestServices.GetService<ITenantContext>();

// ✅ CORRECT - Use ITenantConfigurationProvider directly
var provider = context.HttpContext.RequestServices.GetService<ITenantConfigurationProvider>();
var tenant = await provider.GetTenantConfigurationAsync(tenantId, ct);
```

#### DbContext Fallback

```csharp
// ✅ MUST fall back to global database when no tenant context
if (_tenantContext?.HasTenant != true ||
    _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
{
    // Use global database from appsettings.json
    connectionString = _configuration["DatabaseSettings:ConnectionString"];
}
```

#### Dual Database Migration

```csharp
// ✅ ALWAYS run both migrations if you have BypassTenant endpoints
app.UseDefaultDatabaseMigration<YourDbContext>(); // Global DB

if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<YourDbContext>(config); // Tenant DBs
}
```

**See**: `Doc/BYPASS_TENANT_ENDPOINTS_GUIDE.md` - Complete implementation guide with examples

## Quick Decision Tree

**Need to...**

- Create new service? → `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md`
- **Create admin/global endpoints?** → `Doc/BYPASS_TENANT_ENDPOINTS_GUIDE.md`
- Understand architecture? → `Doc/DATABASE_PER_TENANT_ARCHITECTURE.md`
- Add authentication? → `Doc/SHARED_IDENTITY_SERVICE_GUIDE.md`
- Enable multi-tenancy? → `Doc/MULTI_TENANCY_GUIDE.md`
- Send notifications? → `Doc/NOTIFICATION_SERVICE_README.md`
- AI service overview? → `Doc/AI_SERVICE_OVERVIEW.md`
- AI migration behavior? → `Doc/AI_SERVICE_MIGRATION_GUIDE.md`
- Shared Python files? → `Doc/PYTHON_SHARED_LIBRARY_GUIDE.md`
- Optimize performance? → `Doc/PERFORMANCE_OPTIMIZATION_GUIDE.md`
- Write tests? → `Doc/SHARED_TESTING_FILES.md`

## Technology Stack Reference

- **.NET 8**, **C# 12**, **EF Core 9.0**
- **MediatR 12.4** (CQRS), **FluentValidation 12.0**
- **PostgreSQL** (primary), SQL Server, MySQL, SQLite supported
- **Redis 2.7** (distributed cache), **SignalR 8.0** (real-time)
- **xUnit 2.6**, **Moq 4.20**, **FluentAssertions 6.12**

## 🤖 Auto-Maintenance Rules

After completing ANY task, the agent MUST self-check and update instruction files if the codebase changed:

| Change Made                                     | Section to Update                                                      |
| ----------------------------------------------- | ---------------------------------------------------------------------- |
| New/deleted/renamed `Doc/*.md`                  | This file → "Key Files to Reference" + "Quick Decision Tree"           |
| New service added or port changed               | This file + root `copilot-instructions.md` → Architecture service list |
| New shared library added to `src/Shared/`       | This file → "Shared Libraries" list                                    |
| New endpoint pattern or anti-pattern discovered | This file → "Common Pitfalls" or "Essential Patterns"                  |
| New prompt created in `.github/prompts/`        | This file → "Essential Reading First" if it's a core workflow          |

**Do updates inline** — no separate task, update as part of completing the original request.

---

**Last Updated**: April 2026 | **Version**: 2.2
