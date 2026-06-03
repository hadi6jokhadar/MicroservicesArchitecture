# Platform Capabilities Roadmap

**Status:** Planning  
**Created:** June 3, 2026  
**Purpose:** Actionable implementation guide for missing platform capabilities, organized by priority tier.  
**Stack:** .NET 8 Minimal APIs · PostgreSQL · Redis · Clean Architecture · CQRS · MediatR

> This document is a companion to the analysis in `DOCUMENTATION_INDEX.md`. Each item below is a gap in the current platform. Work through tiers in order — Tier 1 items are load-bearing; Tier 2 and 3 build on top of them.

---

## 📋 Progress Tracker

| # | Capability | Tier | Status |
|---|---|---|---|
| 1 | API Gateway | 1 | ⬜ Not started |
| 2 | Distributed Tracing & Observability | 1 | ⬜ Not started |
| 3 | Secrets Management | 1 | ⬜ Not started |
| 4 | Circuit Breaker / Resilience Patterns | 1 | ⬜ Not started |
| 5 | Audit Logging Service | 1 | ⬜ Not started |
| 6 | Background Job / Scheduling Service | 2 | ⬜ Not started |
| 7 | API Versioning Standard | 2 | ⬜ Not started |
| 8 | Feature Flags Service | 2 | ⬜ Not started |
| 9 | Database Backup & Recovery | 2 | ⬜ Not started |
| 10 | Search Service | 3 | ⬜ Not started |
| 11 | CDN / Media Delivery | 3 | ⬜ Not started |
| 12 | Usage Metering / Billing Hooks | 3 | ⬜ Not started |

---

---

# 🔴 Tier 1 — Must-Have

> These are load-bearing gaps. A production outage without them is hard to diagnose, hard to contain, and hard to recover from.

---

## 1. API Gateway

### Why It's Needed

Every service currently exposes its own port directly. A frontend must know 8 different base URLs. There is no centralized rate limiting, no single SSL termination point, and no place to inject cross-cutting headers (correlation IDs, auth pre-checks) before requests reach services.

### Recommended Approach: YARP (Yet Another Reverse Proxy)

YARP is a Microsoft-maintained .NET reverse proxy — the best fit for a .NET-native stack. It runs as a standard .NET 8 Minimal API project and supports hot-reload routing config.

### What to Build

**New project:** `src/Gateway/Gateway.API/`

#### 1. Project setup

```powershell
dotnet new web -n Gateway.API -o src/Gateway/Gateway.API
dotnet add src/Gateway/Gateway.API/Gateway.API.csproj package Yarp.ReverseProxy
dotnet sln add src/Gateway/Gateway.API/Gateway.API.csproj
```

#### 2. Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("global", o =>
    {
        o.PermitLimit = 10_000;
        o.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

// Inject correlation ID on every inbound request
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Headers.ContainsKey("X-Correlation-Id"))
        ctx.Request.Headers.Append("X-Correlation-Id", Guid.NewGuid().ToString());
    await next();
});

app.UseRateLimiter();
app.MapReverseProxy();
app.Run();
```

#### 3. appsettings.json routing table

```json
{
  "ReverseProxy": {
    "Routes": {
      "identity-route":      { "ClusterId": "identity",      "Match": { "Path": "/api/auth/{**catch-all}" } },
      "tenant-route":        { "ClusterId": "tenant",        "Match": { "Path": "/api/tenant/{**catch-all}" } },
      "filemanager-route":   { "ClusterId": "filemanager",   "Match": { "Path": "/api/filemanager/{**catch-all}" } },
      "notification-route":  { "ClusterId": "notification",  "Match": { "Path": "/api/notifications/{**catch-all}" } },
      "translation-route":   { "ClusterId": "translation",   "Match": { "Path": "/api/translations/{**catch-all}" } },
      "category-route":      { "ClusterId": "category",      "Match": { "Path": "/api/categories/{**catch-all}" } },
      "nasheed-route":       { "ClusterId": "nasheed",       "Match": { "Path": "/api/nasheed/{**catch-all}" } }
    },
    "Clusters": {
      "identity":     { "Destinations": { "d1": { "Address": "https://localhost:5001" } } },
      "tenant":       { "Destinations": { "d1": { "Address": "https://localhost:5002" } } },
      "filemanager":  { "Destinations": { "d1": { "Address": "https://localhost:5005" } } },
      "notification": { "Destinations": { "d1": { "Address": "https://localhost:5004" } } },
      "translation":  { "Destinations": { "d1": { "Address": "https://localhost:5006" } } },
      "category":     { "Destinations": { "d1": { "Address": "https://localhost:5007" } } },
      "nasheed":      { "Destinations": { "d1": { "Address": "https://localhost:5009" } } }
    }
  }
}
```

#### 4. Services affected by this change

- **All frontends:** Update base URL to gateway port only (e.g. `https://localhost:5000`)
- **All services:** No code changes needed — they stay on their own ports; gateway proxies to them
- **CORS:** Each service's `UseTenantAwareCors` or `UseCors` still controls allowed origins

### Implementation Checklist

- [ ] Create `src/Gateway/Gateway.API/` project
- [ ] Install `Yarp.ReverseProxy` NuGet package
- [ ] Configure routing table in appsettings.json for all 7 services
- [ ] Add correlation ID injection middleware
- [ ] Add rate limiter (global + per-IP)
- [ ] Add to solution file
- [ ] Update all frontend API base URLs
- [ ] Add `/health` endpoint that aggregates downstream health checks

---

## 2. Distributed Tracing & Observability

### Why It's Needed

When a request touches Identity → FileManager → Notification, there is no way to trace it end-to-end in production today. A single slow query in the wrong service causes a timeout that surfaces in the gateway with no actionable information.

### Recommended Approach: OpenTelemetry + structured logging

Use **OpenTelemetry** (vendor-neutral) with export to **Jaeger** (local/dev) or **Azure Application Insights** (production). This fits cleanly onto the existing Serilog logging setup.

### What to Build

#### 1. NuGet packages (add to every .NET service)

```powershell
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add package OpenTelemetry.Exporter.Jaeger        # local dev
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol  # production
```

#### 2. Shared registration extension (add to `IhsanDev.Shared.Infrastructure`)

```csharp
// IhsanDev.Shared.Infrastructure/Extensions/ObservabilityExtensions.cs
public static class ObservabilityExtensions
{
    public static IServiceCollection AddPlatformObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName))
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true);

                var jaegerEndpoint = configuration["Observability:JaegerEndpoint"];
                if (!string.IsNullOrEmpty(jaegerEndpoint))
                    tracing.AddJaegerExporter(o => o.AgentHost = jaegerEndpoint);
            });

        return services;
    }
}
```

#### 3. Call in every service's Program.cs

```csharp
builder.Services.AddPlatformObservability(builder.Configuration, "IdentityService");
```

#### 4. Health check endpoints (add to every service)

```csharp
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString, name: "database")
    .AddRedis(redisConnectionString, name: "redis");

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

#### 5. Correlation ID propagation

The gateway injects `X-Correlation-Id`. Each service must read it and include it in logs:

```csharp
// Middleware in Shared.Infrastructure
app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                        ?? Guid.NewGuid().ToString();
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        ctx.Response.Headers.Append("X-Correlation-Id", correlationId);
        await next();
    }
});
```

### Implementation Checklist

- [ ] Add `AddPlatformObservability` extension to `IhsanDev.Shared.Infrastructure`
- [ ] Register in all 7 .NET services' `Program.cs`
- [ ] Add `/health` and `/health/ready` endpoints to all services
- [ ] Add correlation ID middleware to shared infrastructure
- [ ] Stand up local Jaeger container (`docker run -p 16686:16686 jaegertracing/all-in-one`)
- [ ] Wire gateway `/health` to aggregate all downstream `/health` endpoints
- [ ] Verify traces appear in Jaeger UI for a cross-service request

---

## 3. Secrets Management

### Why It's Needed

JWT secrets, database passwords, and the `X-Service-Secret` are currently stored in `appsettings.json` files. Any developer with repo access can read production credentials. Secret rotation requires redeployment.

### Recommended Approach: .NET User Secrets (dev) + Azure Key Vault (production)

.NET already has a secrets abstraction (`IConfiguration`) — the change is only in the provider, not in how services consume secrets.

### What to Build

#### 1. Development — User Secrets (per service, one-time setup)

```powershell
# Run from each service's API project directory
dotnet user-secrets init
dotnet user-secrets set "DatabaseSettings:ConnectionString" "Host=localhost;..."
dotnet user-secrets set "Jwt:Secret" "your-dev-secret"
dotnet user-secrets set "ServiceCommunication:SharedSecret" "dev-service-secret"
```

No code change needed — `IConfiguration` picks up user secrets automatically in Development environment.

#### 2. Production — Azure Key Vault

```powershell
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
```

```csharp
// In Program.cs of every service (add BEFORE builder.Build())
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUri = builder.Configuration["KeyVault:Uri"]
        ?? throw new InvalidOperationException("KeyVault:Uri not configured");

    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}
```

Key Vault naming convention (Key Vault uses `--` as separator for nested keys):

```
DatabaseSettings--ConnectionString  →  appsettings: DatabaseSettings:ConnectionString
Jwt--Secret                         →  appsettings: Jwt:Secret
ServiceCommunication--SharedSecret  →  appsettings: ServiceCommunication:SharedSecret
MultiTenancy--TenantServiceUrl      →  appsettings: MultiTenancy:TenantServiceUrl
```

#### 3. What to remove from appsettings.json (move to secrets)

For every service, move these out of appsettings.json:

```json
// REMOVE from appsettings.json — move to Key Vault / user secrets:
"Jwt": { "Secret": "..." },
"DatabaseSettings": { "ConnectionString": "..." },
"ServiceCommunication": { "SharedSecret": "..." },
"Redis": { "ConnectionString": "..." }
```

Leave non-sensitive config in appsettings.json:

```json
// KEEP in appsettings.json (not sensitive):
"MultiTenancy": { "Enabled": true, "TenantServiceUrl": "https://..." },
"Jwt": { "Issuer": "...", "Audience": "..." },
"DatabaseSettings": { "Provider": "PostgreSql" }
```

### Implementation Checklist

- [ ] Run `dotnet user-secrets init` + set dev secrets for all 7 .NET services
- [ ] Create Azure Key Vault resource (or equivalent)
- [ ] Add `Azure.Extensions.AspNetCore.Configuration.Secrets` to all services
- [ ] Add Key Vault registration to each service's `Program.cs` (production only)
- [ ] Remove sensitive values from `appsettings.json` (replace with placeholder comments)
- [ ] Add `.gitignore` entry for `secrets.json` files
- [ ] Document Key Vault naming convention in `SHARED_IDENTITY_SERVICE_GUIDE.md`

---

## 4. Circuit Breaker / Resilience Patterns

### Why It's Needed

`SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md` registers typed HTTP clients with no retry or circuit breaker logic. When the Notification Service is slow, any service that calls it (Identity, FileManager) also becomes slow — cascading failures take down the entire platform.

### Recommended Approach: Microsoft.Extensions.Resilience (built on Polly v8)

.NET 8 ships `Microsoft.Extensions.Resilience` as a first-class package. It integrates directly with `IHttpClientFactory`.

### What to Build

#### 1. NuGet packages (add to `IhsanDev.Shared.Infrastructure`)

```powershell
dotnet add package Microsoft.Extensions.Http.Resilience
```

#### 2. Update `SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.cs`

Locate the existing extension methods that register HTTP clients and add the resilience pipeline:

```csharp
// Before (current state — no resilience):
services.AddHttpClient<INotificationServiceClient, NotificationServiceClient>(client =>
{
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
});

// After (with resilience pipeline):
services.AddHttpClient<INotificationServiceClient, NotificationServiceClient>(client =>
{
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
})
.AddStandardResilienceHandler(options =>
{
    // Retry: 3 attempts, exponential back-off
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromMilliseconds(200);
    options.Retry.BackoffType = DelayBackoffType.Exponential;

    // Circuit breaker: open after 5 failures in 30s, stay open for 15s
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

    // Overall timeout per attempt
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

    // Total timeout across all retries
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
});
```

#### 3. Apply to all service clients

Services that use HTTP clients for inter-service calls:

| Calling Service | Calls | Priority |
|---|---|---|
| Identity | Notification, FileManager | High |
| FileManager | Notification, Tenant | High |
| Nasheed | FileManager, AI | High |
| Notification | Identity, Tenant | Medium |
| Category | Tenant | Medium |

#### 4. Handle open circuit in handlers

```csharp
// In MediatR handlers that call external services:
try
{
    await _notificationClient.SendAsync(notification, ct);
}
catch (BrokenCircuitException ex)
{
    // Log and continue — notification failure should not fail the main operation
    _logger.LogWarning(ex, "Notification circuit open; notification skipped");
}
```

### Implementation Checklist

- [ ] Add `Microsoft.Extensions.Http.Resilience` to `IhsanDev.Shared.Infrastructure`
- [ ] Update `SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.cs` with `.AddStandardResilienceHandler()`
- [ ] Apply to all 5 inter-service HTTP clients
- [ ] Wrap notification/non-critical calls in `catch (BrokenCircuitException)` in handlers
- [ ] Add resilience pipeline to the AI service HTTP client in Nasheed
- [ ] Verify circuit opens correctly under simulated failure

---

## 5. Audit Logging Service

### Why It's Needed

Admin operations (role changes, tenant creation, file deletion, bypass-tenant endpoints) currently leave no immutable audit trail. This is required for GDPR, SOC2, and any internal compliance review. It is also the only way to answer "who deleted that file?" after the fact.

### Recommended Approach: Dedicated Audit table per service (Strategy B pattern)

Rather than a separate audit microservice (which adds a synchronous dependency), each service writes audit rows to its own `audit_log` table inside the same transaction as the action. A background job or direct query can surface these logs through an admin API.

### What to Build

#### 1. Shared Audit entity (add to `IhsanDev.Shared.Kernel`)

```csharp
// IhsanDev.Shared.Kernel/Entities/AuditLogEntity.cs
public class AuditLogEntity
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;       // e.g. "Category.Deleted"
    public string EntityType { get; set; } = string.Empty;   // e.g. "CategoryEntity"
    public string? EntityId { get; set; }                     // e.g. "42"
    public string? TenantId { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? Before { get; set; }                       // JSON snapshot before change
    public string? After { get; set; }                        // JSON snapshot after change
    public string? IpAddress { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
```

#### 2. Shared audit service interface (add to `IhsanDev.Shared.Application`)

```csharp
public interface IAuditService
{
    void Record(
        string action,
        string entityType,
        string? entityId = null,
        object? before = null,
        object? after = null);
}
```

#### 3. Implementation (add to `IhsanDev.Shared.Infrastructure`)

```csharp
public sealed class DbAuditService : IAuditService
{
    private readonly List<AuditLogEntity> _pending = new();
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantContext _tenantContext;

    public void Record(string action, string entityType, string? entityId = null,
                       object? before = null, object? after = null)
    {
        _pending.Add(new AuditLogEntity
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            TenantId = _tenantContext.TenantId,
            UserId = _currentUser.UserId,
            UserEmail = _currentUser.UserEmail,
            Before = before is null ? null : JsonSerializer.Serialize(before),
            After = after is null ? null : JsonSerializer.Serialize(after),
            OccurredAt = DateTimeOffset.UtcNow
        });
    }

    // Called by BaseDbContext.SaveChangesAsync — flush pending rows into the same transaction
    public IReadOnlyList<AuditLogEntity> Flush() => _pending.ToList();
}
```

#### 4. Add `DbSet<AuditLogEntity>` to each service's DbContext and flush in `SaveChangesAsync`

```csharp
// In BaseDbContext or each service's DbContext:
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var auditRows = _auditService.Flush();
    if (auditRows.Count > 0)
        AuditLogs.AddRange(auditRows);
    return await base.SaveChangesAsync(ct);
}
```

#### 5. Use in handlers (BypassTenant and admin operations)

```csharp
// In DeleteCategoryCommandHandler:
_auditService.Record(
    action: "Category.Deleted",
    entityType: nameof(CategoryEntity),
    entityId: entity.Id.ToString(),
    before: CategoryDto.MapFrom(entity));

await _eventPublisher.PublishAsync(entity, CategoryEventType.Deleted, tenantId, ct);
await _repository.DeleteAsync(entity, ct);
```

#### 6. Priority: which operations to audit first

| Service | Operations to audit |
|---|---|
| Identity | Login, logout, role assignment, password change |
| Tenant | Tenant create, update, delete |
| Category | Admin create, update, delete, move |
| FileManager | Admin delete, blob operations |
| Notification | Global send, archive |

### Implementation Checklist

- [ ] Add `AuditLogEntity` to `IhsanDev.Shared.Kernel`
- [ ] Add `IAuditService` to `IhsanDev.Shared.Application`
- [ ] Implement `DbAuditService` in `IhsanDev.Shared.Infrastructure`
- [ ] Register `IAuditService` as Scoped in shared DI registration
- [ ] Override `SaveChangesAsync` in `BaseDbContext` to flush audit rows
- [ ] Add `DbSet<AuditLogEntity>` and EF configuration to each service's DbContext
- [ ] Add `dotnet ef migrations add AddAuditLog` for each service
- [ ] Call `_auditService.Record(...)` in Identity login, role assignment, and Tenant admin handlers first
- [ ] Expand to Category, FileManager, and Notification admin handlers
- [ ] Add admin endpoint to query audit logs per tenant

---

---

# 🟡 Tier 2 — Strongly Recommended

> Build these after Tier 1 is in place. They significantly improve operational stability and developer velocity.

---

## 6. Background Job / Scheduling Service

### Why It's Needed

The `OutboxEventProcessorService` in Category is a `BackgroundService` with a polling loop. This pattern is repeated in Notification's cleanup service. There's no unified way to schedule periodic jobs, retry failed jobs, see job history, or trigger jobs on demand.

### Recommended Approach: Hangfire with PostgreSQL storage

Hangfire integrates cleanly with the existing PostgreSQL setup. It provides a dashboard, retry logic, and cron scheduling out of the box.

### What to Build

#### 1. NuGet packages (add to services that need scheduled jobs)

```powershell
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.PostgreSql
```

#### 2. Registration in Program.cs

```csharp
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(
        builder.Configuration["DatabaseSettings:ConnectionString"],
        new PostgreSqlStorageOptions { SchemaName = "hangfire" }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 5;
    options.Queues = ["critical", "default", "low"];
});
```

#### 3. Dashboard (admin only — wire behind SuperAdmin auth)

```csharp
app.UseHangfireDashboard("/admin/jobs", new DashboardOptions
{
    Authorization = [new HangfireRoleAuthorizationFilter("SuperAdmin")]
});
```

#### 4. Replace the existing OutboxEventProcessorService polling loop in Category

```csharp
// Register recurring job instead of BackgroundService
RecurringJob.AddOrUpdate<OutboxEventProcessorJob>(
    "category-outbox-processor",
    job => job.ProcessAsync(CancellationToken.None),
    Cron.Minutely);
```

#### 5. Jobs to create first

| Job | Service | Schedule | Queue |
|---|---|---|---|
| Outbox event processor | Category | Every 5 seconds | critical |
| Temp file cleanup | FileManager | Daily at 2am | low |
| Notification cleanup | Notification | Daily at 3am | low |
| Tenant cache refresh | Tenant | Every 30 minutes | default |

### Implementation Checklist

- [ ] Add Hangfire + Hangfire.PostgreSql to Category, FileManager, Notification
- [ ] Configure shared PostgreSQL storage (dedicated `hangfire` schema in global DB)
- [ ] Add Hangfire dashboard behind SuperAdmin route in Gateway or dedicated admin service
- [ ] Migrate Category's `OutboxEventProcessorService` to a Hangfire recurring job
- [ ] Migrate FileManager's temp cleanup to a Hangfire recurring job
- [ ] Migrate Notification's cleanup service to a Hangfire recurring job
- [ ] Add job retry policies (exponential back-off, max 5 retries)

---

## 7. API Versioning Standard

### Why It's Needed

As services evolve, endpoint signatures will change. Without a versioning policy, breaking changes force all clients to update simultaneously. A versioning standard allows old and new clients to coexist during transition periods.

### Recommended Approach: URL path versioning with `Asp.Versioning.Http`

```powershell
dotnet add package Asp.Versioning.Http
```

### What to Build

#### 1. Shared registration (add to `IhsanDev.Shared.Infrastructure`)

```csharp
public static IServiceCollection AddPlatformApiVersioning(this IServiceCollection services)
{
    services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
        options.ReportApiVersions = true; // adds api-supported-versions header
    });
    return services;
}
```

#### 2. Endpoint pattern

```csharp
// New endpoints MUST be versioned from the start:
var v1 = app.NewVersionedApi("Category");
var v1Group = v1.MapGroup("/api/v{version:apiVersion}/categories")
                .HasApiVersion(1);

v1Group.MapGet("/", GetCategoriesV1);

// When breaking change needed, add v2 alongside — don't remove v1 yet:
var v2Group = v1.MapGroup("/api/v{version:apiVersion}/categories")
                .HasApiVersion(2);
v2Group.MapGet("/", GetCategoriesV2);
```

#### 3. Deprecation policy

- A version is **deprecated** when a newer version exists AND a migration guide is written.
- A deprecated version is **removed** after a minimum 3-month notice period.
- Add `options.Policies.Sunset(new ApiVersion(1), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)))` when sunsetting.

### Implementation Checklist

- [ ] Add `Asp.Versioning.Http` to all .NET service projects
- [ ] Add `AddPlatformApiVersioning()` extension to `IhsanDev.Shared.Infrastructure`
- [ ] Call it in every service's Program.cs
- [ ] Rename all existing endpoints from `/api/...` to `/api/v1/...`
- [ ] Update gateway routing table to include the `v1` segment
- [ ] Update all frontend API calls and service-to-service clients to use `/api/v1/...`
- [ ] Document the deprecation policy in this file and in `DOCUMENTATION_GUIDELINES.md`

---

## 8. Feature Flags Service

### Why It's Needed

Currently any new feature ships to all tenants at once. There is no way to enable a new AI capability for one tenant to test it before a global rollout, or to gate a half-finished Nasheed feature behind a flag during development.

### Recommended Approach: Tenant-configuration-driven flags (no external dependency)

The Tenant Service already stores a JSON `data` blob per tenant. Feature flags can live inside that blob — no new service needed.

### What to Build

#### 1. Add `FeatureFlags` section to tenant configuration

```json
// In Tenant data payload (CreateTenantCommand / UpdateTenantCommand):
{
  "featureFlags": {
    "aiChatEnabled": true,
    "nasheedIngestionEnabled": false,
    "advancedSearchEnabled": false
  }
}
```

#### 2. Extend `TenantConfiguration` model (in `IhsanDev.Shared.Kernel`)

```csharp
public class TenantConfiguration
{
    public JwtSettings? Jwt { get; set; }
    public DatabaseSettings? DatabaseSettings { get; set; }
    public CorsSettings? Cors { get; set; }
    public OtpSettings? Otp { get; set; }
    public Dictionary<string, bool> FeatureFlags { get; set; } = new(); // ADD THIS
}
```

#### 3. Feature flag service (add to `IhsanDev.Shared.Application`)

```csharp
public interface IFeatureFlagService
{
    bool IsEnabled(string flagName, bool defaultValue = false);
}

public class TenantFeatureFlagService : IFeatureFlagService
{
    private readonly ITenantContext _tenantContext;

    public bool IsEnabled(string flagName, bool defaultValue = false)
    {
        var flags = _tenantContext.CurrentTenant?.Configuration?.FeatureFlags;
        return flags is not null && flags.TryGetValue(flagName, out var value)
            ? value
            : defaultValue;
    }
}
```

#### 4. Use in handlers

```csharp
public static class FeatureFlags
{
    public const string AiChatEnabled = "aiChatEnabled";
    public const string NasheedIngestionEnabled = "nasheedIngestionEnabled";
}

// In a handler:
if (!_featureFlags.IsEnabled(FeatureFlags.AiChatEnabled))
    throw new ForbiddenException(LocalizationKeys.Exceptions.FeatureNotEnabled);
```

### Implementation Checklist

- [ ] Add `FeatureFlags` dictionary to `TenantConfiguration` in Shared.Kernel
- [ ] Add `IFeatureFlagService` and `TenantFeatureFlagService` to Shared
- [ ] Register as Scoped in shared DI
- [ ] Add `FeatureFlags` static class with flag name constants (one per service)
- [ ] Gate the AI chat endpoints in Nasheed behind `aiChatEnabled`
- [ ] Gate the Nasheed ingestion pipeline behind `nasheedIngestionEnabled`
- [ ] Update Tenant admin API docs to document the `featureFlags` payload structure
- [ ] Add flag checks to any other experimental endpoints

---

## 9. Database Backup & Recovery

### Why It's Needed

The database-per-tenant architecture means a backup strategy must cover dozens of databases, not just one. There is currently no documented backup schedule, no tested recovery procedure, and no point-in-time restore capability.

### Recommended Approach: pg_dump scripts + PostgreSQL WAL archiving

#### 1. Per-tenant backup script (PowerShell)

```powershell
# scripts/backup-tenant-databases.ps1
# Run daily via Windows Task Scheduler or cron

$backupDir = "C:\Backups\PostgreSQL\$(Get-Date -Format 'yyyy-MM-dd')"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

# Get all tenant connection strings from Tenant Service API
$tenants = Invoke-RestMethod -Uri "http://localhost:5002/api/admin/tenant" `
    -Headers @{ Authorization = "Bearer $env:ADMIN_TOKEN" }

foreach ($tenant in $tenants.items) {
    $tenantId = $tenant.tenantId
    $connStr = $tenant.data.database.connectionString

    # Parse host, db, user from connection string
    # pg_dump to compressed file
    $outFile = "$backupDir\$tenantId.dump"
    pg_dump --format=custom --file=$outFile $connStr

    Write-Host "Backed up tenant: $tenantId → $outFile"
}

# Also backup global databases
pg_dump --format=custom --file="$backupDir\global.dump" $env:GLOBAL_DB_CONN
```

#### 2. Recovery procedure (document + test)

```powershell
# scripts/restore-tenant-database.ps1
param(
    [string]$TenantId,
    [string]$BackupFile,
    [string]$TargetConnectionString
)

pg_restore --clean --if-exists --no-owner `
    --connection-string $TargetConnectionString `
    $BackupFile

Write-Host "Restored $TenantId from $BackupFile"
```

#### 3. WAL archiving for point-in-time recovery (postgresql.conf)

```ini
# postgresql.conf
wal_level = replica
archive_mode = on
archive_command = 'cp %p /var/lib/postgresql/wal-archive/%f'
restore_command = 'cp /var/lib/postgresql/wal-archive/%f %p'
```

#### 4. Backup retention policy

| Type | Retention | Storage |
|---|---|---|
| Daily full backup | 30 days | Local + offsite |
| Weekly backup | 12 weeks | Offsite only |
| Monthly backup | 12 months | Offsite only |
| WAL archive | 7 days rolling | Local |

### Implementation Checklist

- [ ] Write `scripts/backup-tenant-databases.ps1`
- [ ] Write `scripts/restore-tenant-database.ps1`
- [ ] Schedule daily backup (Task Scheduler / cron)
- [ ] Configure WAL archiving in `postgresql.conf`
- [ ] Test restore from backup in a staging environment
- [ ] Document backup location and admin token rotation in Secrets Management
- [ ] Add backup verification job (restore + row count check on random tenant)
- [ ] Set up offsite backup destination (Azure Blob / S3)

---

---

# 🟢 Tier 3 — Growth Phase

> Build these when tenant count and traffic demand them. They are not urgent but become painful to retrofit late.

---

## 10. Search Service

### Why It's Needed

PostgreSQL `LIKE` and `ILIKE` queries do not scale for full-text search across large Nasheed song catalogs, category trees, or user lists. Elasticsearch provides relevance ranking, typo tolerance, and faceted filtering that PostgreSQL cannot match at scale.

### Recommended Approach: Elasticsearch + NEST client, event-driven index sync

Sync the Elasticsearch index using the existing event-driven pattern (Category already publishes Redis events — the search indexer subscribes and updates ES).

### What to Build

#### 1. New service: `src/Services/Search/`

Follow `NEW_SERVICE_INTEGRATION_GUIDE.md`. This service:
- **Strategy A** (Global DB) — maintains its own index metadata table
- Subscribes to Redis events from Category, Nasheed
- Exposes `/api/v1/search?q=...&type=song|category|artist`

#### 2. Elasticsearch index mappings (one index per entity type)

```json
// songs index
{
  "mappings": {
    "properties": {
      "title": { "type": "text", "analyzer": "arabic" },
      "artist": { "type": "keyword" },
      "categorySlug": { "type": "keyword" },
      "tenantId": { "type": "keyword" },
      "tags": { "type": "keyword" }
    }
  }
}
```

#### 3. Index sync handler (subscribes to Nasheed song events)

```csharp
// Reuses the same Redis subscriber pattern from CATEGORY_EVENT_DRIVEN_CONSUMER_GUIDE.md
// but writes to Elasticsearch instead of a local DB table
public class NasheedSongIndexer : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var sub = _redis.GetSubscriber();
        await sub.SubscribeAsync("nasheed:events:*", async (channel, message) =>
        {
            var ev = JsonSerializer.Deserialize<NasheedSongEventMessage>(message);
            await IndexOrDeleteAsync(ev, ct);
        });
    }
}
```

### Implementation Checklist

- [ ] Stand up Elasticsearch container locally
- [ ] Create `src/Services/Search/` project following NEW_SERVICE_INTEGRATION_GUIDE.md
- [ ] Define index mappings for songs and categories
- [ ] Implement Redis subscriber for Nasheed song events
- [ ] Implement Redis subscriber for Category events
- [ ] Expose `GET /api/v1/search` endpoint with `q`, `type`, `tenantId` params
- [ ] Add Nasheed event publishing (follow EVENT_DRIVEN_PUBLISHER_PATTERN.md for Nasheed)
- [ ] Add search endpoint to gateway routing table
- [ ] Add Arabic language analyzer for song/artist names

---

## 11. CDN / Media Delivery

### Why It's Needed

FileManager currently serves audio files and images directly from the .NET service. For a Nasheed library with thousands of audio files per tenant, streaming audio through a .NET process will saturate the server's bandwidth and block request threads. A CDN offloads delivery to edge nodes close to the listener.

### Recommended Approach: Cloudflare R2 + signed URLs

FileManager already has Cloudflare R2 configuration in place (`blob storage support: Cloudflare R2` was found in the service scan). The missing piece is signed URL generation for private media access and a CDN-fronted public URL for cacheable assets.

### What to Build

#### 1. Signed URL endpoint (add to FileManager)

```csharp
// FileManager.API/Endpoints — new endpoint
adminGroup.MapGet("/files/{id}/signed-url", async (
    int id,
    [FromQuery] int expiryMinutes = 60,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GenerateSignedUrlQuery(id, expiryMinutes), ct);
    return Results.Ok(new { url = result.Url, expiresAt = result.ExpiresAt });
})
.RequireAuthorization()
.WithMetadata(new BypassTenantAttribute());
```

#### 2. R2 signed URL generation (add to FileManager.Infrastructure)

```csharp
public class R2FileStorageService : IFileStorageService
{
    public string GenerateSignedUrl(string blobKey, TimeSpan expiry)
    {
        // AWS SDK-compatible signed URL (R2 supports S3-compatible pre-signed URLs)
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = blobKey,
            Expires = DateTime.UtcNow.Add(expiry)
        };
        return _s3Client.GetPreSignedURL(request);
    }
}
```

#### 3. Nasheed audio player integration

Instead of streaming through FileManager, Nasheed returns a signed R2 URL. The frontend player streams directly from CDN.

```csharp
// In GetSongQueryHandler:
var signedUrl = await _fileManagerClient.GetSignedUrlAsync(song.AudioFileId, expiry: TimeSpan.FromHours(2));
return new SongDto { ..., AudioUrl = signedUrl };
```

### Implementation Checklist

- [ ] Verify Cloudflare R2 bucket and credentials are configured in FileManager appsettings
- [ ] Implement `GenerateSignedUrl` in `R2FileStorageService`
- [ ] Add `GenerateSignedUrlQuery` + handler in FileManager
- [ ] Add signed URL endpoint to FileManager API
- [ ] Update Nasheed `GetSongQueryHandler` to return CDN signed URL instead of FileManager URL
- [ ] Configure R2 custom domain / CDN for public assets (cover images)
- [ ] Set signed URL expiry to match audio session duration
- [ ] Add CDN cache headers for public non-private assets (artist images, category banners)

---

## 12. Usage Metering / Billing Hooks

### Why It's Needed

The AI service already logs token usage per tenant. FileManager stores file sizes per tenant. There is no unified way to aggregate this data, set per-tenant limits, or feed it into a billing system.

### Recommended Approach: Metering table in global DB + tenant quota enforcement

Rather than a dedicated billing service, add a `usage_events` table to a shared global database and enforce quota checks at the handler level.

### What to Build

#### 1. Shared `UsageEventEntity` (add to `IhsanDev.Shared.Kernel`)

```csharp
public class UsageEventEntity
{
    public long Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string MetricKey { get; set; } = string.Empty; // "ai.tokens", "storage.bytes", "api.calls"
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;      // "tokens", "bytes", "requests"
    public string ServiceName { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
```

#### 2. Shared `IUsageMeteringService` (add to `IhsanDev.Shared.Application`)

```csharp
public interface IUsageMeteringService
{
    Task RecordAsync(string metricKey, decimal quantity, string unit, CancellationToken ct = default);
    Task<decimal> GetMonthlyUsageAsync(string tenantId, string metricKey, CancellationToken ct = default);
    Task<bool> IsWithinQuotaAsync(string tenantId, string metricKey, CancellationToken ct = default);
}
```

#### 3. Metric keys to implement first

| Metric key | Where to record | Unit |
|---|---|---|
| `ai.tokens.input` | AI service call in Nasheed handler | tokens |
| `ai.tokens.output` | AI service call in Nasheed handler | tokens |
| `storage.bytes` | FileManager SaveFileCommandHandler | bytes |
| `storage.files` | FileManager SaveFileCommandHandler | files |
| `api.calls` | Gateway middleware (count per tenant) | requests |

#### 4. Quota enforcement example

```csharp
// In NasheedAiChatCommandHandler:
if (!await _metering.IsWithinQuotaAsync(tenantId, "ai.tokens.input", ct))
    throw new ForbiddenException(LocalizationKeys.Exceptions.QuotaExceeded);

var response = await _aiClient.ChatAsync(request, ct);

await _metering.RecordAsync("ai.tokens.input", response.InputTokens, "tokens", ct);
await _metering.RecordAsync("ai.tokens.output", response.OutputTokens, "tokens", ct);
```

#### 5. Add quota configuration to TenantConfiguration

```json
// In Tenant data payload:
{
  "quotas": {
    "ai.tokens.monthly": 1000000,
    "storage.bytes.max": 10737418240
  }
}
```

### Implementation Checklist

- [ ] Add `UsageEventEntity` to `IhsanDev.Shared.Kernel`
- [ ] Add `IUsageMeteringService` to `IhsanDev.Shared.Application`
- [ ] Implement `DbUsageMeteringService` writing to global DB
- [ ] Add `quotas` dictionary to `TenantConfiguration`
- [ ] Register `IUsageMeteringService` as Scoped in shared DI
- [ ] Record AI token usage in Nasheed's AI chat handler
- [ ] Record storage usage in FileManager's SaveFileCommandHandler
- [ ] Add quota enforcement checks for AI token limit
- [ ] Add admin endpoint to query usage by tenant + metric + date range
- [ ] Add monthly usage summary to Tenant admin dashboard response

---

---

## 🔄 Maintenance

**Update this file when:**
1. ✅ An item is started → change status to `🔵 In progress`
2. ✅ An item is completed → change status to `✅ Done` and link to the guide or PR
3. ✅ An approach changes → update the relevant section
4. ✅ A new capability gap is identified → add it to the appropriate tier

**Status legend:**

| Symbol | Meaning |
|---|---|
| ⬜ | Not started |
| 🔵 | In progress |
| ✅ | Done — link to guide |
| ❌ | Descoped |
