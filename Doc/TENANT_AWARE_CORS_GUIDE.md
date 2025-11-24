# 🌐 Tenant-Aware CORS Configuration Guide

**Version:** 2.0.0  
**Status:** ✅ Production Ready  
**Last Updated:** November 13, 2025

## Overview

The microservices architecture now supports **tenant-aware CORS** configuration, allowing each tenant to have their own allowed origins with **merged mode** (base origins + tenant-specific origins combined).

### Key Features

- ✅ **Tenant-Specific Origins**: Each tenant can define their own allowed CORS origins
- ✅ **Merged Mode**: Base origins from appsettings.json are ALWAYS included, then merged with tenant-specific origins
- ✅ **Single-Tenant Mode**: Uses appsettings.json CORS when multi-tenancy is disabled
- ✅ **Dynamic Resolution**: CORS origins are resolved at request time based on tenant context
- ✅ **Middleware-Based**: Uses custom middleware for runtime CORS validation
- ✅ **Zero Configuration**: Works automatically when multi-tenancy is enabled
- ✅ **Swagger-Friendly**: Base origins ensure Swagger UI (http://localhost:5001) always works

---

## How It Works

### Request Flow

```
1. Request arrives with Origin header (with or without x-tenant-id)
   ↓
2. TenantMiddleware extracts tenant ID (if provided)
   ↓
3. TenantConfigurationProvider fetches tenant config (with caching, if tenant exists)
   ↓
4. TenantAwareCorsMiddleware validates Origin header
   ├─ Multi-tenancy ENABLED?
   │  ├─ Get base origins from appsettings.json (ALWAYS included)
   │  ├─ Get tenant origins from TenantInfo.Configuration.Cors.AllowedOrigins (if tenant exists)
   │  └─ Merge both using Union (base + tenant = combined list)
   └─ Multi-tenancy DISABLED?
      └─ Use ONLY appsettings.json Cors:AllowedOrigins
   ↓
5. If Origin is valid in combined list:
   ├─ Set Access-Control-Allow-Origin header
   └─ Set Access-Control-Allow-Credentials header
   ↓
6. Request continues to controller/endpoint
```

### Configuration Priority

**When Multi-Tenancy is ENABLED (`"Enabled": true`):**

- **Base origins ALWAYS included** - Read from `appsettings.json` → `Cors:AllowedOrigins`
- **Tenant origins merged** - Read from `TenantInfo.Configuration.Cors.AllowedOrigins` (if tenant exists)
- **Combined list** = Base origins ∪ Tenant origins (union, no duplicates)
- If tenant CORS config is missing → Uses only base origins (no error)
- **Swagger-friendly**: Base origins ensure development tools (http://localhost:5001) always work

**When Multi-Tenancy is DISABLED (`"Enabled": false`):**

- **ONLY application default CORS** - Read from `appsettings.json` → `Cors:AllowedOrigins`
- Tenant configuration is not used

---

## Configuration

### Application-Level CORS (appsettings.json)

This configuration is used:

- **Always when multi-tenancy is enabled** (as base origins, merged with tenant origins)
- **Exclusively when multi-tenancy is disabled** (`"MultiTenancy:Enabled": false`)

**Identity Service** (`appsettings.json`):

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5001",
      "https://localhost:5001",
      "https://localhost:5101",
      "http://localhost:4200",
      "http://localhost:3000",
      "https://myapp.com"
    ]
  }
}
```

**Purpose of Base Origins:**

- Development tools (Swagger UI at http://localhost:5001)
- Local development servers (Angular, React, etc.)
- Shared production domains that ALL tenants need

### Tenant-Specific CORS

Each tenant can define their own allowed origins in their configuration.

**Example Tenant Configuration**:

```json
{
  "tenantId": "company-abc",
  "tenantName": "ABC Corporation",
  "data": "{\"Jwt\":{\"Secret\":\"...\",\"Issuer\":\"CompanyABC\",\"Audience\":\"CompanyABCApp\"},\"Database\":{\"Provider\":\"PostgreSql\",\"ConnectionString\":\"...\"},\"Cors\":{\"AllowedOrigins\":[\"https://abc-company.com\",\"https://app.abc-company.com\"]}}"
}
```

When tenant `company-abc` makes a request, the allowed origins will be:

- **Base origins**: `["http://localhost:5001", "https://localhost:5001", "https://localhost:5101", ...]` (from appsettings.json)
- **Tenant origins**: `["https://abc-company.com", "https://app.abc-company.com"]` (from tenant config)
- **Combined**: All origins from both sources (union, no duplicates)

---

## Implementation

### Step 1: CORS Service Registration (Program.cs)

**Optional**: You can register CORS services if needed for other parts of your application, but the `TenantAwareCorsMiddleware` handles all CORS validation independently.

```csharp
// ============================================
// CORS Configuration (Optional)
// ============================================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
```

**Note:** The standard `UseCors()` middleware is **NOT** used. All CORS handling is done by `TenantAwareCorsMiddleware`.

### Step 2: Middleware Pipeline Configuration

The tenant-aware CORS middleware must run **after** tenant resolution. It handles both preflight (OPTIONS) and actual requests.

```csharp
app.UseGlobalExceptionHandler();
app.UseHttpsRedirection();

// Multi-tenancy middleware (must be before CORS and authentication)
// Only runs if MultiTenancy:Enabled is true
app.UseTenantResolution(builder.Configuration);

// Tenant-aware CORS (validates origins based on tenant config or appsettings)
// Must be after tenant resolution to access tenant context
// MUST be BEFORE JwtTenantVerification to handle OPTIONS preflight requests first
// This middleware handles ALL CORS including preflight requests
app.UseTenantAwareCors();

// JWT tenant verification (AFTER tenant resolution and CORS, BEFORE authentication)
// Prevents users from accessing other tenants by changing x-tenant-id header
app.UseJwtTenantVerification(builder.Configuration);

// Note: Standard UseCors() is NOT needed - TenantAwareCors handles everything

// ... rest of middleware
app.UseAuthentication();
app.UseAuthorization();
```

### Middleware Order (Critical!)

```
1. Exception Handler
2. HTTPS Redirection
3. Tenant Resolution ← Populates ITenantContext (skips OPTIONS requests)
4. Tenant-Aware CORS ← Validates origins and handles preflight
5. JWT Tenant Verification ← Validates JWT tenant claims
6. Database Migration
7. Authentication
8. Authorization
```

---

## Code Implementation

### TenantAwareCorsMiddleware

Located at: `src/Shared/IhsanDev.Shared.Infrastructure/Middleware/TenantAwareCorsMiddleware.cs`

**Key Features:**

- ✅ Validates origin against tenant-specific or appsettings.json configuration
- ✅ Allows all HTTP methods (GET, POST, PUT, DELETE, PATCH, OPTIONS)
- ✅ Allows all headers requested by the browser (dynamic header support)
- ✅ Handles preflight (OPTIONS) requests automatically
- ✅ Sets CORS headers immediately for actual requests

```csharp
public async Task InvokeAsync(
    HttpContext context,
    ITenantContext tenantContext,
    IConfiguration configuration)
{
    var origin = context.Request.Headers["Origin"].FirstOrDefault();

    if (!string.IsNullOrEmpty(origin))
    {
        var allowedOrigins = GetAllowedOrigins(configuration, tenantContext);

        if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            // Set CORS headers for all valid origins
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";

            // Handle preflight requests (OPTIONS)
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.Headers["Access-Control-Allow-Methods"] =
                    "GET, POST, PUT, DELETE, PATCH, OPTIONS";

                // Echo back requested headers (required when credentials are enabled)
                var requestedHeaders = context.Request.Headers["Access-Control-Request-Headers"].ToString();
                if (!string.IsNullOrEmpty(requestedHeaders))
                {
                    context.Response.Headers["Access-Control-Allow-Headers"] = requestedHeaders;
                }
                else
                {
                    context.Response.Headers["Access-Control-Allow-Headers"] =
                        "Content-Type, Authorization, X-Requested-With, x-tenant-id";
                }

                context.Response.Headers["Access-Control-Max-Age"] = "86400";
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            // For actual requests, ensure CORS headers are set even on error responses
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                {
                    context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                    context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                }
                return Task.CompletedTask;
            });
        }
        else
        {
            // Invalid origin - reject preflight requests
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("CORS policy: Origin not allowed");
                return;
            }
        }
    }

    await _next(context);
}

private static string[] GetAllowedOrigins(
    IConfiguration configuration,
    ITenantContext tenantContext)
{
    // Always get base origins from appsettings.json
    var appSettingsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? Array.Empty<string>();

    var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled");

    // If multi-tenancy enabled and tenant has CORS config, merge with base origins
    if (multiTenancyEnabled &&
        tenantContext.HasTenant &&
        tenantContext.CurrentTenant?.Configuration?.Cors?.AllowedOrigins?.Length > 0)
    {
        var tenantOrigins = tenantContext.CurrentTenant.Configuration.Cors.AllowedOrigins;

        // Merge base and tenant origins (union removes duplicates)
        return appSettingsOrigins.Union(tenantOrigins).ToArray();
    }

    // Fallback: Use only base origins (multi-tenancy disabled OR tenant has no CORS config)
    return appSettingsOrigins;
}
```

### Extension Method

Located at: `src/Shared/IhsanDev.Shared.Infrastructure/Extensions/MultiTenancyExtensions.cs`

```csharp
/// <summary>
/// Add tenant-aware CORS middleware to the request pipeline
/// This middleware validates CORS origins based on merged configuration:
/// - Base origins from appsettings.json (ALWAYS included)
/// - Tenant-specific origins from tenant config (if available)
/// Must be called AFTER UseTenantResolution()
/// Handles both preflight (OPTIONS) and actual requests
/// </summary>
public static IApplicationBuilder UseTenantAwareCors(
    this IApplicationBuilder app)
{
    return app.UseMiddleware<TenantAwareCorsMiddleware>();
}
```

---

## Testing

### Test Case 1: Request Without Tenant (Uses Base CORS)

**Request**:

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:4200" \
  -d '{"email": "user@example.com", "password": "Password123!"}'
```

**Expected Behavior**:

- CORS validation uses `appsettings.json` → `Cors:AllowedOrigins` (base origins only)
- Origin `http://localhost:4200` is allowed (if in config)
- No tenant-specific origins added

### Test Case 2: Request With Tenant (Uses Merged CORS)

**Request**:

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "Origin: https://abc-company.com" \
  -H "x-tenant-id: company-abc" \
  -d '{"email": "user@example.com", "password": "Password123!"}'
```

**Expected Behavior**:

- CORS validation uses **merged origins**:
  - Base origins from appsettings.json (e.g., http://localhost:5001, http://localhost:4200)
  - Tenant origins from company-abc config (e.g., https://abc-company.com, https://app.abc-company.com)
- Origin `https://abc-company.com` is allowed (from tenant config)
- Origin `http://localhost:5001` also allowed (from base config)

### Test Case 3: Swagger UI Request (Uses Base CORS)

**Scenario**: Developer opens Swagger UI at `http://localhost:5001/swagger`

**Expected Behavior**:

- Swagger UI sends API requests with `Origin: http://localhost:5001`
- Even if x-tenant-id header is missing or invalid, base origins are checked
- Origin `http://localhost:5001` is allowed (from appsettings.json base origins)
- **No CORS errors** - Developer can test APIs without tenant context

### Test Case 4: Tenant Without CORS Config (Uses Base CORS Only)

**Request**:

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:4200" \
  -H "x-tenant-id: tenant-without-cors" \
  -d '{"email": "user@example.com", "password": "Password123!"}'
```

**Expected Behavior**:

- Tenant doesn't have CORS configuration in their config
- Fallback to base origins from appsettings.json
- Origin `http://localhost:4200` is allowed (if in base config)
- **No error** - Base origins always provide fallback

### Test Case 5: Invalid Origin (Blocked)

**Request**:

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "Origin: https://malicious-site.com" \
  -H "x-tenant-id: company-abc" \
  -d '{"email": "user@abc-company.com", "password": "Password123!"}'
```

**Expected Behavior**:

- Origin `https://malicious-site.com` is not in tenant's allowed origins
- CORS headers are not set
- Browser blocks the response

---

## Creating Tenant with CORS Configuration

### Using Tenant Service API

**Endpoint**: `POST /api/admin/tenant`

```json
{
  "tenantId": "company-xyz",
  "tenantName": "XYZ Corporation",
  "userId": 1,
  "startDate": "2025-01-01T00:00:00Z",
  "expireDate": "2026-01-01T00:00:00Z",
  "data": "{\"Jwt\":{\"Secret\":\"tenant-specific-secret-key-min-256-bits\",\"Issuer\":\"XYZCorp\",\"Audience\":\"XYZApp\"},\"Database\":{\"Provider\":\"PostgreSql\",\"ConnectionString\":\"Host=localhost;Database=XYZ_DB;...\"},\"Cors\":{\"AllowedOrigins\":[\"https://xyz-corp.com\",\"https://admin.xyz-corp.com\",\"https://mobile-app.xyz-corp.com\"]}}"
}
```

### Tenant Configuration JSON Structure

```json
{
  "Jwt": {
    "Secret": "tenant-specific-secret-key-min-256-bits",
    "Issuer": "TenantIssuer",
    "Audience": "TenantAudience"
  },
  "Database": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=TenantDb;..."
  },
  "Cors": {
    "AllowedOrigins": [
      "https://tenant-app.com",
      "https://tenant-admin.com",
      "https://mobile.tenant-app.com"
    ]
  }
}
```

---

## Security Considerations

### 1. Origin Validation

- Origins are validated using case-insensitive comparison
- Exact match required (no wildcard support for security)
- Invalid origins don't receive CORS headers

### 2. Credentials Support

- `Access-Control-Allow-Credentials: true` is set for valid origins
- Required for cookie-based authentication

### 3. Preflight Requests

- OPTIONS requests are handled automatically
- Max age set to 24 hours (86400 seconds)
- Requested methods and headers are allowed

### 4. Tenant Isolation

- Each tenant's CORS settings are isolated
- Tenant A cannot access resources using Tenant B's origins
- Configuration changes are cached (5-minute TTL by default)

---

## Troubleshooting

### Issue: CORS Error Despite Correct Configuration

**Symptoms**:

```
Access to fetch at 'https://localhost:5001/api/auth/login' from origin
'https://myapp.com' has been blocked by CORS policy
```

**Solutions**:

1. **Check if multi-tenancy is enabled**:

   ```json
   {
     "MultiTenancy": {
       "Enabled": true
     }
   }
   ```

2. **Verify tenant has CORS configuration**:

   ```bash
   curl https://localhost:5002/api/tenant/config/company-abc
   ```

3. **Check if origin matches exactly**:

   - `https://myapp.com` ≠ `https://www.myapp.com`
   - `http://myapp.com` ≠ `https://myapp.com`
   - Ports must match: `http://localhost:3000` ≠ `http://localhost:4200`

4. **Verify middleware order**:

   - `UseTenantResolution()` before `UseTenantAwareCors()`
   - `UseTenantAwareCors()` before `UseCors()`

5. **Check tenant service is running**:
   ```bash
   curl https://localhost:5002/health
   ```

### Issue: Preflight Request Failing

**Symptoms**:

```
OPTIONS request returns 404 or 500
```

**Solutions**:

1. Ensure middleware handles OPTIONS requests
2. Check that `UseTenantAwareCors()` is before `UseCors()`
3. Verify CORS headers are set correctly

### Issue: Configuration Not Updating

**Symptoms**:

- Changed CORS configuration but still blocked

**Solutions**:

1. **Clear cache** (configuration cached for 5 minutes):

   ```csharp
   // In Tenant Service
   var tenantConfigProvider = app.Services.GetRequiredService<ITenantConfigurationProvider>();
   tenantConfigProvider.ClearCache("company-abc");
   ```

2. **Wait for cache expiration** (5 minutes by default)

3. **Restart the service** (forces cache clear)

---

## Best Practices

### 1. Production Configuration

**DO**:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://myapp.com",
      "https://www.myapp.com",
      "https://admin.myapp.com"
    ]
  }
}
```

**DON'T**:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "*", // ❌ Never use wildcard
      "http://localhost:3000" // ❌ No localhost in production
    ]
  }
}
```

### 2. Development vs Production

**Development** (`appsettings.Development.json`):

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:4200",
      "http://localhost:3000",
      "http://localhost:5173"
    ]
  }
}
```

**Production** (`appsettings.json`):

```json
{
  "Cors": {
    "AllowedOrigins": ["https://myapp.com", "https://www.myapp.com"]
  }
}
```

### 3. Tenant Onboarding

When creating a new tenant:

1. **Collect all their domains**:

   - Main application domain
   - Admin panel domain
   - Mobile app domain (if web-based)

2. **Include all variants**:

   - With/without `www`
   - Different subdomains

3. **Use HTTPS in production**:

   - Never allow HTTP origins in production

4. **Document tenant origins**:
   - Keep record of why each origin is allowed

---

## Performance Considerations

### Caching

- Tenant configurations are cached in memory (default: 5 minutes)
- CORS validation happens per request but uses cached tenant config
- Overhead: ~1-2ms per request for CORS validation

### Recommendations

- **Cache Duration**: 5-10 minutes (configurable via `MultiTenancy:CacheExpirationMinutes`)
- **Origin Count**: Keep tenant origin list under 20 entries
- **Network**: Ensure low latency to Tenant Service

---

## Migration Guide

### From Static CORS to Tenant-Aware CORS

**Before** (Static CORS):

```csharp
var corsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ...

app.UseCors();
```

**After** (Tenant-Aware CORS):

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ...

app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();  // ← New middleware
app.UseCors();
```

**Strict Mode Behavior**:

- When multi-tenancy is enabled: CORS MUST come from tenant configuration (no fallback)
- When multi-tenancy is disabled: CORS comes from `appsettings.json`
- Frontend applications must ensure proper tenant headers are sent

---

## Summary

### What You Get

- ✅ **Tenant-specific CORS origins** per tenant
- ✅ **Strict mode enforcement** - no fallback when multi-tenancy enabled
- ✅ **Single-tenant mode support** - uses appsettings.json when disabled
- ✅ **Runtime validation** based on tenant context
- ✅ **Dynamic header support** - allows any headers requested by browser
- ✅ **All HTTP methods supported** - GET, POST, PUT, DELETE, PATCH, OPTIONS
- ✅ **Secure by default** with exact origin matching
- ✅ **Performance optimized** with configuration caching
- ✅ **Proper preflight handling** for OPTIONS requests

### Key Points

1. **Middleware order matters**: Tenant resolution → Tenant-aware CORS (no standard UseCors needed)
2. **Strict mode**: When multi-tenancy enabled, tenant MUST have CORS config (no fallback)
3. **Single-tenant mode**: Uses appsettings.json when multi-tenancy is disabled
4. **Cache-aware**: Configuration changes may take up to 5 minutes to propagate
5. **Security first**: No wildcard origins, exact matching only
6. **Headers**: Dynamically allows all headers requested by the browser (required when using credentials)
7. **Methods**: All HTTP methods are allowed (GET, POST, PUT, DELETE, PATCH, OPTIONS)

### Important Notes

- ⚠️ **Do NOT use standard `UseCors()` middleware** - TenantAwareCorsMiddleware handles everything
- ✅ **Wildcard `*` cannot be used for headers** when `Access-Control-Allow-Credentials: true` is set
- ✅ **Middleware echoes back requested headers** from preflight requests for maximum compatibility

---

## Related Documentation

- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Comprehensive multi-tenancy guide
- [TENANT_MIDDLEWARE_EXPLAINED.md](TENANT_MIDDLEWARE_EXPLAINED.md) - How tenant middleware works
- [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) - Integrating new services

---

**Last Updated**: November 24, 2025  
**Status**: ✅ Production Ready & Tested

**Critical Fixes Applied:**

- ✅ **TenantMiddleware** now skips OPTIONS preflight requests before tenant validation
- ✅ **TenantAwareCorsMiddleware** uses `Response.OnStarting` callback to ensure CORS headers are set even on error responses (401, 403, 500, etc.)
- ✅ **Middleware order corrected** - UseTenantAwareCors() must be BEFORE UseJwtTenantVerification() to handle preflight requests first

**Built with ❤️ for Secure Multi-Tenant Architecture**
