# 🔧 New Service Design Pattern - Stage 2: Configuration & Integration

**Version:** 1.0  
**Last Updated:** January 2025  
**Stage:** 2 of 3 - Configuration & Integration  
**Previous Stage:** [Stage 1: Architecture & Structure](NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md)  
**Next Stage:** [Stage 3: Implementation & Testing](NEW_SERVICE_DESIGN_PATTERN_STAGE_3.md)

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Configuration Decision Tree](#configuration-decision-tree)
3. [Authentication Configuration](#authentication-configuration)
4. [Multi-Tenancy Configuration](#multi-tenancy-configuration)
5. [Database Configuration](#database-configuration)
6. [Caching Configuration](#caching-configuration)
7. [Shared Libraries Integration](#shared-libraries-integration)
8. [Middleware Pipeline Setup](#middleware-pipeline-setup)
9. [Service-to-Service Communication](#service-to-service-communication)
10. [Package Management](#package-management)
11. [Checklist](#stage-2-checklist)

---

## Overview

### What This Stage Covers

This document provides complete configuration and integration patterns for your new microservice. By the end of Stage 2, you will have:

- ✅ Authentication configured (JWT)
- ✅ Multi-tenancy enabled (if required)
- ✅ Database provider configured (PostgreSQL/SQL Server/MySQL/SQLite)
- ✅ Caching strategy implemented (Redis or MemoryCache)
- ✅ Shared libraries integrated
- ✅ Middleware pipeline established
- ✅ Service-to-service communication ready

### Prerequisites

Before starting Stage 2:

- [ ] Completed [Stage 1: Architecture & Structure](NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md)
- [ ] All projects created and structured
- [ ] Domain entities and repositories defined
- [ ] Application commands/queries created

---

## Configuration Decision Tree

### Authentication Decision Matrix

```
┌─────────────────────────────────────────────────────────────────┐
│ Does this service need authentication?                          │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                                   │
         ▼                                   ▼
   ┌────────────┐                      ┌────────────┐
   │ YES        │                      │ NO         │
   │            │                      │            │
   └─────┬──────┘                      └─────┬──────┘
         │                                   │
         ▼                                   ▼
   Configure JWT:                      Skip authentication:
   • Add JWT settings                  • No JWT config needed
   • Enable authentication             • No auth middleware
   • Add user context access           • Public endpoints
   • Role-based authorization          • No user context

   Examples:                           Examples:
   • Order Service                     • Public Catalog
   • Customer Service                  • Health Check Service
   • Admin Panel                       • Monitoring Service
```

### Multi-Tenancy Decision Matrix

```
┌─────────────────────────────────────────────────────────────────┐
│ Does this service need multi-tenancy?                           │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                                   │
         ▼                                   ▼
   ┌────────────┐                      ┌────────────┐
   │ YES        │                      │ NO         │
   │ (Consumer) │                      │ (Static)   │
   └─────┬──────┘                      └─────┬──────┘
         │                                   │
         ▼                                   ▼
   Configure Multi-Tenancy:            Use Static Config:
   • Enable MultiTenancy:true          • Set MultiTenancy:false
   • Add TenantServiceUrl              • Use appsettings.json
   • Configure cache expiration        • Single database
   • Add x-tenant-id header support    • No tenant resolution
   • Dynamic DB connections            • Simpler deployment

   Examples:                           Examples:
   • Order Service                     • Identity Service (provider)
   • Product Service                   • Tenant Service (provider)
   • Customer Service                  • Notification Service (provider)

   CRITICAL: Identity, Tenant, and Notification are
   PROVIDERS of multi-tenancy services, NOT consumers!
   They use static configuration from appsettings.json.
```

### Database Provider Decision Matrix

```
┌─────────────────────────────────────────────────────────────────┐
│ Which database provider will this service use?                  │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┬────────────┐
         │                 │                 │            │
         ▼                 ▼                 ▼            ▼
   ┌──────────┐      ┌──────────┐     ┌──────────┐  ┌──────────┐
   │PostgreSQL│      │SQL Server│     │  MySQL   │  │  SQLite  │
   │(Default) │      │          │     │          │  │          │
   └──────────┘      └──────────┘     └──────────┘  └──────────┘
        │                 │                 │            │
   Production        Production        Production   Development
   Recommended       Enterprise        Cloud/Web    Testing Only

   • Best perf.     • MS ecosystem    • AWS/GCP    • In-memory
   • JSONB support  • Azure           • Managed    • No setup
   • Free & OSS     • T-SQL           • Scalable   • Prototyping
```

### Caching Strategy Decision Matrix

```
┌─────────────────────────────────────────────────────────────────┐
│ What caching strategy will this service use?                    │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                                   │
         ▼                                   ▼
   ┌────────────┐                      ┌────────────┐
   │ Redis      │                      │MemoryCache │
   │ (Enabled)  │                      │ (Disabled) │
   └─────┬──────┘                      └─────┬──────┘
         │                                   │
         ▼                                   ▼
   Production:                         Development:
   • Set Redis:Enabled=true            • Set Redis:Enabled=false
   • Distributed cache                 • In-memory cache (automatic)
   • Shared across instances           • Per-instance cache
   • SignalR scaling support           • Single-instance only
   • 95%+ cache hit rate               • 70-85% cache hit rate
   • Requires Redis server             • Zero setup

   Use When:                           Use When:
   • Multiple service instances        • Single instance deployment
   • Horizontal scaling needed         • Local development
   • Production environment            • Testing environment
   • SignalR multi-instance            • Rapid prototyping

   CRITICAL: System automatically falls back to MemoryCache
   when Redis:Enabled=false. No code changes needed!
```

---

## Authentication Configuration

### Step 1: Add JWT Configuration to appsettings.json

**File:** `{ServiceName}.API/appsettings.json`

```json
{
  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters-long-for-production-use",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 21600,
    "RefreshTokenExpirationDays": 7
  }
}
```

**CRITICAL:** The JWT configuration MUST be **IDENTICAL** across ALL services!

- ✅ Use the **SAME Secret** in all services
- ✅ Use the **SAME Issuer** in all services
- ✅ Use the **SAME Audience** in all services
- ❌ Different values = token validation fails

**Production Secret Management:**

```json
// appsettings.Production.json
{
  "Jwt": {
    "Secret": "${JWT_SECRET}", // Environment variable
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp"
  }
}
```

### Step 2: Add Authentication Packages

**File:** `{ServiceName}.API/{ServiceName}.API.csproj`

```xml
<ItemGroup>
  <!-- Authentication -->
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />

  <!-- HTTP Context Access (for user context) -->
  <PackageReference Include="Microsoft.AspNetCore.Http" />
</ItemGroup>
```

**Note:** Package versions are managed centrally in `Directory.Packages.props` at the root.

### Step 3: Configure Authentication in Program.cs

**File:** `{ServiceName}.API/Program.cs`

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Authentication & Authorization
// ============================================
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT Secret is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
    };
});

builder.Services.AddAuthorization();

// Register HTTP context accessor for user context access
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ============================================
// Middleware Pipeline (ORDER MATTERS!)
// ============================================
app.UseAuthentication(); // MUST come before UseAuthorization
app.UseAuthorization();

// ... rest of middleware

app.Run();
```

### Step 4: Access Authenticated User in Handlers

**File:** `{ServiceName}.Application/Handlers/{Feature}/{Action}Handler.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

public class Create{Entity}Handler : IRequestHandler<Create{Entity}Command, {Entity}Dto>
{
    private readonly I{Entity}Repository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public Create{Entity}Handler(
        I{Entity}Repository repository,
        IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<{Entity}Dto> Handle(
        Create{Entity}Command request,
        CancellationToken cancellationToken)
    {
        // Extract user information from JWT claims
        var httpContext = _httpContextAccessor.HttpContext;
        var userIdClaim = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier);
        var emailClaim = httpContext?.User.FindFirst(ClaimTypes.Email);
        var roleClaim = httpContext?.User.FindFirst(ClaimTypes.Role);

        if (userIdClaim == null)
            throw new UnauthorizedException("User not authenticated");

        var userId = int.Parse(userIdClaim.Value);
        var userEmail = emailClaim?.Value;
        var userRole = roleClaim?.Value;

        // Use user info in business logic
        var entity = new {Entity}
        {
            UserId = userId,
            CreatedBy = userEmail,
            // ... rest of properties
        };

        var created = await _repository.AddAsync(entity, cancellationToken);
        return MapToDto(created);
    }
}
```

### Step 5: Protect Endpoints with Authorization

**File:** `{ServiceName}.API/Endpoints/{Feature}Endpoints.cs`

```csharp
public static void Map{Entity}Endpoints(this IEndpointRouteBuilder routes)
{
    // Option 1: Require authentication for entire group
    var group = routes.MapGroup("/api/{entities}")
        .WithTags("{Entities}")
        .RequireAuthorization(); // All endpoints require authentication

    group.MapGet("/", GetAll{Entities});
    group.MapGet("/{id:int}", Get{Entity}ById);
    group.MapPost("/", Create{Entity});

    // Option 2: Require specific role
    var adminGroup = routes.MapGroup("/api/admin/{entities}")
        .WithTags("Admin {Entities}")
        .RequireAuthorization(policy => policy.RequireRole("Admin"));

    adminGroup.MapDelete("/{id:int}", Delete{Entity});

    // Option 3: Allow anonymous (no authorization)
    var publicGroup = routes.MapGroup("/api/public/{entities}")
        .WithTags("Public {Entities}")
        .AllowAnonymous();

    publicGroup.MapGet("/featured", GetFeatured{Entities});
}
```

---

## Multi-Tenancy Configuration

### When to Enable Multi-Tenancy

**Enable Multi-Tenancy (`true`) When:**

- ✅ Service manages tenant-specific data (Orders, Products, Customers)
- ✅ Service needs dynamic database connections per tenant
- ✅ Service requires tenant-specific configuration
- ✅ Service needs to isolate data by tenant

**Disable Multi-Tenancy (`false`) When:**

- ✅ Service is a **provider** of multi-tenancy (Identity, Tenant, Notification)
- ✅ Service uses static configuration from appsettings.json
- ✅ Service doesn't manage tenant-specific data
- ✅ Service is a simple gateway or proxy

### Step 1: Add Multi-Tenancy Configuration

**File:** `{ServiceName}.API/appsettings.json`

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "Shared",
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 30
  }
}
```

**Configuration Options:**

| Setting                  | Value        | Description                                     |
| ------------------------ | ------------ | ----------------------------------------------- |
| `Enabled`                | `true/false` | Enable/disable multi-tenancy for this service   |
| `JwtMode`                | `Shared`     | Superadmin can access all tenants (recommended) |
| `JwtMode`                | `PerTenant`  | Each tenant validates with own JWT secret       |
| `TenantServiceUrl`       | URL          | Tenant Service endpoint for config retrieval    |
| `CacheExpirationMinutes` | Number       | How long to cache tenant configs (default: 30)  |

**Important:** When `Enabled = false`, the service uses static configuration from appsettings.json (no tenant resolution).

### Step 2: Add Multi-Tenancy Support in Program.cs

**File:** `{ServiceName}.API/Program.cs`

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Multi-Tenancy Support (OPTIONAL)
// ============================================
// This single line registers everything:
// ✅ Tenant middleware (automatic tenant resolution)
// ✅ ITenantContext (access current tenant)
// ✅ ITenantService (fetch tenant configuration)
// ✅ Caching (distributed Redis or in-memory fallback)
// ✅ HTTP client for Tenant Service

builder.Services.AddMultiTenancy(builder.Configuration);

var app = builder.Build();

// ============================================
// Database Migration (if-else based on multi-tenancy)
// ============================================
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");

if (multiTenancyEnabled)
{
    // Multi-tenant mode: Requires x-tenant-id header
    app.UseTenantDatabaseMigration();
}
else
{
    // Single-tenant mode: Uses appsettings.json
    app.UseDefaultDatabaseMigration();
}

app.Run();
```

**CRITICAL:** You do NOT need to manually implement or register tenant middleware. It's **already implemented** in `IhsanDev.Shared.Infrastructure` and automatically registered when you call `AddMultiTenancy()`.

### Step 3: Access Tenant Context in Handlers

**File:** `{ServiceName}.Application/Handlers/{Feature}/{Action}Handler.cs`

```csharp
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

public class Create{Entity}Handler : IRequestHandler<Create{Entity}Command, {Entity}Dto>
{
    private readonly I{Entity}Repository _repository;
    private readonly ITenantContext _tenantContext;

    public Create{Entity}Handler(
        I{Entity}Repository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<{Entity}Dto> Handle(
        Create{Entity}Command request,
        CancellationToken cancellationToken)
    {
        // Check if tenant context is available
        string? tenantId = null;

        if (_tenantContext.HasTenant && _tenantContext.CurrentTenant != null)
        {
            tenantId = _tenantContext.CurrentTenant.TenantId;

            // Access tenant-specific configuration
            var jwtConfig = _tenantContext.CurrentTenant.Configuration?.Jwt;
            var databaseConfig = _tenantContext.CurrentTenant.Configuration?.Database;
        }

        // Create entity with tenant context
        var entity = new {Entity}
        {
            TenantId = tenantId, // null if multi-tenancy disabled
            // ... rest of properties
        };

        var created = await _repository.AddAsync(entity, cancellationToken);
        return MapToDto(created);
    }
}
```

### Step 4: Client Requests with Tenant ID

**Option 1: HTTP Header (Recommended)**

```bash
curl -X GET "https://localhost:5001/api/orders" \
  -H "Authorization: Bearer <jwt-token>" \
  -H "x-tenant-id: acme-corp-12345"
```

**Option 2: Subdomain (Requires Custom Middleware)**

```
https://acme-corp.yourapp.com/api/orders
```

**Option 3: Query Parameter (Not Recommended)**

```bash
curl -X GET "https://localhost:5001/api/orders?tenantId=acme-corp-12345" \
  -H "Authorization: Bearer <jwt-token>"
```

---

## Database Configuration

### Database-Per-Tenant Pattern

**CRITICAL UNDERSTANDING:**

```
┌─────────────────────────────────────────────────────────────────┐
│ ONE Service Binary → MULTIPLE Tenant Databases                  │
│                                                                   │
│  • ONE codebase, ONE DbContext class                            │
│  • MULTIPLE database instances (one per tenant)                 │
│  • Dynamic connection based on tenant configuration             │
│  • Complete data isolation between tenants                      │
└─────────────────────────────────────────────────────────────────┘

Request Flow:
Client → Middleware extracts x-tenant-id header
      → Tenant Service fetches tenant config (includes DB connection)
      → DbContext created with tenant's connection string
      → Query executes on tenant's database
      → Response returned
```

**Key Points:**

- ✅ You define **ONE** DbContext class
- ✅ Middleware routes to **DIFFERENT** databases per tenant
- ✅ Each tenant gets **SEPARATE** database (complete isolation)
- ✅ Databases are **automatically created** on first request
- ✅ Migrations applied **automatically** per tenant database

See: [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) for complete explanation.

### Step 1: Add Database Configuration

**File:** `{ServiceName}.API/appsettings.json`

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database={ServiceName}Db;Username=postgres;Password=postgres"
  }
}
```

**Supported Providers:**

| Provider   | Value        | Package Required                          |
| ---------- | ------------ | ----------------------------------------- |
| PostgreSQL | `PostgreSql` | `Npgsql.EntityFrameworkCore.PostgreSQL`   |
| SQL Server | `SqlServer`  | `Microsoft.EntityFrameworkCore.SqlServer` |
| MySQL      | `MySql`      | `Pomelo.EntityFrameworkCore.MySql`        |
| SQLite     | `Sqlite`     | `Microsoft.EntityFrameworkCore.Sqlite`    |

**Multi-Tenant Override:**

When multi-tenancy is enabled (`MultiTenancy:Enabled = true`):

- ✅ Tenant Service provides per-tenant database connection strings
- ✅ `appsettings.json` connection string becomes **fallback** for tenants without custom DB
- ✅ System automatically uses tenant-specific DB if configured in Tenant Service

### Step 2: Add Database Packages

**File:** `{ServiceName}.Infrastructure/{ServiceName}.Infrastructure.csproj`

```xml
<ItemGroup>
  <!-- Entity Framework Core -->
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />

  <!-- Choose ONE database provider -->

  <!-- PostgreSQL (Recommended) -->
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />

  <!-- OR SQL Server -->
  <!-- <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" /> -->

  <!-- OR MySQL -->
  <!-- <PackageReference Include="Pomelo.EntityFrameworkCore.MySql" /> -->

  <!-- OR SQLite (Development Only) -->
  <!-- <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" /> -->
</ItemGroup>
```

### Step 3: Register Database Context in Program.cs

**File:** `{ServiceName}.API/Program.cs`

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;
using {ServiceName}.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Database Configuration
// ============================================
// This extension method:
// ✅ Reads DatabaseSettings:Provider from config
// ✅ Configures appropriate database provider
// ✅ Sets up connection string (static or dynamic per tenant)
// ✅ Registers DbContext in DI container

builder.Services.AddDatabaseContext<{ServiceName}DbContext>(
    builder.Configuration,
    migrationAssembly: typeof({ServiceName}DbContext).Assembly.GetName().Name);

var app = builder.Build();

// ============================================
// Automatic Database Migration
// ============================================
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");

if (multiTenancyEnabled)
{
    // Multi-tenant: Automatically creates and migrates tenant databases
    // Triggered by first request with x-tenant-id header
    app.UseTenantDatabaseMigration();
}
else
{
    // Single-tenant: Creates and migrates database from appsettings.json
    // Executed once at startup
    app.UseDefaultDatabaseMigration();
}

app.Run();
```

**Automatic Migration Behavior:**

```
Multi-Tenancy ENABLED (true):
  → First request with x-tenant-id: "acme-123"
  → Middleware fetches tenant config (includes DB connection)
  → Checks if database exists
  → If NOT exists: Creates database
  → Applies all pending migrations
  → Database ready for use
  → Subsequent requests use migrated database

Multi-Tenancy DISABLED (false):
  → Service startup
  → Reads connection string from appsettings.json
  → Checks if database exists
  → If NOT exists: Creates database
  → Applies all pending migrations
  → Service starts
```

See: [AUTOMATIC_DATABASE_MIGRATION.md](AUTOMATIC_DATABASE_MIGRATION.md) for complete details.

### Step 4: Create and Apply Migrations

**Create Migration (from Infrastructure project):**

```bash
cd src/Services/{ServiceName}/{ServiceName}.Infrastructure

dotnet ef migrations add InitialCreate --startup-project ../{ServiceName}.API
```

**Manual Migration (Optional - Usually Not Needed):**

```bash
cd src/Services/{ServiceName}/{ServiceName}.API

dotnet ef database update
```

**Note:** Manual migration is rarely needed because:

- ✅ Multi-tenant databases are auto-migrated on first request
- ✅ Single-tenant databases are auto-migrated at startup

---

## Caching Configuration

### Redis vs MemoryCache Decision

**Use Redis (`Redis:Enabled = true`) When:**

- ✅ Production environment
- ✅ Multiple service instances (horizontal scaling)
- ✅ SignalR multi-instance setup
- ✅ Need cache persistence across restarts
- ✅ Want 95%+ cache hit rate

**Use MemoryCache (`Redis:Enabled = false`) When:**

- ✅ Development environment
- ✅ Single service instance
- ✅ Local testing
- ✅ Rapid prototyping
- ✅ Don't want to manage Redis server

**Key Insight:** System **automatically falls back** to `IMemoryCache` when `Redis:Enabled = false`. No code changes needed!

See: [REDIS_ENABLED_VS_DISABLED_GUIDE.md](REDIS_ENABLED_VS_DISABLED_GUIDE.md) for complete comparison.

### Step 1: Add Caching Configuration

**Development (MemoryCache):**

```json
{
  "Redis": {
    "Enabled": false
  }
}
```

**Production (Redis):**

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "your-redis-server:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  }
}
```

### Step 2: Add Caching Packages (If Using Redis)

**File:** `{ServiceName}.API/{ServiceName}.API.csproj`

```xml
<ItemGroup>
  <!-- Redis (only needed if Redis:Enabled = true) -->
  <PackageReference Include="StackExchange.Redis" />
  <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" />
</ItemGroup>
```

**Note:** `IMemoryCache` is built into ASP.NET Core (no packages needed).

### Step 3: Use Caching in Handlers (Optional)

**Caching is automatically used for:**

- ✅ Tenant configuration (cached for 30 minutes by default)
- ✅ SignalR message distribution (if Redis enabled)

**Manual caching (if needed):**

```csharp
using Microsoft.Extensions.Caching.Distributed;

public class Get{Entity}Handler : IRequestHandler<Get{Entity}Query, {Entity}Dto>
{
    private readonly IDistributedCache _cache;
    private readonly I{Entity}Repository _repository;

    public async Task<{Entity}Dto> Handle(Get{Entity}Query request, CancellationToken ct)
    {
        var cacheKey = $"{entity}:{request.Id}";

        // Try to get from cache
        var cachedJson = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            return JsonSerializer.Deserialize<{Entity}Dto>(cachedJson);
        }

        // Not in cache, fetch from database
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        var dto = MapToDto(entity);

        // Store in cache (1 hour)
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        };

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(dto),
            options,
            ct);

        return dto;
    }
}
```

---

## Shared Libraries Integration

### Available Shared Libraries

| Library                          | Purpose                                             | When to Use                      |
| -------------------------------- | --------------------------------------------------- | -------------------------------- |
| `IhsanDev.Shared.Kernel`         | Base entities, tenant interfaces, domain primitives | **ALL services** (required)      |
| `IhsanDev.Shared.Application`    | CQRS interfaces, validators, exceptions             | **ALL services** (required)      |
| `IhsanDev.Shared.Infrastructure` | Middleware, database extensions, caching            | **ALL services** (required)      |
| `IhsanDev.Shared.Authentication` | JWT generation, validation, service auth            | Services needing auth            |
| `IhsanDev.Shared.Notifications`  | Notification client, SignalR hub                    | Services sending notifications   |
| `IhsanDev.Shared.Testing`        | Test helpers, factories, fixtures                   | **ALL test projects** (required) |
| `IhsanDev.Shared.Messaging`      | Event bus, message queue abstractions               | Services with async messaging    |

### Step 1: Add Shared Library References

**Domain Layer:** `{ServiceName}.Domain/{ServiceName}.Domain.csproj`

```xml
<ItemGroup>
  <!-- Domain needs Kernel only -->
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Kernel\IhsanDev.Shared.Kernel.csproj" />
</ItemGroup>
```

**Application Layer:** `{ServiceName}.Application/{ServiceName}.Application.csproj`

```xml
<ItemGroup>
  <!-- Application needs Kernel + Application -->
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Kernel\IhsanDev.Shared.Kernel.csproj" />
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Application\IhsanDev.Shared.Application.csproj" />

  <!-- Reference Domain -->
  <ProjectReference Include="..\..\{ServiceName}.Domain\{ServiceName}.Domain.csproj" />
</ItemGroup>
```

**Infrastructure Layer:** `{ServiceName}.Infrastructure/{ServiceName}.Infrastructure.csproj`

```xml
<ItemGroup>
  <!-- Infrastructure needs Kernel + Infrastructure -->
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Kernel\IhsanDev.Shared.Kernel.csproj" />
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Infrastructure\IhsanDev.Shared.Infrastructure.csproj" />

  <!-- Reference Domain + Application -->
  <ProjectReference Include="..\..\{ServiceName}.Domain\{ServiceName}.Domain.csproj" />
  <ProjectReference Include="..\..\{ServiceName}.Application\{ServiceName}.Application.csproj" />
</ItemGroup>
```

**API Layer:** `{ServiceName}.API/{ServiceName}.API.csproj`

```xml
<ItemGroup>
  <!-- API references all layers -->
  <ProjectReference Include="..\{ServiceName}.Application\{ServiceName}.Application.csproj" />
  <ProjectReference Include="..\{ServiceName}.Infrastructure\{ServiceName}.Infrastructure.csproj" />

  <!-- Shared libraries -->
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Infrastructure\IhsanDev.Shared.Infrastructure.csproj" />
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Authentication\IhsanDev.Shared.Authentication.csproj" />
</ItemGroup>
```

**Test Layer:** `{ServiceName}.API.Tests/{ServiceName}.API.Tests.csproj`

```xml
<ItemGroup>
  <!-- Test needs all shared libraries -->
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Testing\IhsanDev.Shared.Testing.csproj" />

  <!-- Reference API project -->
  <ProjectReference Include="..\..\{ServiceName}.API\{ServiceName}.API.csproj" />
</ItemGroup>
```

### Step 2: Use Base Entities from Shared.Kernel

**File:** `{ServiceName}.Domain/Entities/{Entity}.cs`

```csharp
using IhsanDev.Shared.Kernel.Entities;

namespace {ServiceName}.Domain.Entities;

// Inherit from BaseEntity (provides Id, CreatedAt, UpdatedAt)
public class {Entity} : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? TenantId { get; set; } // Required if multi-tenant
    // ... rest of properties
}
```

**BaseEntity Definition (from Shared.Kernel):**

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 3: Use Tenant Interfaces from Shared.Kernel

**File:** `{ServiceName}.Application/Handlers/{Feature}/{Action}Handler.cs`

```csharp
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

public class Create{Entity}Handler : IRequestHandler<Create{Entity}Command, {Entity}Dto>
{
    private readonly ITenantContext _tenantContext; // From Shared.Kernel

    public Create{Entity}Handler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task<{Entity}Dto> Handle(...)
    {
        if (_tenantContext.HasTenant)
        {
            var tenantId = _tenantContext.CurrentTenant.TenantId;
            // Use tenant context
        }
    }
}
```

---

## Middleware Pipeline Setup

### Middleware Order (CRITICAL!)

The order of middleware matters! Use this exact order:

```csharp
var app = builder.Build();

// ============================================
// Middleware Pipeline (ORDER MATTERS!)
// ============================================

// 1. Exception handling (must be first)
app.UseExceptionHandler("/error");

// 2. HTTPS redirection
app.UseHttpsRedirection();

// 3. Static files (if needed)
app.UseStaticFiles();

// 4. Routing
app.UseRouting();

// 5. CORS (if needed)
app.UseCors("AllowAll");

// 6. Authentication (MUST come before Authorization)
app.UseAuthentication();

// 7. Authorization
app.UseAuthorization();

// 8. Tenant resolution (OPTIONAL - if multi-tenancy enabled)
// NOTE: Automatically registered by AddMultiTenancy()
// You DO NOT need to manually add it

// 9. Database migration (automatic, if-else based on multi-tenancy)
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");
if (multiTenancyEnabled)
    app.UseTenantDatabaseMigration();
else
    app.UseDefaultDatabaseMigration();

// 10. Endpoints (must be last)
app.MapEndpoints(); // Your endpoint registrations

app.Run();
```

**Common Mistakes:**

- ❌ `UseAuthorization()` before `UseAuthentication()` → 401 errors
- ❌ `UseRouting()` after `UseAuthentication()` → Routing fails
- ❌ Manually adding tenant middleware → Already done by `AddMultiTenancy()`

---

## Service-to-Service Communication

### When Needed

Your service needs service-to-service communication when:

- ✅ Sending notifications (calls Notification Service)
- ✅ Validating user permissions (calls Identity Service)
- ✅ Fetching tenant configuration (calls Tenant Service)
- ✅ Uploading files (calls File Manager Service)

### Step 1: Add Service Secret Configuration

**File:** `{ServiceName}.API/appsettings.json`

```json
{
  "ServiceAuthentication": {
    "SharedSecret": "your-service-shared-secret-minimum-32-characters"
  },
  "ExternalServices": {
    "NotificationService": "https://localhost:5004",
    "TenantService": "https://localhost:5002",
    "IdentityService": "https://localhost:5001"
  }
}
```

**CRITICAL:** The `SharedSecret` MUST be **IDENTICAL** across all services!

### Step 2: Register HTTP Clients in Program.cs

**File:** `{ServiceName}.API/Program.cs`

```csharp
using IhsanDev.Shared.Infrastructure.Clients;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// HTTP Clients for Service-to-Service Communication
// ============================================
var notificationServiceUrl = builder.Configuration["ExternalServices:NotificationService"]
    ?? "https://localhost:5004";

builder.Services.AddHttpClient<INotificationServiceClient, NotificationServiceClient>(client =>
{
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
```

### Step 3: Use Service Clients in Handlers

**File:** `{ServiceName}.Application/Handlers/{Feature}/{Action}Handler.cs`

```csharp
using IhsanDev.Shared.Infrastructure.Clients;

public class Create{Entity}Handler : IRequestHandler<Create{Entity}Command, {Entity}Dto>
{
    private readonly INotificationServiceClient _notificationClient;

    public Create{Entity}Handler(INotificationServiceClient notificationClient)
    {
        _notificationClient = notificationClient;
    }

    public async Task<{Entity}Dto> Handle(
        Create{Entity}Command request,
        CancellationToken cancellationToken)
    {
        // Create entity
        var entity = new {Entity} { /* ... */ };
        var created = await _repository.AddAsync(entity, cancellationToken);

        // Send notification
        await _notificationClient.SendNotificationAsync(
            tenantId: entity.TenantId,
            userId: entity.UserId,
            title: "{Entity} Created",
            message: $"New {entity} has been created successfully",
            cancellationToken: cancellationToken);

        return MapToDto(created);
    }
}
```

See: [SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md](SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md) for complete details.

---

## Package Management

### Central Package Versioning

This project uses **Central Package Management** (CPM). All package versions are defined in one place.

**File:** `Directory.Packages.props` (root of repository)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- ASP.NET Core -->
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />

    <!-- Entity Framework Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />

    <!-- MediatR -->
    <PackageVersion Include="MediatR" Version="12.4.0" />

    <!-- ... more packages -->
  </ItemGroup>
</Project>
```

### Adding Packages to Your Service

**Step 1: Check if package version exists in Directory.Packages.props**

```bash
# Search for package
grep -i "PackageName" Directory.Packages.props
```

**Step 2: Add package reference WITHOUT version**

```xml
<ItemGroup>
  <!-- Version managed centrally -->
  <PackageReference Include="MediatR" />
  <PackageReference Include="FluentValidation" />
</ItemGroup>
```

**Step 3: If package not in Directory.Packages.props, add it there first**

```xml
<!-- In Directory.Packages.props -->
<ItemGroup>
  <PackageVersion Include="NewPackage" Version="1.0.0" />
</ItemGroup>

<!-- Then reference in your project -->
<ItemGroup>
  <PackageReference Include="NewPackage" />
</ItemGroup>
```

### Updating All Packages

**Script:** `update-csproj.ps1` (root of repository)

```powershell
# Run from repository root
.\update-csproj.ps1
```

This script:

- ✅ Reads all package versions from `Directory.Packages.props`
- ✅ Updates all `.csproj` files to remove inline versions
- ✅ Ensures consistent package versions across all services

---

## Stage 2 Checklist

Before proceeding to Stage 3, ensure you have:

### Authentication

- [ ] Added JWT configuration to `appsettings.json`
- [ ] JWT settings match Identity Service (Secret, Issuer, Audience)
- [ ] Added `Microsoft.AspNetCore.Authentication.JwtBearer` package
- [ ] Configured authentication in `Program.cs`
- [ ] Registered `IHttpContextAccessor` for user context
- [ ] Added authentication middleware (`app.UseAuthentication()`)
- [ ] Protected endpoints with `RequireAuthorization()`

### Multi-Tenancy (if enabled)

- [ ] Added multi-tenancy configuration to `appsettings.json`
- [ ] Decided on `JwtMode` (Shared or PerTenant)
- [ ] Called `AddMultiTenancy()` in `Program.cs`
- [ ] Added if-else for database migration (tenant vs default)
- [ ] Tested with `x-tenant-id` header
- [ ] Verified tenant resolution works

### Database

- [ ] Added database configuration to `appsettings.json`
- [ ] Chosen database provider (PostgreSQL/SQL Server/MySQL/SQLite)
- [ ] Added appropriate database provider package
- [ ] Called `AddDatabaseContext<T>()` in `Program.cs`
- [ ] Created DbContext class
- [ ] Created entity configurations
- [ ] Implemented repositories
- [ ] Created and applied initial migration
- [ ] Verified automatic migration works

### Caching

- [ ] Decided on caching strategy (Redis or MemoryCache)
- [ ] Added Redis configuration (if using Redis)
- [ ] Added Redis packages (if using Redis)
- [ ] Verified caching works (check logs)

### Shared Libraries

- [ ] Added references to all required shared libraries
- [ ] Used `BaseEntity` from Shared.Kernel
- [ ] Used tenant interfaces (if multi-tenant)
- [ ] Used shared exceptions (if applicable)

### Middleware

- [ ] Configured middleware pipeline in correct order
- [ ] Verified authentication comes before authorization
- [ ] Added exception handling middleware
- [ ] Configured CORS (if needed)

### Service Communication (if needed)

- [ ] Added service secret configuration
- [ ] Configured HTTP clients for external services
- [ ] Tested service-to-service calls
- [ ] Verified shared secret authentication works

### Package Management

- [ ] All packages referenced without version numbers
- [ ] Verified all packages exist in `Directory.Packages.props`
- [ ] No version conflicts

---

## Next Steps

**Congratulations!** You've completed Stage 2 - Configuration & Integration.

**Next:** Proceed to [Stage 3: Implementation & Testing](NEW_SERVICE_DESIGN_PATTERN_STAGE_3.md) to:

- Implement complete CQRS handlers
- Create API endpoints
- Set up integration testing
- Configure deployment
- Document your API

---

**Built with ❤️ for Clean Architecture & Microservices**

_Last Updated: January 2025_
