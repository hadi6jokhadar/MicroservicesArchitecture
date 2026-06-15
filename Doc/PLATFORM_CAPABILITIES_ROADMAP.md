# Platform Capabilities Roadmap

**Status:** Planning  
**Created:** June 3, 2026  
**Purpose:** Actionable implementation guide for missing platform capabilities, organized by priority tier.  
**Stack:** .NET 10 Minimal APIs · PostgreSQL · Redis · Clean Architecture · CQRS · MediatR

> This document is a companion to the analysis in `DOCUMENTATION_INDEX.md`. Each item below is a gap in the current platform. Work through tiers in order — Tier 1 items are load-bearing; Tier 2 and 3 build on top of them.

---

## 📋 Progress Tracker

| #   | Capability                            | Tier | Status         |
| --- | ------------------------------------- | ---- | -------------- |
| 1   | API Gateway                           | 1    | ✅ Done        |
| 2   | Distributed Tracing & Observability   | 1    | ✅ Done        |
| 3   | Secrets Management                    | 1    | ⬜ Not started |
| 4   | Circuit Breaker / Resilience Patterns | 1    | ✅ Done        |
| 5   | Audit Logging Service                 | 1    | ✅ Done        |
| 6   | Background Job / Scheduling Service   | 2    | ✅ Done        |
| 7   | API Versioning Standard               | 2    | ⬜ Not started |
| 8   | Feature Flags Service                 | 2    | ⬜ Not started |
| 9   | Database Backup & Recovery            | 2    | ⬜ Not started |
| 10  | Search Service                        | 3    | ⬜ Not started |
| 11  | CDN / Media Delivery                  | 3    | ⬜ Not started |
| 12  | Usage Metering / Billing Hooks        | 3    | ⬜ Not started |

---

---

# 🔴 Tier 1 — Must-Have

> These are load-bearing gaps. A production outage without them is hard to diagnose, hard to contain, and hard to recover from.

---

## 1. API Gateway

### Why It's Needed

Every service currently exposes its own port directly. A frontend or mobile app must know 8 different base URLs (.NET services on 5001–5007, Python AI service on 5008). There is no centralized rate limiting, no single SSL termination point, and no place to inject cross-cutting headers (correlation IDs, auth pre-checks) before requests reach services.

### Recommended Approach: YARP (Yet Another Reverse Proxy)

YARP is a Microsoft-maintained .NET reverse proxy — the best fit for a .NET-native stack. It runs as a standard .NET 10 Minimal API project and supports hot-reload routing config.

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

YARP is **language-agnostic** — it proxies HTTP regardless of whether the backend is .NET or Python. All 8 services go in the same routing table.

The AI Python service has two endpoint types that need different route configurations:

- `/api/v1/chat/single` — standard JSON response, no special config needed
- `/api/v1/chat/stream` — Server-Sent Events (SSE) streaming; needs response buffering disabled and a long timeout

```json
{
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity",
        "Match": { "Path": "/api/auth/{**catch-all}" }
      },
      "tenant-route": {
        "ClusterId": "tenant",
        "Match": { "Path": "/api/tenant/{**catch-all}" }
      },
      "filemanager-route": {
        "ClusterId": "filemanager",
        "Match": { "Path": "/api/filemanager/{**catch-all}" }
      },
      "notification-route": {
        "ClusterId": "notification",
        "Match": { "Path": "/api/notifications/{**catch-all}" }
      },
      "translation-route": {
        "ClusterId": "translation",
        "Match": { "Path": "/api/translations/{**catch-all}" }
      },
      "category-route": {
        "ClusterId": "category",
        "Match": { "Path": "/api/categories/{**catch-all}" }
      },
      "nasheed-route": {
        "ClusterId": "nasheed",
        "Match": { "Path": "/api/nasheed/{**catch-all}" }
      },

      "ai-route": {
        "ClusterId": "ai",
        "Match": { "Path": "/api/v1/ai/{**catch-all}" }
      },
      "ai-stream-route": {
        "ClusterId": "ai",
        "Match": { "Path": "/api/v1/ai/chat/stream" },
        "Transforms": [{ "ResponseHeaderRemove": "Content-Length" }],
        "Timeout": "00:10:00"
      }
    },
    "Clusters": {
      "identity": {
        "Destinations": { "d1": { "Address": "https://localhost:5001" } }
      },
      "tenant": {
        "Destinations": { "d1": { "Address": "https://localhost:5002" } }
      },
      "filemanager": {
        "Destinations": { "d1": { "Address": "https://localhost:5005" } }
      },
      "notification": {
        "Destinations": { "d1": { "Address": "https://localhost:5004" } }
      },
      "translation": {
        "Destinations": { "d1": { "Address": "https://localhost:5006" } }
      },
      "category": {
        "Destinations": { "d1": { "Address": "https://localhost:5007" } }
      },
      "nasheed": {
        "Destinations": { "d1": { "Address": "https://localhost:5009" } }
      },
      "ai": {
        "Destinations": { "d1": { "Address": "http://localhost:5008" } },
        "HttpClient": {
          "RequestHeaderEncoding": "Latin1"
        }
      }
    }
  }
}
```

> **Why a separate `ai-stream-route`?** SSE streaming keeps the HTTP connection open until the model finishes generating. YARP's default response timeout (100s) will cut it off mid-stream for long completions. The stream route overrides timeout to 10 minutes and removes `Content-Length` (SSE responses don't have one). The non-stream route uses the default — no change needed.

> **Why `http://` for the AI cluster?** The Python FastAPI service runs on plain HTTP in development (no .NET Kestrel HTTPS). In production, use a TLS-terminating load balancer in front of the Python service and point the YARP cluster at the internal HTTP address.

#### 4. Path rewriting for the AI service

The AI service's internal routes are `/api/v1/chat/single` and `/api/v1/chat/stream`. The gateway exposes them at `/api/v1/ai/chat/single` and `/api/v1/ai/chat/stream`. YARP must strip the `/ai` prefix before forwarding:

```json
"ai-route": {
  "ClusterId": "ai",
  "Match": { "Path": "/api/v1/ai/{**catch-all}" },
  "Transforms": [
    { "PathPattern": "/api/v1/{**catch-all}" }
  ]
}
```

This means a client calls `POST /api/v1/ai/chat/single` → YARP rewrites to `POST /api/v1/chat/single` → Python service.

#### 5. Service-to-service calls bypass the gateway

Internal calls between services (e.g. Nasheed → AI) should go **direct** to `http://localhost:5008`, not through the gateway. Going through the gateway adds an extra network hop and ties internal availability to the gateway's availability. The gateway is for **client-to-service** traffic only.

```csharp
// Nasheed appsettings.json — point directly at AI service, not gateway
"AiService": {
  "BaseUrl": "http://localhost:5008"   // direct, not through gateway
}
```

#### 6. Services affected by this change

- **All frontends / mobile apps:** Single base URL — the gateway port (e.g. `https://localhost:5000`)
- **All services:** No code changes — they stay on their own ports; gateway proxies to them
- **Service-to-service clients:** Keep pointing direct (no change to `appsettings.json` service URLs)
- **CORS:** Each service's `UseTenantAwareCors` or `UseCors` still controls allowed origins

### Implementation Checklist

- [x] Create `src/Gateway/Gateway.API/` project
- [x] Install `Yarp.ReverseProxy` NuGet package (v2.3.0)
- [x] Configure routing table in appsettings.json for all 8 services (7 .NET + 1 Python AI)
- [x] Add separate `ai-stream-route` with 10-minute timeout and `Content-Length` removal
- [x] Add path-rewrite transform for the AI cluster (`/api/v1/ai/` → `/api/v1/`)
- [x] Add correlation ID injection middleware
- [x] Add rate limiter (per-IP, 500 req/min)
- [x] Add to solution file
- [x] Add `run-development-instance.bat` and entry in `start-all-services.mjs`
- [x] Add `/health` endpoint on the gateway itself
- [ ] Update all frontend API base URLs to point to gateway (port 5000)
- [ ] Verify service-to-service calls still use direct addresses (not gateway)
- [ ] Wire gateway `/health` to aggregate all downstream `/health` endpoints
- [ ] Smoke-test the streaming endpoint through the gateway (`/api/v1/ai/chat/stream`)

> **Gateway project:** `src/Gateway/Gateway.API/` — runs on `http://localhost:5000`  
> **Full routing reference:** `Doc/API_GATEWAY_GUIDE.md`

---

## 2. Distributed Tracing & Observability

### Why It's Needed

When a request touches Identity → FileManager → Notification, there is no way to trace it end-to-end in production today. A single slow query in the wrong service causes a timeout that surfaces in the gateway with no actionable information.

### Recommended Approach: OpenTelemetry + Jaeger + Grafana (fully free, OSS)

Use **OpenTelemetry** (vendor-neutral instrumentation) with **Jaeger** as the tracing backend across **all environments** — local and production. For metrics and alerting, add **Prometheus + Grafana** alongside it. This fits cleanly onto the existing Serilog logging setup and costs nothing.

| Concern              | Tool                       | Cost       |
| -------------------- | -------------------------- | ---------- |
| Distributed tracing  | OpenTelemetry SDK → Jaeger | Free / OSS |
| Metrics & dashboards | Prometheus + Grafana       | Free / OSS |
| Structured logs      | Serilog (already in place) | Free / OSS |

### What to Build

#### 1. NuGet packages (add to every .NET service)

```powershell
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol   # OTLP — used by both Jaeger and Grafana
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore   # Prometheus /metrics scrape endpoint
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

                var otlpEndpoint = configuration["Observability:OtlpEndpoint"];
                if (!string.IsNullOrEmpty(otlpEndpoint))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddPrometheusExporter();   // exposes /metrics for Prometheus to scrape
            });

        return services;
    }
}
```

#### 3. Expose the Prometheus scrape endpoint in every service's Program.cs

```csharp
builder.Services.AddPlatformObservability(builder.Configuration, "IdentityService");

// ...after app.Build()...
app.MapPrometheusScrapingEndpoint("/metrics");   // Prometheus scrapes this
```

#### 4. Local Docker Compose — Jaeger + Prometheus + Grafana

```yaml
# docker-compose.observability.yml  (run alongside your services)
services:
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686" # Jaeger UI
      - "4317:4317" # OTLP gRPC (services export here)
      - "4318:4318" # OTLP HTTP

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
    ports:
      - "9090:9090"

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    depends_on:
      - prometheus
      - jaeger
```

Minimal `prometheus.yml` to scrape all services:

```yaml
scrape_configs:
  - job_name: "services"
    static_configs:
      - targets:
          - "host.docker.internal:5001" # IdentityService
          - "host.docker.internal:5002" # TenantService
          - "host.docker.internal:5003" # FileManagerService
          - "host.docker.internal:5004" # NotificationService
          - "host.docker.internal:5005" # TranslationService
          - "host.docker.internal:5006" # AIService
          - "host.docker.internal:5000" # Gateway
    metrics_path: /metrics
```

#### 5. appsettings configuration (all services)

```json
{
  "Observability": {
    "OtlpEndpoint": "http://localhost:4317"
  }
}
```

In production, point `OtlpEndpoint` at the hosted Jaeger OTLP collector. No code change needed.

#### 6. Health check endpoints (add to every service)

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

#### 7. Correlation ID propagation

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

- [x] Add `AddPlatformObservability` extension to `IhsanDev.Shared.Infrastructure`
- [x] Register in all 6 .NET services' `Program.cs` + expose `/metrics`
- [x] Wire AI Python service (FastAPI + SQLAlchemy + Prometheus)
- [x] Add `docker-compose.observability.yml` + `prometheus.yml` to repo root
- [x] Add `start-observability.mjs` — launches stack automatically with `start-all-services`
- [x] Add `start-observability` Nx target to `project.json`
- [x] Add `/health` and `/health/ready` endpoints to all services (Notification already has them)
- [x] Add correlation ID middleware to shared infrastructure (`CorrelationIdMiddleware` + `UseCorrelationId()` in `IhsanDev.Shared.Infrastructure`)
- [x] Wire gateway `/health/aggregate` to probe all downstream `/health` endpoints in parallel (reads cluster addresses from YARP config)
- [ ] Verify traces in Jaeger UI (`http://localhost:16686`) after first run — **requires Docker stack running**
- [ ] Add Prometheus data source in Grafana and import ASP.NET Core dashboard (ID 10915) — **manual step in Grafana UI**

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

.NET 10 ships `Microsoft.Extensions.Resilience` as a first-class package. It integrates directly with `IHttpClientFactory`.

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

| Calling Service | Calls                     | Priority |
| --------------- | ------------------------- | -------- |
| Identity        | Notification, FileManager | High     |
| FileManager     | Notification, Tenant      | High     |
| Nasheed         | FileManager, AI           | High     |
| Notification    | Identity, Tenant          | Medium   |
| Category        | Tenant                    | Medium   |

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

- [x] Add `Microsoft.Extensions.Http.Resilience` to `IhsanDev.Shared.Infrastructure`
- [x] Update all service client extension methods with `.AddStandardResilienceHandler()` (FileManager, Notification, Identity, Tenant — all named and typed overloads)
- [x] Apply to all 5 inter-service HTTP clients (FileManager, Notification, Identity × 2 overloads, Tenant × 2 overloads)
- [x] Wrap all FileManager and AI service calls in `catch (BrokenCircuitException)` in handlers — Category (3), Nasheed (5), Identity (4) — 12 handlers total
- [x] Add resilience pipeline to the AI service HTTP client in Nasheed (custom `AddResilienceHandler` — no timeout override, circuit breaker + 1 retry)
- [x] Verify circuit opens correctly under simulated failure — tested by stopping FileManager; circuit opened after timeout, subsequent calls returned in ~0ms, profile endpoint responded successfully without image. Polly logs at `Warning` level.
- [x] Fix `NasheedIngestionWorker` retry behavior: replaced flat 5-minute `RetryDelay` with exponential back-off (30 s → 2 min → 10 min → 30 min), bumped `SongIngestionJobEntity.MaxRetries` default from 3 to 10, and added explicit `BrokenCircuitException` handling inside `RunEmbeddingGenerationAsync` so circuit-open state logs at `Warning` rather than `Error`.

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

| Service      | Operations to audit                             |
| ------------ | ----------------------------------------------- |
| Identity     | Login, logout, role assignment, password change |
| Tenant       | Tenant create, update, delete                   |
| Category     | Admin create, update, delete, move              |
| FileManager  | Admin delete, blob operations                   |
| Notification | Global send, archive                            |

### Implementation Checklist

- [x] Add `AuditLogEntity` to `IhsanDev.Shared.Kernel`
- [x] Add `IAuditService` to `IhsanDev.Shared.Application`
- [x] Implement `DbAuditService` in `IhsanDev.Shared.Infrastructure`
- [x] Register `IAuditService` as Scoped in shared DI registration (`AddAuditService()` in `InfrastructureServiceExtensions`)
- [x] Override `SaveChangesAsync` in `BaseDbContext` to flush audit rows atomically
- [x] Add `DbSet<AuditLogEntity>` and EF configuration to each service's DbContext (all 8 DbContexts updated)
- [x] Add `dotnet ef migrations add AddAuditLog` for each service (Identity, Tenant, FileManager, Translation, Category, Nasheed, Notification global, Notification tenant)
- [x] Replaced explicit `_auditService.Record()` calls with automatic ChangeTracker capture in `BaseDbContext.SaveChangesAsync` — all handlers across all services are covered with zero per-handler code
- [x] Add admin endpoint to query audit logs per tenant — `GET /api/admin/audit-logs` added to all 7 services (Identity, Tenant, FileManager, Notification, Translation, Category, Nasheed) with filter (`tenantId`, `entityType`, `action`, `userId`, `userEmail`, `fromDate`, `toDate`), sort (`sortBy`, `sortDesc`), and pagination (`page`, `pageSize`)

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

| Job                    | Service      | Schedule         | Queue    |
| ---------------------- | ------------ | ---------------- | -------- |
| Outbox event processor | Category     | Every 5 seconds  | critical |
| Temp file cleanup      | FileManager  | Daily at 2am     | low      |
| Notification cleanup   | Notification | Daily at 3am     | low      |
| Tenant cache refresh   | Tenant       | Every 30 minutes | default  |

### Implementation Checklist

- [x] Add `Hangfire.AspNetCore` + `Hangfire.PostgreSql` to Category.Infrastructure, FileManager.Infrastructure, Notification.API, Tenant.Infrastructure (versions pinned in `Directory.Packages.props`)
- [x] Configure per-service PostgreSQL storage with isolated schemas (`hangfire_category`, `hangfire_filemanager`, `hangfire_notification`, `hangfire_tenant`)
- [x] Add Hangfire dashboard behind `HangfireBasicAuthFilter` (HTTP Basic Auth) at service-specific paths: `/admin/jobs/category`, `/admin/jobs/filemanager`, `/admin/jobs/notification`, `/admin/jobs/tenant`
- [x] Credentials stored in each service's `appsettings.json` under `Hangfire:Dashboard:Username` / `Hangfire:Dashboard:Password`; dashboards accessed directly per service (not through the gateway)
- [x] Add `/admin/jobs` prefix to `TenantMiddleware` bypass list in `IhsanDev.Shared.Infrastructure` (alongside `/health` and `/metrics`)
- [x] Migrate Category's `OutboxEventProcessorService` polling loop → `OutboxEventProcessorJob` (every 1 minute)
- [x] Migrate FileManager's `TempFileCleanupService` polling loop → `TempFileCleanupJob` (daily at 02:00 UTC)
- [x] Migrate Notification's `CleanupService` polling loop → `NotificationCleanupJob` (hourly)
- [x] Migrate Tenant's `TenantCacheRefreshService` polling loop → `TenantCacheRefreshJob` (every 30 minutes)
- [x] `NotificationProcessor` kept as `BackgroundService` — it is a real-time sub-second queue poller, not a scheduled job
- [x] Verify Hangfire dashboards accessible at direct service URLs after startup (Basic Auth login prompt appears on first visit)
- [x] Verify recurring jobs appear in the Hangfire dashboard after first run
- [ ] Add job retry policies (exponential back-off, max 5 retries) — currently using Hangfire default (10 attempts, exponential)

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

| Type              | Retention      | Storage         |
| ----------------- | -------------- | --------------- |
| Daily full backup | 30 days        | Local + offsite |
| Weekly backup     | 12 weeks       | Offsite only    |
| Monthly backup    | 12 months      | Offsite only    |
| WAL archive       | 7 days rolling | Local           |

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

| Metric key         | Where to record                       | Unit     |
| ------------------ | ------------------------------------- | -------- |
| `ai.tokens.input`  | AI service call in Nasheed handler    | tokens   |
| `ai.tokens.output` | AI service call in Nasheed handler    | tokens   |
| `storage.bytes`    | FileManager SaveFileCommandHandler    | bytes    |
| `storage.files`    | FileManager SaveFileCommandHandler    | files    |
| `api.calls`        | Gateway middleware (count per tenant) | requests |

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

| Symbol | Meaning              |
| ------ | -------------------- |
| ⬜     | Not started          |
| 🔵     | In progress          |
| ✅     | Done — link to guide |
| ❌     | Descoped             |
