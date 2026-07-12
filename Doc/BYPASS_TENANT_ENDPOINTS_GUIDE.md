# 🔓 BypassTenant Endpoints Implementation Guide

## ⚠️ CRITICAL: Read This Before Creating Admin/Global Endpoints

This guide explains how to implement endpoints that work **without tenant context** (global/admin endpoints) in a multi-tenant system. These are advanced patterns that require careful implementation to avoid database access errors.

**Last Updated**: July 2026 (JWT validation sections corrected — the previous `OnMessageReceived`/`ITenantConfigurationProvider` pattern shown here was the actual root cause of an intermittent-under-load auth bug; replaced with the real, currently-implemented `IssuerSigningKeyResolver` pattern — see `MULTI_TENANCY_GUIDE.md` and `LOAD_TESTING_GUIDE.md`)  
**Applies To**: Services with multi-tenancy enabled that need admin/global endpoints

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Authorization on BypassTenant Endpoints](#authorization-on-bypasstenant-endpoints)
3. [Critical Concepts](#critical-concepts)
4. [Common Pitfalls](#common-pitfalls)
5. [Implementation Patterns](#implementation-patterns)
6. [Complete Example](#complete-example)
7. [Database Migration Strategy](#database-migration-strategy)
8. [Testing](#testing)

---

## Overview

### What Are BypassTenant Endpoints?

**BypassTenant endpoints** are API endpoints that:

- ✅ Work **without** the `x-tenant-id` header
- ✅ Accessible by **global users** (SuperAdmin, Service roles)
- ✅ Use **global JWT secret** for authentication
- ✅ Can optionally accept `tenantId` as query parameter
- ✅ Bypass tenant resolution middleware

### When to Use BypassTenant Endpoints

Use `BypassTenantAttribute` when:

- ✅ **Cross-tenant operations**: Admin needs to manage multiple tenants
- ✅ **System-level operations**: Background jobs, cleanup tasks
- ✅ **Service-to-service calls**: Internal microservice communication
- ✅ **Optional tenant context**: Endpoint works with or without tenant

### When NOT to Use

❌ **Regular user operations**: Users accessing their own tenant data  
❌ **Tenant-specific business logic**: Operations that should be isolated per tenant  
❌ **Public endpoints**: Use `.AllowAnonymous()` instead

---

## Authorization on BypassTenant Endpoints

> **Critical rule: BypassTenant ≠ Anonymous.** Bypassing tenant resolution never removes the authorization requirement. Every `[BypassTenant]` endpoint in this system is protected — the bypass only removes the `x-tenant-id` header requirement, not the security layer.

### Two Authorization Patterns Used in This System

#### Pattern A — Role-Based (Admin / SuperAdmin)

Used for human admin operations (Category admin tree, FileManager admin delete, Notification send). The caller must present a **global JWT** (issued without a tenant scope) with the appropriate role.

```csharp
app.MapGet("/api/v1/admin/categories/tree", handler)
    .WithMetadata(new BypassTenantAttribute())
    .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));
```

The global JWT is issued by Identity Service at a non-tenant endpoint (no `x-tenant-id` header). It contains a `Role` claim of `SuperAdmin` or `Admin` and **no** `tenant_id` claim.

#### Pattern B — Service-to-Service (X-Service-Secret)

Used for internal microservice calls (FileManager internal batch endpoints). No user JWT is involved — authentication is done by `ServiceAuthenticationMiddleware` checking the `X-Service-Secret` header and an `IsInternalService` claim.

```csharp
// Internal S2S endpoint — no user auth, uses service secret
app.MapGet("/api/filemanager/internal/files/{id}", handler)
    .WithMetadata(new BypassTenantAttribute())
    .AllowAnonymous();  // JWT bypassed; ServiceAuthenticationMiddleware validates the secret instead
```

The calling service must include the shared secret header:
```http
GET /api/filemanager/internal/files/123
X-Service-Secret: <shared-secret-from-config>
X-Service-Name: IdentityService
```

### Quick Reference: Which Pattern to Use

| Endpoint type | Who calls it | Authorization pattern |
|---|---|---|
| Admin cross-tenant data management | Human SuperAdmin via frontend | Pattern A — `RequireRole("SuperAdmin")` + global JWT |
| System-wide operations (send notification, cleanup) | Human SuperAdmin or service | Pattern A — `RequireRole("Service", "SuperAdmin")` |
| Internal service calls (file lookup, batch fetch) | Other microservice | Pattern B — `AllowAnonymous()` + `X-Service-Secret` header |

### Common Mistake: Leaving BypassTenant Endpoints Unprotected

```csharp
// ❌ WRONG — Bypass without any authorization
app.MapDelete("/api/admin/cleanup", handler)
    .WithMetadata(new BypassTenantAttribute());

// ✅ CORRECT — Always add role requirement or rely on ServiceAuthenticationMiddleware
app.MapDelete("/api/admin/cleanup", handler)
    .WithMetadata(new BypassTenantAttribute())
    .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"));
```

---

## Critical Concepts

### 1. JWT Mode Configuration

**MUST be consistent across ALL services:**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant" // ⚠️ CRITICAL: Must match Identity Service
  }
}
```

#### JwtMode Options

| Mode        | Description                      | Use Case                              |
| ----------- | -------------------------------- | ------------------------------------- |
| `Shared`    | All services use same JWT secret | Simple deployments, superadmin access |
| `PerTenant` | Each tenant has own JWT secret   | Production, tenant isolation          |

**⚠️ PITFALL**: If Identity Service uses `JwtMode: "PerTenant"` but your service uses `"Shared"`, tenant users get **401 Unauthorized**!

### 2. JWT Validation Pattern (Corrected — July 2026)

**CRITICAL**: Do NOT mutate `JwtBearerOptions.TokenValidationParameters` from inside `OnMessageReceived` (or any other `JwtBearerEvents` handler) to implement per-tenant validation. That object is a **single instance shared by every concurrent request** — an earlier version of this codebase did exactly this (fetching tenant config via `ITenantConfigurationProvider.GetTenantConfigurationAsync(...).GetAwaiter().GetResult()` inside `OnMessageReceived` and assigning the result to `context.Options.TokenValidationParameters`), and under concurrent load it caused a real race: one request's validation could run against a different, concurrently in-flight request's freshly-overwritten parameters, intermittently rejecting valid tokens with `"signature key was not found"`. Found via k6 load testing — see `LOAD_TESTING_GUIDE.md` and `MULTI_TENANCY_GUIDE.md`'s Troubleshooting section for the full writeup.

**Don't hand-roll this at all** — every service calls the shared `AddJwtAuthentication()` extension (`IhsanDev.Shared.Infrastructure/Extensions/JwtAuthenticationExtensions.cs`), which already implements per-tenant JWT support correctly for both `Shared` and `PerTenant` modes:

```csharp
// Program.cs — this is the ENTIRE JWT setup needed; PerTenant vs Shared is
// read automatically from MultiTenancy:JwtMode
builder.Services.AddJwtAuthentication(builder.Configuration);
```

**How it actually resolves the correct key per request** (no shared mutable state involved): `AddJwtAuthentication` sets `TokenValidationParameters.IssuerSigningKeyResolver` / `IssuerValidator` / `AudienceValidator` — pure, stateless, per-validation callbacks registered **once at startup** via `services.AddOptions<JwtBearerOptions>(scheme).Configure<IHttpContextAccessor>(...)`, not inside an event handler. Each callback reads `ITenantContext.CurrentTenant` off the *current* request's `HttpContext` (via `IHttpContextAccessor`, which is `AsyncLocal`-backed so every concurrent request gets its own correct instance) and returns candidate keys/issuers/audiences (tenant-specific + global fallback). By the time this runs, `ITenantContext.CurrentTenant` is already populated — `UseTenantResolution` runs earlier in the same request's pipeline, before `UseAuthentication` — so there's no extra fetch and no blocking call either.

**Why this matters for BypassTenant endpoints specifically**: when there's no `x-tenant-id` header (or `[BypassTenant]` skips tenant resolution), `ITenantContext.CurrentTenant` is simply `null` for that request, so the resolver/validators fall back to the global key/issuer/audience automatically — exactly the behavior a global SuperAdmin/Service JWT needs, with no special-casing required in application code.

### 3. DbContext Fallback Pattern

**CRITICAL**: DbContext MUST fall back to global database when no tenant context:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    if (optionsBuilder.IsConfigured) return;

    var multiTenancyEnabled = _configuration?.GetValue<bool>("MultiTenancy:Enabled") ?? false;

    if (multiTenancyEnabled)
    {
        // ✅ CORRECT - Fall back to global DB if no tenant context
        if (_tenantContext?.HasTenant != true ||
            _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
        {
            // Use global database from appsettings.json as fallback
            connectionString = _configuration["DatabaseSettings:ConnectionString"];
            provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
        }
        else
        {
            // Use tenant-specific database
            connectionString = _tenantContext.CurrentTenant.Configuration
                .DatabaseSettings.ConnectionString;
            provider = _tenantContext.CurrentTenant.Configuration
                .DatabaseSettings.Provider ?? "PostgreSql";
        }
    }
    else
    {
        // Multi-tenancy disabled - always use global database
        connectionString = _configuration["DatabaseSettings:ConnectionString"];
        provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
    }

    // Configure provider...
}
```

**❌ WRONG - Throws exception when no tenant:**

```csharp
if (multiTenancyEnabled)
{
    if (_tenantContext?.HasTenant != true)
    {
        throw new InvalidOperationException(
            "Tenant context required!"); // ❌ Breaks admin endpoints!
    }
}
```

---

## Common Pitfalls

### ❌ Pitfall 1: Mismatched JwtMode Configuration

**Symptom**: Tenant users get 401 Unauthorized

**Cause**: Identity Service generates tokens with `JwtMode: "PerTenant"` but your service expects `"Shared"`

**Solution**: Ensure ALL services use same `MultiTenancy:JwtMode`:

```json
// Identity Service appsettings.json
{
  "MultiTenancy": { "JwtMode": "PerTenant" }
}

// Your Service appsettings.json
{
  "MultiTenancy": { "JwtMode": "PerTenant" } // ✅ Must match!
}
```

### ❌ Pitfall 2: DbContext Throws Without Tenant Context

**Symptom**: `400 Bad Request - Tenant context required` on admin endpoints

**Cause**: DbContext throws exception when `_tenantContext.HasTenant` is false

**Solution**: Add fallback to global database (see [DbContext Fallback Pattern](#3-dbcontext-fallback-pattern))

### ❌ Pitfall 3: Missing Global Database Migration

**Symptom**: `42P01: relation "TableName" does not exist` on admin endpoints without tenantId

**Cause**: Only tenant-specific migration runs; global database never migrated

**Solution**: Run BOTH migrations in Program.cs:

```csharp
// ✅ CORRECT - Run both migrations
app.UseDefaultDatabaseMigration<YourDbContext>(); // Global DB

if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<YourDbContext>(builder.Configuration); // Tenant DBs
}
```

**❌ WRONG - Only one migration:**

```csharp
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<YourDbContext>(...); // ❌ Global DB never migrated!
}
else
{
    app.UseDefaultDatabaseMigration<YourDbContext>();
}
```

### ❌ Pitfall 4: Required tenantId Parameter

**Symptom**: Admin can't use endpoint without specifying tenant

**Cause**: Endpoint validation requires `tenantId` even when not needed

**Solution**: Make `tenantId` optional and only set context if provided:

```csharp
// ✅ CORRECT - Optional tenantId
adminGroup.MapPost("/files", async (
    [FromQuery] string? tenantId, // ✅ Optional
    ITenantContext tenantContext,
    ITenantConfigurationProvider tenantConfigProvider,
    // ... other params
) =>
{
    // Only set tenant context if tenantId provided
    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        var tenantInfo = await tenantConfigProvider
            .GetTenantConfigurationAsync(tenantId, ct);

        if (tenantInfo == null)
            return Results.NotFound(new { error = "Tenant not found" });

        tenantContext.SetTenant(tenantInfo); // Manually set context
    }
    // else: No tenant context, uses global database

    // Handler executes with or without tenant context
});
```

---

## Implementation Patterns

### Pattern 1: Optional Tenant Context (Recommended)

Use when admin endpoint should work with OR without tenant:

```csharp
var adminGroup = app.MapGroup("/api/v1/admin")
    .WithTags("Admin")
    .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"));

// Admin can upload to specific tenant OR global database
adminGroup.MapPost("/files", async (
    [FromForm] IFormFile file,
    [FromQuery] string? tenantId, // ✅ Optional
    ITenantContext tenantContext,
    ITenantConfigurationProvider tenantConfigProvider,
    IMediator mediator,
    CancellationToken ct) =>
{
    // If tenantId provided, set tenant context
    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        var tenantInfo = await tenantConfigProvider
            .GetTenantConfigurationAsync(tenantId, ct);

        if (tenantInfo == null)
            return Results.NotFound(new { error = $"Tenant '{tenantId}' not found" });

        tenantContext.SetTenant(tenantInfo);
    }
    // else: Uses global database fallback

    var command = new SaveFileCommand(file);
    var result = await mediator.Send(command, ct);
    return Results.Created($"/api/v1/admin/files/{result.Id}", result);
})
.WithMetadata(new BypassTenantAttribute())
.WithName("SaveFileAdmin");
```

**Usage:**

```http
# Upload to specific tenant
POST /api/v1/admin/files?tenantId=ihsandev
Authorization: Bearer <global-jwt>

# Upload to global database
POST /api/v1/admin/files
Authorization: Bearer <global-jwt>
```

### Pattern 2: Cross-Tenant Operations

Use for background jobs or admin operations across multiple tenants:

```csharp
adminGroup.MapDelete("/cleanup/temp-files", async (
    ITenantConfigurationProvider tenantConfigProvider,
    IMediator mediator,
    CancellationToken ct) =>
{
    // Get all tenants
    var tenants = await tenantConfigProvider.GetAllTenantsAsync(ct);

    var results = new List<object>();

    // Process each tenant
    foreach (var tenant in tenants)
    {
        using var scope = serviceProvider.CreateScope();
        var tenantContext = scope.ServiceProvider
            .GetRequiredService<ITenantContext>();

        // Set tenant context for this iteration
        tenantContext.SetTenant(tenant);

        var command = new DeleteTempFilesCommand();
        var deleted = await mediator.Send(command, ct);

        results.Add(new { TenantId = tenant.TenantId, DeletedCount = deleted });
    }

    return Results.Ok(results);
})
.WithMetadata(new BypassTenantAttribute())
.RequireAuthorization(policy => policy.RequireRole("Service"));
```

### Pattern 3: Service-to-Service Communication

Use for internal microservice calls:

```csharp
// Service-to-service endpoint (no JWT, uses shared secret)
adminGroup.MapPost("/internal/process", async (
    [FromBody] ProcessRequest request,
    IMediator mediator) =>
{
    var command = new ProcessCommand(request);
    var result = await mediator.Send(command);
    return Results.Ok(result);
})
.WithMetadata(new BypassTenantAttribute()); // No user authentication
```

**Client side (calling service):**

```csharp
builder.Services.AddHttpClient("YourService", client =>
{
    client.BaseAddress = new Uri("https://yourservice");

    var serviceSecret = builder.Configuration["ServiceCommunication:SharedSecret"];
    client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
    client.DefaultRequestHeaders.Add("X-Service-Name", "CallingService");
});
```

---

## Complete Example

### FileManager Service with BypassTenant Endpoints

This example shows the complete implementation of a service with both tenant and admin endpoints:

#### 1. appsettings.json

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=global;..."
  },
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant",
    "TenantServiceUrl": "http://localhost:5002"
  },
  "Jwt": {
    "Secret": "CHANGE_ME_JWT_SECRET",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp"
  }
}
```

#### 2. Program.cs - Authentication Setup

Don't hand-roll `AddAuthentication`/`AddJwtBearer` per service — call the shared extension, which reads `MultiTenancy:JwtMode` automatically and handles both `Shared` and `PerTenant` modes internally:

```csharp
builder.Services.AddJwtAuthentication(builder.Configuration);
```

That's the entire setup. See `IhsanDev.Shared.Infrastructure/Extensions/JwtAuthenticationExtensions.cs` for the implementation — per-tenant key/issuer/audience resolution is done via `TokenValidationParameters.IssuerSigningKeyResolver`/`IssuerValidator`/`AudienceValidator` (stateless, per-validation callbacks reading `ITenantContext` through `IHttpContextAccessor`), **not** by mutating `context.Options.TokenValidationParameters` inside a `JwtBearerEvents` handler — see the "JWT Validation Pattern" section above for why that distinction matters. Global fallback (no tenant header, `[BypassTenant]` endpoints) is handled automatically: when `ITenantContext.CurrentTenant` is `null`, the resolver/validators fall back to the global key/issuer/audience with no extra code needed.

#### 3. Program.cs - Database Migration Setup

```csharp
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");

// ✅ CRITICAL: Always migrate global database first
app.UseDefaultDatabaseMigration<FileManagerDbContext>();

// Then enable tenant-specific migrations if multi-tenancy enabled
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<FileManagerDbContext>(builder.Configuration);
}
```

#### 4. DbContext with Fallback

```csharp
public class FileManagerDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<FileManagerDbContext>? _logger;

    public FileManagerDbContext(
        DbContextOptions<FileManagerDbContext> options,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<FileManagerDbContext>? logger = null)
        : base(options)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        string? connectionString = null;
        string? provider = null;
        var multiTenancyEnabled = _configuration?
            .GetValue<bool>("MultiTenancy:Enabled") ?? false;

        if (multiTenancyEnabled)
        {
            // ✅ CRITICAL: Fall back to global DB if no tenant context
            if (_tenantContext?.HasTenant != true ||
                _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
            {
                _logger?.LogDebug(
                    "No tenant context - using global database");

                connectionString = _configuration["DatabaseSettings:ConnectionString"];
                provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
            }
            else
            {
                var tenantDb = _tenantContext.CurrentTenant.Configuration.DatabaseSettings;
                connectionString = tenantDb.ConnectionString;
                provider = tenantDb.Provider ?? "PostgreSql";

                _logger?.LogInformation(
                    "Using tenant database: {TenantId}",
                    _tenantContext.CurrentTenant.TenantId);
            }
        }
        else
        {
            connectionString = _configuration["DatabaseSettings:ConnectionString"];
            provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
        }

        // Configure provider
        switch (provider)
        {
            case "PostgreSql":
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(
                        typeof(FileManagerDbContext).Assembly.GetName().Name);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
                break;
            // ... other providers
        }

        base.OnConfiguring(optionsBuilder);
    }
}
```

#### 5. Endpoints with BypassTenant

```csharp
public static class FileManagerEndpoints
{
    public static IEndpointRouteBuilder MapFileManagerEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Tenant user endpoints (require x-tenant-id header)
        var tenantGroup = app.MapGroup("/api/v1/filemanager")
            .WithTags("FileManager");

        tenantGroup.MapPost("/files", async (
            [FromForm] IFormFile file,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new SaveFileCommand(file);
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/v1/filemanager/files/{result.Id}", result);
        })
        .RequireAuthorization(policy =>
            policy.RequireRole("User", "Admin", "SuperAdmin"))
        .WithName("SaveFile");

        // Admin endpoints (optional tenant context)
        var adminGroup = app.MapGroup("/api/v1/filemanager/admin")
            .WithTags("FileManager - Admin");

        adminGroup.MapPost("/files", async (
            [FromForm] IFormFile file,
            [FromQuery] string? tenantId, // ✅ Optional
            ITenantContext tenantContext,
            ITenantConfigurationProvider tenantConfigProvider,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // If tenantId provided, set tenant context
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                var tenantInfo = await tenantConfigProvider
                    .GetTenantConfigurationAsync(tenantId, ct);

                if (tenantInfo == null)
                    return Results.NotFound(new {
                        error = $"Tenant '{tenantId}' not found"
                    });

                tenantContext.SetTenant(tenantInfo);
            }
            // else: No tenant context, uses global database

            var command = new SaveFileCommand(file);
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/v1/filemanager/admin/files/{result.Id}", result);
        })
        .RequireAuthorization(policy =>
            policy.RequireRole("Service", "SuperAdmin"))
        .WithMetadata(new BypassTenantAttribute())
        .WithName("SaveFileAdmin");

        return app;
    }
}
```

---

## Database Migration Strategy

### Why Both Migrations Are Needed

When multi-tenancy is enabled AND you have BypassTenant endpoints:

1. **Global database**: Used by admin endpoints without `tenantId` parameter
2. **Tenant databases**: Used by tenant-specific endpoints or admin endpoints with `tenantId`

### Correct Migration Setup

```csharp
// ✅ CORRECT - Dual migration strategy
app.UseDefaultDatabaseMigration<YourDbContext>(); // Global DB (always)

if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<YourDbContext>(config); // Tenant DBs (per-request)
}
```

**How it works:**

- **Global migration**: Runs once on startup, creates tables in global database
- **Tenant migration**: Runs per-tenant on first request, creates tables in each tenant's database

### When You DON'T Need Both

**Only use tenant migration if:**

- ✅ Service has NO BypassTenant endpoints
- ✅ ALL endpoints require tenant context
- ✅ No admin/global operations needed

Example: Identity Service (all operations are tenant-specific)

```csharp
// Identity Service - No global operations
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<IdentityDbContext>(config);
}
else
{
    app.UseDefaultDatabaseMigration<IdentityDbContext>();
}
```

---

## Testing

### Integration Test with BypassTenant Endpoint

```csharp
public class AdminEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdminEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Add global SuperAdmin JWT
        var token = GenerateGlobalSuperAdminToken();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task AdminEndpoint_WithoutTenantId_UsesGlobalDatabase()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        content.Add(fileContent, "file", "test.txt");

        // Act - No tenantId parameter
        var response = await _client.PostAsync("/api/v1/filemanager/admin/files", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify saved to global database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FileManagerDbContext>();
        var file = await dbContext.FileManager.FirstOrDefaultAsync();
        Assert.NotNull(file);
    }

    [Fact]
    public async Task AdminEndpoint_WithTenantId_UsesTenantDatabase()
    {
        // Arrange
        var tenantId = "test-tenant";
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        content.Add(fileContent, "file", "test.txt");

        // Act - With tenantId parameter
        var response = await _client.PostAsync(
            $"/api/v1/filemanager/admin/files?tenantId={tenantId}",
            content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private string GenerateGlobalSuperAdminToken()
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var secret = config["Jwt:Secret"];
        var issuer = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Email, "admin@system.com"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
            // Note: No tenant_id claim = global user
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

---

## Checklist: Before Creating BypassTenant Endpoints

Use this checklist when implementing admin/global endpoints:

### Configuration

- [ ] `MultiTenancy:JwtMode` matches Identity Service
- [ ] `Jwt` section exists in appsettings.json
- [ ] Global database connection string configured
- [ ] Service-to-service secret configured (if needed)

### Authentication

- [ ] `builder.Services.AddJwtAuthentication(builder.Configuration)` called in Program.cs — do NOT hand-roll `AddJwtBearer`/`JwtBearerEvents` per service
- [ ] `MultiTenancy:JwtMode` set correctly (`Shared` or `PerTenant`) and matches Identity Service
- [ ] No custom code mutates `context.Options.TokenValidationParameters` from inside a `JwtBearerEvents` handler (shared singleton — see "JWT Validation Pattern" above)

### Database

- [ ] DbContext falls back to global database when no tenant context
- [ ] Both `UseDefaultDatabaseMigration` AND `UseTenantDatabaseMigration` registered
- [ ] Global migration runs on startup
- [ ] Tenant migration runs per-request

### Endpoints

- [ ] Admin group created with `.RequireRole("Service", "SuperAdmin")`
- [ ] `BypassTenantAttribute` added to admin endpoints
- [ ] `tenantId` parameter is optional (`string?`)
- [ ] Tenant context manually set only if `tenantId` provided
- [ ] Endpoint works without `x-tenant-id` header

### Testing

- [ ] Integration test without tenantId (global database)
- [ ] Integration test with tenantId (tenant database)
- [ ] JWT token generation tested for global users
- [ ] Database migration verified for both scenarios

---

## Related Documentation

- [Multi-Tenancy Guide](MULTI_TENANCY_GUIDE.md) - Overview of multi-tenant system, JWT modes, and middleware pipeline
- [New Service Integration Guide](NEW_SERVICE_INTEGRATION_GUIDE.md) - Creating new services
- [Shared Identity Service Guide](SHARED_IDENTITY_SERVICE_GUIDE.md) - JWT authentication, roles, and how global tokens are issued
- [Database Per Tenant Architecture](DATABASE_PER_TENANT_ARCHITECTURE.md) - Database isolation
- [Service-to-Service Authentication Guide](SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md) - X-Service-Secret pattern for internal endpoints

---

**Questions or Issues?**  
Check the troubleshooting section or review existing service implementations (FileManager, Notification) for working examples.
