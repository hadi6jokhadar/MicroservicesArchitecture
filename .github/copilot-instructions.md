# GitHub Copilot Instructions for MicroservicesArchitecture

## Project Overview

.NET 8 microservices with Clean Architecture, DDD, CQRS, optional multi-tenancy, and database-per-tenant support.

## CRITICAL: Workflow for Every Task

1. **Read** relevant `Doc/*.md` files first (start with `Doc/00_START_HERE.md`)
2. **Implement** the requested changes
3. **Update** affected documentation in `Doc/` folder
4. **Update** `Doc/00_START_HERE.md` if you added/modified doc files

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
- See: `Doc/DATETIME_STANDARDIZATION_SUMMARY.md`

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

See: `Doc/AUTOMAPPER_REMOVAL_SUMMARY.md`

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

### Caching Strategy

- `Redis:Enabled=true` (production) or `false` (dev - auto in-memory fallback)
- Used for tenant configs, SignalR backplane
- See: `Doc/REDIS_ENABLED_VS_DISABLED_GUIDE.md`

## Key Files to Reference

- `Doc/00_START_HERE.md` - Complete documentation index
- `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md` - Creating new services
- `Doc/MULTI_TENANCY_QUICK_START.md` - Multi-tenancy setup
- `Doc/BYPASS_TENANT_ENDPOINTS_GUIDE.md` - Admin/global endpoints
- `Doc/BOTTLENECKS_COMPLETION_SUMMARY.md` - Performance optimizations

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

See: `.github/instructions/terminal.instructions.md`

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
- See: `Doc/MINIMAL_API_MIGRATION.md`

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
- Enable multi-tenancy? → `Doc/MULTI_TENANCY_QUICK_START.md`
- Send notifications? → `Doc/SERVICE_TO_NOTIFICATION_INTEGRATION_GUIDE.md`
- Optimize performance? → `Doc/BOTTLENECKS_COMPLETION_SUMMARY.md`
- Write tests? → `Doc/SHARED_TESTING_FILES.md`

## Technology Stack Reference

- **.NET 8**, **C# 12**, **EF Core 9.0**
- **MediatR 12.4** (CQRS), **FluentValidation 12.0**
- **PostgreSQL** (primary), SQL Server, MySQL, SQLite supported
- **Redis 2.7** (distributed cache), **SignalR 8.0** (real-time)
- **xUnit 2.6**, **Moq 4.20**, **FluentAssertions 6.12**

---

**Last Updated**: November 2025 | **Version**: 2.1
