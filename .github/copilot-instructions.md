# GitHub Copilot Instructions for MicroservicesArchitecture

## Project Overview

This is a .NET 8 microservices architecture implementing Clean Architecture, DDD, and CQRS patterns with optional multi-tenancy and database-per-tenant support.

## Critical Architecture Concepts

### Multi-Database Per-Tenant Pattern

- **ONE service binary**, **MULTIPLE tenant databases** - Each tenant gets their own isolated database
- Request flow: `Client → TenantMiddleware → Tenant Service (config) → Dynamic DbContext → Tenant's DB`
- `x-tenant-id` header REQUIRED when `MultiTenancy:Enabled=true`, triggers tenant resolution
- When disabled: uses `appsettings.json` with single database
- **Automatic DB creation**: First request auto-creates and migrates tenant database (no manual provisioning)
- See: `Doc/DATABASE_PER_TENANT_ARCHITECTURE.md`, `Doc/AUTOMATIC_DATABASE_MIGRATION.md`

### TenantId vs ProjectId (Critical Distinction)

- **TenantId**: Database boundary - different DBs = complete isolation
- **ProjectId**: Logical filter within same DB - soft isolation via WHERE clauses
- Same email in different tenants = different users (different DBs)
- Same email in different projects = same user (same DB, filtered by ProjectId)
- See: `Doc/PROJECT_ISOLATION_STRATEGY_GUIDE.md`

### Multi-Tenancy Configuration

```json
{
  "MultiTenancy": {
    "Enabled": true, // Toggle for entire system
    "JwtMode": "Shared", // "Shared" (superadmin) or "PerTenant" (isolated)
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 30
  }
}
```

- **Enabled=false**: Traditional mode, uses appsettings.json (no tenant header)
- **Enabled=true**: Tenant mode, requires `x-tenant-id` header, fetches config from Tenant Service
- **JwtMode="Shared"**: Superadmin can access all tenants with one JWT secret
- **JwtMode="PerTenant"**: Each tenant validates JWT with their own secret

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
// DTO Definition
public class MyDto
{
    public string Created { get; set; } = string.Empty;
    public string? LastModified { get; set; }
}

// MapFrom Method (ALWAYS use ToUniversalTime)
public static MyDto MapFrom(MyEntity entity)
{
    return new MyDto
    {
        Id = entity.Id,
        Created = entity.Created.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LastModified = entity.LastModified?.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}

// LINQ Select (EF Core queries)
var dtoQuery = query.Select(e => new MyDto
{
    Id = e.Id,
    Created = e.Created.ToUniversalTime()
        .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
});
```

**Critical Rules:**
- ✅ **ALWAYS** use `.ToUniversalTime()` before `.ToString()`
- ✅ Format: `"yyyy-MM-ddTHH:mm:ssZ"` with `CultureInfo.InvariantCulture`
- ✅ PostgreSQL configured with `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false)`
- ✅ Test parsing: `DateTime.Parse(dto.Created, null, DateTimeStyles.RoundtripKind)`
- ❌ **NEVER** use `DateTime.ToString()` without `.ToUniversalTime()` first
- See: `Doc/DATETIME_STANDARDIZATION_SUMMARY.md`

### Manual Mapping Pattern (No AutoMapper)

**All DTOs use static MapFrom methods:**

```csharp
public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
    
    public static UserDto MapFrom(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Created = user.Created.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
        };
    }
}
```

**Benefits:**
- ✅ Explicit, type-safe mappings
- ✅ No reflection overhead
- ✅ IDE autocomplete support
- ✅ Easier debugging and refactoring
- See: `Doc/AUTOMAPPER_REMOVAL_SUMMARY.md`

### CQRS with MediatR

```csharp
// Commands/Queries in Application layer
public record LoginCommand(string Email, string Password) : IRequest<UserDtoIncludesToken>;

// Handlers in Application/Handlers/{Feature}/
public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDtoIncludesToken>
{
    // Constructor injection: IUserRepository, IJwtTokenGenerator, INotificationServiceClient
    public async Task<UserDtoIncludesToken> Handle(LoginCommand request, CancellationToken ct) { }
}

// Endpoint in API layer (Minimal APIs)
app.MapPost("/api/auth/login", async (LoginCommand command, IMediator mediator)
    => await mediator.Send(command));
```

### Database Context Registration (Multi-Provider + Multi-Tenant)

```csharp
// In Program.cs - supports PostgreSQL, SQL Server, MySQL, SQLite
builder.Services.AddDatabaseContext<IdentityDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(IdentityDbContext).Assembly.GetName().Name);

// Automatic migration middleware (if-else based on MultiTenancy:Enabled)
if (multiTenancyEnabled)
    app.UseTenantDatabaseMigration(); // Requires x-tenant-id header
else
    app.UseDefaultDatabaseMigration(); // Uses appsettings.json
```

### Service-to-Service Communication

- **Shared secret authentication**: Services call each other with `X-Service-Secret` header
- **No JWT required**: Middleware creates service identity with "Service" role
- **Client usage**: Inject `INotificationServiceClient` (from Shared.Infrastructure)

```csharp
await _notificationClient.SendNotificationAsync(
    tenantId: "acme-corp",
    userId: user.Id,
    title: "Welcome!",
    message: "You successfully logged in");
```

- See: `Doc/SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md`

### Caching Strategy (Redis with Automatic Fallback)

- **Production**: Set `Redis:Enabled=true` for distributed caching across instances
- **Development**: Set `Redis:Enabled=false` for automatic in-memory fallback
- **No code changes needed** - abstraction handles fallback transparently
- Used for: Tenant configs (95% fewer API calls), SignalR backplane
- See: `Doc/REDIS_ENABLED_VS_DISABLED_GUIDE.md`

## Development Workflows

### Running Services

```bash
# Identity Service (port 5001)
cd src/Services/Identity/Identity.API
dotnet run

# Tenant Service (port 5002)
cd src/Services/Tenant/Tenant.API
dotnet run

# Notification Service (port 5004)
cd src/Services/Notification/Notification.API
dotnet run

# Or use batch file for all services
run-all-development-instances.bat
```

### Database Migrations

```bash
# Add migration (from Infrastructure project)
cd src/Services/Identity/Identity.Infrastructure
dotnet ef migrations add MigrationName --startup-project ../Identity.API

# Manual migration (usually unnecessary - auto-migration handles it)
cd src/Services/Identity/Identity.API
dotnet ef database update
```

### Testing

- Integration tests in `{Service}.API.Tests/` - Use `WebApplicationFactory`
- Use `TenantTestHelper` from Shared.Testing for tenant data generation
- Example: `src/Services/Identity/Identity.API.Tests/`

### Package Management (Central Package Versioning)

- **All versions in**: `Directory.Packages.props` (root)
- **Projects reference without version**: `<PackageReference Include="MediatR" />`
- **Update script**: `.\update-csproj.ps1` (PowerShell)

## Key Files to Reference

### Starting Points

- `Doc/00_START_HERE.md` - Complete documentation index
- `Doc/README.md` - Project overview and roadmap
- `Doc/QUICK_REFERENCE.md` - Common scenarios

### Creating New Services

- `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md` - Step-by-step service creation (auth, multi-tenancy, testing)

### Multi-Tenancy

- `Doc/MULTI_TENANCY_GUIDE.md` - Comprehensive guide
- `Doc/MULTI_TENANCY_QUICK_START.md` - Quick setup

### Performance & Scaling

- `Doc/BOTTLENECKS_COMPLETION_SUMMARY.md` - All 10 performance optimizations (100k+ concurrent users)
- `Doc/DATABASE_REPLICATION_SETUP_GUIDE.md` - PostgreSQL HA with automatic failover

### Notification System

- `Doc/NOTIFICATION_SERVICE_README.md` - Complete notification guide
- `Doc/NOTIFICATION_HUB_GUIDE.md` - SignalR hub implementation

## Configuration Patterns

### JWT Configuration (MUST be identical across all services)

```json
{
  "Jwt": {
    "Secret": "your-secret-minimum-32-chars",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 21600,
    "RefreshTokenExpirationDays": 7
  }
}
```

### Database Configuration (Multi-Provider)

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql", // or "SqlServer", "MySql", "Sqlite"
    "ConnectionString": "Host=localhost;Database=IdentityDb;Username=user;Password=pass"
  }
}
```

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

## Documentation Rules

- **ALWAYS read** `Doc/*.md` files before making architectural changes
- **ALWAYS update** relevant documentation after completing features
- Start with `Doc/00_START_HERE.md` for navigation
- Most docs are production-ready (✅ in doc index)

## Quick Decision Tree

**Need to...**

- Create new service? → `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md`
- Understand architecture? → `Doc/DATABASE_PER_TENANT_ARCHITECTURE.md`
- Add authentication? → `Doc/SHARED_IDENTITY_SERVICE_GUIDE.md`
- Enable multi-tenancy? → `Doc/MULTI_TENANCY_QUICK_START.md`
- Send notifications? → `Doc/SERVICE_TO_NOTIFICATION_INTEGRATION_GUIDE.md`
- Optimize performance? → `Doc/BOTTLENECKS_COMPLETION_SUMMARY.md`
- Write tests? → `Doc/SHARED_TESTING_FILES.md`

## Technology Stack Reference

- **.NET 8**, **C# 12**, **EF Core 9.0**
- **MediatR 12.4** (CQRS), **FluentValidation 12.0**, **AutoMapper 12.0**
- **PostgreSQL** (primary), SQL Server, MySQL, SQLite supported
- **Redis 2.7** (distributed cache), **SignalR 8.0** (real-time)
- **xUnit 2.6**, **Moq 4.20**, **FluentAssertions 6.12**

---

**Last Updated**: January 2025 | **Version**: 2.0
