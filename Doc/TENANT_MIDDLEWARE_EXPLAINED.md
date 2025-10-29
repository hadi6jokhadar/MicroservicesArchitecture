# 🔍 Tenant Middleware - What You Need to Know

## The Short Answer

**Q: Do I need to implement tenant middleware in every new service?**

**A: NO!** The tenant middleware is already implemented in the shared infrastructure library. You just need **ONE LINE** to enable it:

```csharp
builder.Services.AddMultiTenancy(builder.Configuration);
```

That's it. Nothing more to implement.

---

## How It Works

### What Happens When You Call AddMultiTenancy()

```
Your Code:
┌──────────────────────────────────────────────┐
│ builder.Services.AddMultiTenancy(config);   │
└──────────────────────────────────────────────┘
                    │
                    ↓
Shared Library Automatically:
┌──────────────────────────────────────────────┐
│ 1. ✅ Registers TenantResolutionMiddleware   │
│ 2. ✅ Adds it to middleware pipeline         │
│ 3. ✅ Configures in-memory caching           │
│ 4. ✅ Sets up HttpClient for Tenant Service  │
│ 5. ✅ Registers ITenantContext               │
│ 6. ✅ Registers ITenantService               │
│ 7. ✅ Enforces strict tenant validation      │
└──────────────────────────────────────────────┘
```

### The Middleware Flow (Automatic)

```
1. HTTP Request arrives
   │
   ├─ Header: x-tenant-id: customer-123
   │
   ↓
2. TenantResolutionMiddleware (from shared lib) intercepts
   │
   ↓
3. Extract tenant ID from header
   │
   ↓
4. Check in-memory cache
   │
   ├─ Found → Use cached data
   │
   └─ Not Found → Call Tenant Service API
       │
       ↓
5. Load tenant configuration
   │
   ├─ Success → Cache for 60 minutes
   │
   └─ Failed → Return error (no fallback when multi-tenancy enabled)
       │
       ↓
6. Set ITenantContext.CurrentTenant (if successful)
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
│   └── MultiTenancyExtensions.cs       ← AddMultiTenancy() method
│
├── Middleware/
│   └── TenantResolutionMiddleware.cs   ← The actual middleware
│
└── Services/
    └── TenantService.cs                ← Calls Tenant Service API
```

**You don't need to create or modify these files.** They're already implemented and ready to use.

---

## What You Actually Do

### Step 1: Configuration (appsettings.json)

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5003",
    "CacheDurationMinutes": 60
  }
}
```

### Step 2: Register in Program.cs (ONE LINE)

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Your other service registrations...

// Enable multi-tenancy (one line!)
builder.Services.AddMultiTenancy(builder.Configuration);

var app = builder.Build();

// Your middleware pipeline...
// No need to call app.UseMultiTenancy() or anything else!

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
        // Access tenant data (if available)
        if (_tenantContext.HasTenant && _tenantContext.CurrentTenant != null)
        {
            var tenantId = _tenantContext.CurrentTenant.TenantId;
            var tenantName = _tenantContext.CurrentTenant.TenantName;
            var tenantConfig = _tenantContext.CurrentTenant.Configuration;

            // Use tenant data in your logic
        }
        else
        {
            // No tenant (non-tenant request or tenant not found)
            // Use default behavior
        }

        // Your business logic...
        return orderDto;
    }
}
```

---

## Common Misconceptions

### ❌ WRONG: "I need to create TenantMiddleware in my service"

```csharp
// ❌ DON'T DO THIS!
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // You don't need to write this!
    }
}

// ❌ DON'T DO THIS!
app.UseMiddleware<TenantMiddleware>();
```

### ✅ CORRECT: "I just call AddMultiTenancy()"

```csharp
// ✅ DO THIS!
builder.Services.AddMultiTenancy(builder.Configuration);
```

---

## Comparison: What You Implement vs What's Automatic

| Task                               | You Implement? | Shared Library Does? |
| ---------------------------------- | -------------- | -------------------- |
| Write middleware class             | ❌ No          | ✅ Yes               |
| Register middleware in pipeline    | ❌ No          | ✅ Yes               |
| Extract tenant ID from header      | ❌ No          | ✅ Yes               |
| Call Tenant Service API            | ❌ No          | ✅ Yes               |
| Implement caching                  | ❌ No          | ✅ Yes               |
| Enforce tenant validation          | ❌ No          | ✅ Yes               |
| Register ITenantContext            | ❌ No          | ✅ Yes               |
| **Call AddMultiTenancy()**         | **✅ YES**     | ❌ No                |
| **Use ITenantContext in handlers** | **✅ YES**     | ❌ No                |

**You only do 2 things:**

1. Call `AddMultiTenancy()` in Program.cs
2. Use `ITenantContext` in your handlers

---

## Services That Already Use This

### Identity Service

```csharp
// src/Services/Identity/Identity.API/Program.cs
builder.Services.AddMultiTenancy(builder.Configuration);
```

### Tenant Service

```csharp
// src/Services/Tenant/Tenant.API/Program.cs
// Multi-tenancy NOT used (Tenant Service is the provider, not a consumer)
// It always uses static configuration from appsettings.json:
// - Database: DatabaseSettings:ConnectionString
// - JWT: Jwt section
// - CORS: Cors section
// No MultiTenancy configuration needed in Tenant Service
```

### Your New Service

```csharp
// src/Services/Order/Order.API/Program.cs
builder.Services.AddMultiTenancy(builder.Configuration);
```

Same code. Same implementation. Zero custom middleware needed.

---

## Advanced: Customizing Tenant Resolution

### Default Behavior

The middleware extracts tenant ID from the `x-tenant-id` HTTP header.

### Custom Behavior (e.g., from subdomain)

If you need to extract tenant from subdomain instead of header:

1. **Modify the shared middleware** (affects ALL services):

   ```csharp
   // In IhsanDev.Shared.Infrastructure/Middleware/TenantResolutionMiddleware.cs

   // OLD (header-based):
   var tenantId = context.Request.Headers["x-tenant-id"].FirstOrDefault();

   // NEW (subdomain-based):
   var host = context.Request.Host.Host; // e.g., "customer123.myapp.com"
   var tenantId = host.Split('.')[0];    // Extract "customer123"
   ```

2. **Rebuild shared library**:

   ```bash
   dotnet build src/Shared/IhsanDev.Shared.Infrastructure/
   ```

3. **All services automatically use the new logic** (no service-specific changes needed)

---

## Testing Without the Middleware

In integration tests, you typically disable multi-tenancy:

```csharp
// CustomWebApplicationFactory.cs
protected override Dictionary<string, string?> GetTestConfiguration()
{
    var config = base.GetTestConfiguration();

    // Disable multi-tenancy for tests
    config["MultiTenancy:Enabled"] = "false";

    return config;
}
```

Then use `TenantTestHelper` to generate test data:

```csharp
var userId = TenantTestHelper.GenerateUniqueUserId();
var tenantId = TenantTestHelper.GenerateUniqueTenantId();
```

---

## Troubleshooting

### Issue: ITenantContext.CurrentTenant is always null

**Possible Causes**:

1. Multi-tenancy not enabled in configuration

   ```json
   { "MultiTenancy": { "Enabled": true } }
   ```

2. `x-tenant-id` header not sent in request

   ```bash
   curl -H "x-tenant-id: customer-123" https://localhost:5002/api/orders
   ```

3. Tenant doesn't exist in Tenant Service

   ```bash
   # Verify tenant exists
   curl https://localhost:5003/api/tenants/customer-123
   ```

4. Tenant Service is not running
   ```bash
   # Check Tenant Service is accessible
   curl https://localhost:5003/health
   ```

### Issue: "Cannot resolve ITenantContext"

**Solution**: Call `AddMultiTenancy()` in Program.cs:

```csharp
builder.Services.AddMultiTenancy(builder.Configuration);
```

### Issue: Middleware not intercepting requests

**Solution**: Make sure you're calling `AddMultiTenancy()`, not `UseMultiTenancy()`. The middleware is registered automatically:

```csharp
// ✅ Correct
builder.Services.AddMultiTenancy(builder.Configuration);

// ❌ Wrong - this method doesn't exist
app.UseMultiTenancy();
```

---

## Summary

### What You Need to Remember

1. **The tenant middleware is already implemented** in `IhsanDev.Shared.Infrastructure`
2. **You only need ONE LINE** to enable it: `builder.Services.AddMultiTenancy(configuration)`
3. **The middleware automatically**:
   - Intercepts requests
   - Extracts tenant ID from header
   - Calls Tenant Service
   - Caches tenant data
   - Sets ITenantContext
4. **You just use** `ITenantContext` in your handlers to access tenant data
5. **Multi-tenancy is optional** - works fine without it

### Quick Reference

```csharp
// Enable multi-tenancy (one line in Program.cs)
builder.Services.AddMultiTenancy(builder.Configuration);

// Access tenant in any handler
if (_tenantContext.HasTenant)
{
    var tenantId = _tenantContext.CurrentTenant.TenantId;
}
```

That's all you need to know! 🎉

---

## Related Documentation

- [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) - Complete guide
- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Comprehensive multi-tenancy docs
- [MULTI_TENANCY_QUICK_START.md](MULTI_TENANCY_QUICK_START.md) - Quick start guide

---

**Last Updated**: October 19, 2025  
**Status**: ✅ Complete and verified

**Built with ❤️ for the Microservices Architecture**
