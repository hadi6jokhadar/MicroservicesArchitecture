# 🔍 Tenant Middleware - What You Need to Know

## The Short Answer

**Q: Do I need to write tenant-resolution middleware in every new service?**

**A: NO — the middleware class itself is shared.** But you do need to register it explicitly in `Program.cs`; it is **not** wired up automatically by `AddMultiTenancy()` alone. For a Strategy B/C service you write four lines, not one:

```csharp
// Program.cs — DI
builder.Services.AddMultiTenancy(builder.Configuration);   // registers ITenantContext, ITenantConfigurationProvider, the Tenant Service HttpClient

// Program.cs — pipeline, after app.Build()
app.UseTenantResolution(builder.Configuration);   // reads x-tenant-id, resolves ITenantContext
app.UseTenantAwareCors();                          // tenant-aware CORS (replaces UseCors())
app.UseTenantDatabaseMigration<MyServiceDbContext>(builder.Configuration); // only if multi-tenancy enabled
```

`AddMultiTenancy()` only registers DI services. It never touches `IApplicationBuilder` — skip the `app.Use...` calls and tenant resolution simply never runs for that service, with no startup error to warn you. See `.claude/instructions/database-strategy.instructions.md` for the full, strategy-specific pipeline order (`UseDefaultDatabaseMigration` → `UseTenantResolution` → `UseTenantAwareCors` → `UseTenantDatabaseMigration` → `UseAuthentication` → `UseJwtTenantVerification` → `UseAuthorization`).

---

## How It Works

### What `AddMultiTenancy()` Registers (DI only)

```
Your Code:
┌──────────────────────────────────────────────┐
│ builder.Services.AddMultiTenancy(config);     │
└──────────────────────────────────────────────┘
                    │
                    ↓
Shared Library Registers (no pipeline changes):
┌──────────────────────────────────────────────┐
│ 1. ✅ ITenantContext                          │
│ 2. ✅ ITenantConfigurationProvider            │
│ 3. ✅ HttpClient for Tenant Service            │
│    (with resilience: retry, circuit breaker)  │
│ 4. ✅ ICacheService-backed tenant-config cache │
└──────────────────────────────────────────────┘
```

### What `app.UseTenantResolution(configuration)` Adds (pipeline)

This is the separate call that actually wires `TenantMiddleware` into the request pipeline. Without it, `ITenantContext.CurrentTenant` is always `null`, no matter what `AddMultiTenancy()` registered.

### The Middleware Flow

```
1. HTTP Request arrives
   │
   ├─ Header: x-tenant-id: customer-123
   │
   ↓
2. TenantMiddleware (from shared lib) intercepts
   │  Bypasses immediately for: OPTIONS preflight, static files,
   │  /metrics, /health, and any [BypassTenant]/[OptionalTenant] endpoint
   │
   ↓
3. Extract tenant ID from the x-tenant-id header
   │
   ↓
4. ITenantConfigurationProvider checks its cache (Redis, or in-memory if Redis is disabled)
   │
   ├─ Found → Use cached config
   │
   └─ Not Found → Call Tenant Service's GET /api/v1/tenant/config/{tenantId}
       │
       ↓
5. Load tenant configuration
   │
   ├─ Success → Cache for MultiTenancy:CacheExpirationMinutes (default 30)
   │
   └─ Failed → 404/400 JSON error via ILocalizationService (no silent fallback)
       │
       ↓
6. Set ITenantContext.CurrentTenant, set request culture
   │
   ↓
7. Continue to your handler
   │
   ↓
8. Your handler accesses tenant via ITenantContext
```

---

## Where Is the Middleware Code?

### Location in Shared Library

```
src/Shared/IhsanDev.Shared.Infrastructure/
├── Extensions/
│   ├── MultiTenancyExtensions.cs        ← AddMultiTenancy() — DI only
│   └── TenantResolutionExtensions.cs    ← UseTenantResolution()/UseTenantAwareCors() — pipeline
│
├── Middleware/
│   └── TenantMiddleware.cs              ← The actual middleware
│
└── Services/Tenant/
    └── TenantConfigurationProvider.cs   ← Calls Tenant Service API, owns the cache
```

**You don't need to create or modify these files.** They're already implemented and ready to use — you only need to call the two extension methods above from `Program.cs`.

---

## What You Actually Do

### Step 1: Configuration (appsettings.json)

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 30
  }
}
```

### Step 2: Register in Program.cs

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// DI
builder.Services.AddMultiTenancy(builder.Configuration);
builder.Services.AddDatabaseContext<MyServiceDbContext>(builder.Configuration, "MyService");

var app = builder.Build();

// Pipeline — order matters, see database-strategy.instructions.md for the full sequence
app.UseDefaultDatabaseMigration<MyServiceDbContext>();
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();

if (builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false))
{
    app.UseTenantDatabaseMigration<MyServiceDbContext>(builder.Configuration);
}

app.UseAuthentication();
app.UseJwtTenantVerification(builder.Configuration);
app.UseAuthorization();

app.Run();
```

### Step 3: Use in Your Handlers

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly ITenantContext _tenantContext;

    public CreateOrderHandler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.HasTenant && _tenantContext.CurrentTenant != null)
        {
            var tenantId = _tenantContext.CurrentTenant.TenantId;
            var tenantConfig = _tenantContext.CurrentTenant.Configuration;
            // Use tenant data in your logic
        }
        else
        {
            // No tenant (non-tenant request, or a [BypassTenant]/[OptionalTenant] endpoint with no header)
        }

        // Your business logic...
        return orderDto;
    }
}
```

---

## Common Misconceptions

### ❌ WRONG: "I need to write my own tenant-resolution middleware class"

```csharp
// ❌ DON'T DO THIS! — TenantMiddleware already exists in the shared library
public class MyOwnTenantMiddleware
{
    public async Task InvokeAsync(HttpContext context) { /* ... */ }
}
```

### ❌ ALSO WRONG: "AddMultiTenancy() alone is enough, no pipeline registration needed"

```csharp
// ❌ Tenant resolution never runs with only this line — ITenantContext stays empty for every request
builder.Services.AddMultiTenancy(builder.Configuration);
var app = builder.Build();
app.Run(); // no app.UseTenantResolution() call!
```

### ✅ CORRECT: register the DI services, then wire the pipeline

```csharp
// ✅ DI
builder.Services.AddMultiTenancy(builder.Configuration);

var app = builder.Build();

// ✅ Pipeline — this is the part that actually resolves the tenant per request
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();
```

---

## Comparison: What You Implement vs What's Automatic

| Task                                             | You Implement? | Shared Library Does? |
| ------------------------------------------------- | -------------- | --------------------- |
| Write the middleware class                        | ❌ No          | ✅ Yes                |
| Extract tenant ID from header                      | ❌ No          | ✅ Yes                |
| Call Tenant Service API                            | ❌ No          | ✅ Yes                |
| Implement caching                                  | ❌ No          | ✅ Yes                |
| Enforce tenant validation                          | ❌ No          | ✅ Yes                |
| **Call `AddMultiTenancy()` in `Program.cs`**       | **✅ Yes**     | ❌ No                 |
| **Call `app.UseTenantResolution(...)`/`UseTenantAwareCors()` in the pipeline** | **✅ Yes** | ❌ No |
| **Use `ITenantContext` in handlers**               | **✅ Yes**     | ❌ No                 |

**You do 3 things, not 1:**

1. Call `AddMultiTenancy()` in the DI section of `Program.cs`
2. Call `app.UseTenantResolution(...)` and `app.UseTenantAwareCors()` in the pipeline section
3. Use `ITenantContext` in your handlers

---

## Services That Already Use This

### Identity Service (Strategy B — per-tenant DB)

```csharp
// src/Services/Identity/Identity.API/Program.cs
builder.Services.AddMultiTenancy(builder.Configuration);
// ...
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();
```

### Tenant Service

```csharp
// src/Services/Tenant/Tenant.API/Program.cs
// Multi-tenancy NOT used (Tenant Service is the provider, not a consumer)
// It always uses static configuration from appsettings.json:
// - Database: DatabaseSettings:ConnectionString
// - JWT: Jwt section
// - CORS: Cors section
// No MultiTenancy configuration or UseTenantResolution() call needed here
```

### A New Service

Copy Identity's or Category's `Program.cs` pipeline section exactly (see `.claude/instructions/database-strategy.instructions.md` for the strategy-specific full order) — no custom middleware needed, but the pipeline calls are not optional.

---

## Testing Without the Middleware

In integration tests, you typically disable multi-tenancy:

```csharp
// CustomWebApplicationFactory.cs
protected override Dictionary<string, string?> GetTestConfiguration()
{
    var config = base.GetTestConfiguration();
    config["MultiTenancy:Enabled"] = "false";
    return config;
}
```

---

## Troubleshooting

### Issue: `ITenantContext.CurrentTenant` is always null

**Possible causes**, roughly in order of how often each one is actually the culprit:

1. **`app.UseTenantResolution(...)` was never called** — `AddMultiTenancy()` alone does not resolve anything per request. This is the single most common cause and is easy to miss because nothing errors at startup.
2. Multi-tenancy not enabled in configuration: `{ "MultiTenancy": { "Enabled": true } }`.
3. `x-tenant-id` header not sent in the request:
   ```bash
   curl -H "x-tenant-id: customer-123" https://localhost:5002/api/v1/orders
   ```
4. Tenant doesn't exist, or is archived, in Tenant Service:
   ```bash
   curl https://localhost:5002/api/v1/tenant/config/customer-123
   ```
5. Tenant Service is not running:
   ```bash
   curl https://localhost:5002/health
   ```

### Issue: "Cannot resolve ITenantContext"

**Solution**: Call `AddMultiTenancy()` in the DI section of `Program.cs`:

```csharp
builder.Services.AddMultiTenancy(builder.Configuration);
```

### Issue: Middleware not intercepting requests at all

**Solution**: Confirm `app.UseTenantResolution(builder.Configuration)` is actually present in the pipeline (not just `AddMultiTenancy()` in DI) — there is no `app.UseMultiTenancy()` method; the pipeline registration is `UseTenantResolution`, a separate call from the DI registration.

---

## Summary

### What You Need to Remember

1. **The tenant middleware class is already implemented** in `IhsanDev.Shared.Infrastructure` — you never write your own.
2. **You still need both a DI call and a pipeline call**: `AddMultiTenancy(configuration)` in DI, then `UseTenantResolution(configuration)` + `UseTenantAwareCors()` in the pipeline.
3. **The middleware automatically**: intercepts requests, extracts the tenant ID from the header, calls Tenant Service, caches the result (`MultiTenancy:CacheExpirationMinutes`, default 30), and sets `ITenantContext`.
4. **You use** `ITenantContext` in your handlers to access tenant data.
5. **Multi-tenancy is optional** per service — a Strategy A/D service (Tenant Service, Translation) skips all of this.

### Quick Reference

```csharp
// DI
builder.Services.AddMultiTenancy(builder.Configuration);

// Pipeline (after app.Build())
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();

// Access tenant in any handler
if (_tenantContext.HasTenant)
{
    var tenantId = _tenantContext.CurrentTenant.TenantId;
}
```

---

## Related Documentation

- [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) — complete new-service guide
- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) — comprehensive multi-tenancy docs
- [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) — per-tenant database architecture
- `.claude/instructions/database-strategy.instructions.md` — full, strategy-specific `Program.cs` pipeline order

---

**Last Updated**: August 2026
**Status**: ✅ Corrected to match the current `TenantMiddleware`/`UseTenantResolution` implementation
