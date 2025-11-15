# 🔒 Multi-Tenancy Strict Mode - Behavior Changes

**Last Updated:** November 13, 2025  
**Status:** ⚠️ PARTIALLY OUTDATED (CORS behavior changed - see TENANT_AWARE_CORS_GUIDE.md)

---

## ⚠️ Important Notice

**CORS Behavior Changed:** This document describes the original strict mode where CORS must come from tenant config only. As of November 13, 2025, CORS now uses **merged mode** - base origins from appsettings.json are ALWAYS included and merged with tenant-specific origins.

**For current CORS behavior, see:** [TENANT_AWARE_CORS_GUIDE.md](TENANT_AWARE_CORS_GUIDE.md)

The rest of this document (Database and JWT strict behavior) remains accurate.

---

## Overview

The multi-tenancy system has been updated to enforce **strict separation** between multi-tenant and single-tenant modes. This document explains the behavior and requirements, with the exception of CORS which now uses merged mode.

---

## 🎯 Key Changes

### Before (Old Behavior)

```
✅ MultiTenancy Enabled + x-tenant-id header → Use tenant config
✅ MultiTenancy Enabled + NO x-tenant-id header → Fallback to appsettings.json
✅ MultiTenancy Disabled → Use appsettings.json
```

**Problem:** This allowed requests without tenant headers to work, which could lead to:

- Security issues (accessing wrong tenant's data)
- Configuration confusion (which settings are being used?)
- Debugging difficulties (why is default data being used?)

### After (New Behavior) ✅

```
✅ MultiTenancy Enabled + x-tenant-id header → Use tenant config (NO FALLBACK)
❌ MultiTenancy Enabled + NO x-tenant-id header → Return 400 Bad Request
✅ MultiTenancy Disabled → Use appsettings.json (x-tenant-id ignored)
```

**Benefits:**

- ✅ Clear separation of concerns
- ✅ Explicit error messages when tenant header is missing
- ✅ No accidental fallback to default configuration
- ✅ Easier to debug tenant-related issues

---

## ⚠️ Common Startup Error

### Problem: Database Initialization at Startup

**Error Message:**

```
System.InvalidOperationException: Multi-tenancy is enabled but tenant database configuration is not available.
Ensure x-tenant-id header is provided and tenant exists with valid database settings.
```

**Cause:**  
When multi-tenancy is enabled, database initialization (`InitializeDatabaseAsync`) at application startup fails because:

- No HTTP context exists yet (no request)
- No `x-tenant-id` header available
- DbContext requires tenant configuration

**Solution:**  
Skip startup database initialization when multi-tenancy is enabled. Use automatic per-request migration instead:

```csharp
// ❌ WRONG - Will fail with multi-tenancy enabled
if (app.Environment.IsDevelopment())
{
    await app.Services.InitializeDatabaseAsync<IdentityDbContext>(
        applyMigrations: true,
        seedData: true);
}

// ✅ CORRECT - Skip if multi-tenancy is enabled
if (app.Environment.IsDevelopment() && !builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false))
{
    await app.Services.InitializeDatabaseAsync<IdentityDbContext>(
        applyMigrations: true,
        seedData: true);
}
```

**Note:** Tenant databases will be initialized automatically on first request using `UseTenantDatabaseMigration<T>()` middleware.

---

## 📋 Detailed Behavior

### Mode 1: Multi-Tenancy ENABLED (`"MultiTenancy:Enabled": true`)

#### Requirements

1. **`x-tenant-id` header is MANDATORY**

   ```bash
   curl -X GET https://api.example.com/orders \
     -H "x-tenant-id: tenant-123" \
     -H "Authorization: Bearer {token}"
   ```

2. **Tenant must exist and be active**

   - Tenant must be registered in Tenant Service
   - Tenant `IsActive` must be `true`

3. **Tenant configuration must be complete**
   - Database connection string is required
   - JWT settings are required (for PerTenant mode)
   - CORS origins are optional (merged with base origins from appsettings.json)

#### Error Responses

| Scenario                     | HTTP Status                 | Error Message                                                                      |
| ---------------------------- | --------------------------- | ---------------------------------------------------------------------------------- |
| Missing `x-tenant-id` header | `400 Bad Request`           | "Multi-tenancy is enabled. The 'x-tenant-id' header is required for all requests." |
| Tenant not found             | `404 Not Found`             | "Tenant not found or inactive"                                                     |
| Tenant inactive              | `403 Forbidden`             | "Tenant is not active"                                                             |
| Missing database config      | `500 Internal Server Error` | "Multi-tenancy is enabled but tenant database configuration is not available."     |
| Missing JWT config           | `500 Internal Server Error` | "Multi-tenancy is enabled but tenant JWT configuration is not available."          |

#### Request Flow

```
1. Request arrives
   ↓
2. TenantMiddleware checks for x-tenant-id header
   ↓ (missing)
   ❌ Return 400 Bad Request

   ↓ (present)
3. Fetch tenant configuration from Tenant Service
   ↓ (not found)
   ❌ Return 404 Not Found

   ↓ (found)
4. Validate tenant is active
   ↓ (inactive)
   ❌ Return 403 Forbidden

   ↓ (active)
5. Set TenantContext
   ↓
6. Database context resolves tenant-specific connection
   ↓ (missing)
   ❌ Throw InvalidOperationException

   ↓ (configured)
7. Process request with tenant's database
   ↓
8. Return response
```

---

### Mode 2: Multi-Tenancy DISABLED (`"MultiTenancy:Enabled": false`)

#### Behavior

1. **`x-tenant-id` header is IGNORED**

   - Even if provided, it will not be used
   - No tenant resolution occurs

2. **All configuration from appsettings.json**

   - `DatabaseSettings:ConnectionString`
   - `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`
   - `Cors:AllowedOrigins`

3. **Single-tenant mode**
   - All requests use the same database
   - No tenant validation
   - No tenant-specific configuration

#### Request Flow

```
1. Request arrives (x-tenant-id header ignored)
   ↓
2. TenantMiddleware skips (multi-tenancy disabled)
   ↓
3. Database context uses appsettings.json connection
   ↓
4. JWT validation uses appsettings.json settings
   ↓
5. Process request with default database
   ↓
6. Return response
```

---

## 🔧 Configuration Examples

### Example 1: Multi-Tenancy Enabled (PerTenant JWT)

**appsettings.json (Identity Service)**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant",
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  }
}
```

**Tenant Configuration (in Tenant Service database)**

```json
{
  "tenantId": "acme-corp",
  "tenantName": "Acme Corporation",
  "isActive": true,
  "data": {
    "Database": {
      "Provider": "PostgreSql",
      "ConnectionString": "Host=db1.example.com;Database=acme_corp;Username=acme_user;Password=secure123"
    },
    "Jwt": {
      "Secret": "acme-corp-secret-key-minimum-32-characters",
      "Issuer": "IdentityService",
      "Audience": "MicroservicesApp",
      "AccessTokenExpirationMinutes": 21600,
      "RefreshTokenExpirationDays": 7
    },
    "Cors": {
      "AllowedOrigins": [
        "https://acme.example.com",
        "https://admin.acme.example.com"
      ]
    }
  }
}
```

**Request Example**

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "x-tenant-id: acme-corp" \
  -d '{
    "email": "john@acme.com",
    "password": "Password123!"
  }'
```

**What Happens:**

1. ✅ Tenant middleware extracts `x-tenant-id: acme-corp`
2. ✅ Fetches tenant configuration from Tenant Service
3. ✅ Database context connects to `db1.example.com/acme_corp`
4. ✅ Validates user credentials in Acme's database
5. ✅ Generates JWT using Acme's JWT secret
6. ✅ Returns token with `tenant_id: acme-corp` claim

---

### Example 2: Multi-Tenancy Enabled (Shared JWT)

**appsettings.json (Identity Service)**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "Shared",
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  },
  "Jwt": {
    "Secret": "shared-secret-for-all-tenants-minimum-32-characters",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 21600,
    "RefreshTokenExpirationDays": 7
  }
}
```

**Tenant Configuration (in Tenant Service database)**

```json
{
  "tenantId": "acme-corp",
  "tenantName": "Acme Corporation",
  "isActive": true,
  "data": {
    "Database": {
      "Provider": "PostgreSql",
      "ConnectionString": "Host=db1.example.com;Database=acme_corp;Username=acme_user;Password=secure123"
    },
    "Cors": {
      "AllowedOrigins": ["https://acme.example.com"]
    }
  }
}
```

**Note:** In Shared JWT mode, JWT settings come from appsettings.json, not tenant configuration.

---

### Example 3: Multi-Tenancy Disabled (Single Tenant)

**appsettings.json (Identity Service)**

```json
{
  "MultiTenancy": {
    "Enabled": false
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "your-secret-key-minimum-32-characters",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 21600,
    "RefreshTokenExpirationDays": 7
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:4200", "http://localhost:3000"]
  }
}
```

**Request Example**

```bash
# x-tenant-id header not required (ignored if provided)
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john@example.com",
    "password": "Password123!"
  }'
```

**What Happens:**

1. ✅ Tenant middleware skips (multi-tenancy disabled)
2. ✅ Database context uses connection from appsettings.json
3. ✅ Validates user credentials in default database
4. ✅ Generates JWT using appsettings.json JWT settings
5. ✅ Returns token (no tenant_id claim)

---

## 🧪 Testing Checklist

### Test Case 1: Multi-Tenancy Enabled - Valid Request

```bash
# Setup
MultiTenancy:Enabled = true
Tenant "test-tenant" exists and is active

# Test
curl -H "x-tenant-id: test-tenant" https://localhost:5001/api/auth/login

# Expected Result
✅ 200 OK with JWT token
✅ Token contains tenant_id claim: "test-tenant"
✅ Database: Connected to tenant-specific database
```

### Test Case 2: Multi-Tenancy Enabled - Missing Header

```bash
# Setup
MultiTenancy:Enabled = true

# Test
curl https://localhost:5001/api/auth/login
# (no x-tenant-id header)

# Expected Result
❌ 400 Bad Request
❌ Error: "Multi-tenancy is enabled. The 'x-tenant-id' header is required for all requests."
```

### Test Case 3: Multi-Tenancy Enabled - Tenant Not Found

```bash
# Setup
MultiTenancy:Enabled = true

# Test
curl -H "x-tenant-id: non-existent-tenant" https://localhost:5001/api/auth/login

# Expected Result
❌ 404 Not Found
❌ Error: "Tenant not found or inactive"
```

### Test Case 4: Multi-Tenancy Enabled - Tenant Inactive

```bash
# Setup
MultiTenancy:Enabled = true
Tenant "inactive-tenant" exists but IsActive = false

# Test
curl -H "x-tenant-id: inactive-tenant" https://localhost:5001/api/auth/login

# Expected Result
❌ 403 Forbidden
❌ Error: "Tenant is not active"
```

### Test Case 5: Multi-Tenancy Disabled - With Header

```bash
# Setup
MultiTenancy:Enabled = false

# Test
curl -H "x-tenant-id: any-tenant" https://localhost:5001/api/auth/login

# Expected Result
✅ 200 OK (header ignored)
✅ Token does NOT contain tenant_id claim
✅ Database: Connected to default database from appsettings.json
```

### Test Case 6: Multi-Tenancy Disabled - Without Header

```bash
# Setup
MultiTenancy:Enabled = false

# Test
curl https://localhost:5001/api/auth/login

# Expected Result
✅ 200 OK
✅ Token does NOT contain tenant_id claim
✅ Database: Connected to default database from appsettings.json
```

---

## 🔄 Migration Guide

### For Existing Deployments

If you have an existing deployment with multi-tenancy enabled:

#### Option 1: Keep Multi-Tenancy Enabled (Recommended)

**Action Required:**

1. Ensure all client applications send `x-tenant-id` header
2. Update API documentation to specify header requirement
3. Test all endpoints with valid tenant IDs

**Example Client Update (JavaScript):**

```javascript
// Before (header optional)
fetch("/api/orders", {
  headers: {
    Authorization: `Bearer ${token}`,
  },
});

// After (header required when MultiTenancy:Enabled = true)
fetch("/api/orders", {
  headers: {
    Authorization: `Bearer ${token}`,
    "x-tenant-id": "acme-corp", // ← Now required
  },
});
```

#### Option 2: Disable Multi-Tenancy

If you don't need multi-tenancy:

**Action Required:**

1. Set `"MultiTenancy:Enabled": false` in appsettings.json
2. Configure `DatabaseSettings`, `Jwt`, and `Cors` sections in appsettings.json
3. Remove `x-tenant-id` header from client applications (optional, as it will be ignored)

---

## 📚 Code Changes Summary

### Files Modified

1. **TenantMiddleware.cs**

   - Now returns `400 Bad Request` when `x-tenant-id` header is missing and multi-tenancy is enabled
   - Clear error message added

2. **IdentityDbContext.cs**

   - Removed fallback to appsettings.json when multi-tenancy is enabled
   - Throws `InvalidOperationException` if tenant database config is missing

3. **UserService.cs**

   - Removed fallback to appsettings.json JWT settings when multi-tenancy is enabled
   - Throws `InvalidOperationException` if tenant JWT config is missing (PerTenant mode)

4. **ConfigurationHelper.cs**

   - Updated `GetConfigValue()` to not fallback when multi-tenancy is enabled
   - Updated `GetJwtSettings()` to not fallback when multi-tenancy is enabled
   - Updated `GetCorsAllowedOrigins()` to not fallback when multi-tenancy is enabled

5. **Documentation**
   - Updated `MULTI_TENANCY_GUIDE.md` with new behavior
   - Updated `QUICK_REFERENCE.md` with warnings
   - Created this document (`MULTI_TENANCY_STRICT_MODE.md`)

---

## 🎯 Best Practices

### 1. Use Environment-Specific Configuration

**Development:**

```json
{
  "MultiTenancy": {
    "Enabled": false // Easier for local development
  }
}
```

**Production:**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant" // Maximum security
  }
}
```

### 2. Centralized Tenant ID Resolution

```csharp
// Create a middleware or extension method to extract tenant ID
public static class TenantExtensions
{
    public static string GetTenantId(this HttpContext context)
    {
        return context.Request.Headers["x-tenant-id"].FirstOrDefault()
            ?? throw new BadHttpRequestException("Tenant ID is required");
    }
}

// Usage
var tenantId = httpContext.GetTenantId();
```

### 3. Client-Side Error Handling

```typescript
// TypeScript example
async function apiCall(endpoint: string) {
  const response = await fetch(endpoint, {
    headers: {
      Authorization: `Bearer ${getToken()}`,
      "x-tenant-id": getTenantId(), // Always include
    },
  });

  if (response.status === 400) {
    const error = await response.json();
    if (error.message.includes("x-tenant-id")) {
      // Handle missing tenant header
      console.error("Tenant ID is required but not provided");
      // Redirect to tenant selection or login
    }
  }

  return response;
}
```

### 4. Testing with Tenant Headers

```csharp
// Integration test example
[Fact]
public async Task Login_WithoutTenantHeader_Returns400_WhenMultiTenancyEnabled()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act
    var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
        Email = "test@example.com",
        Password = "Password123!"
    });
    // No x-tenant-id header

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    Assert.Contains("x-tenant-id", error.Message);
}
```

---

## 🚨 Breaking Changes

### Summary

| Change                                | Impact                                        | Mitigation                                      |
| ------------------------------------- | --------------------------------------------- | ----------------------------------------------- |
| Missing `x-tenant-id` now returns 400 | Existing clients without header will fail     | Add header to all requests                      |
| No fallback to appsettings.json       | Requests without tenant config will fail      | Ensure all tenants have complete configuration  |
| Stricter validation                   | More error messages for invalid tenant config | Validate tenant configuration before activation |

### Backward Compatibility

**NOT backward compatible** when:

- `MultiTenancy:Enabled = true` AND
- Clients don't send `x-tenant-id` header

**Fully backward compatible** when:

- `MultiTenancy:Enabled = false`

---

## ❓ FAQ

### Q: Can I have some endpoints work without tenant header?

**A:** No. When `MultiTenancy:Enabled = true`, ALL requests must include `x-tenant-id` header. If you need public endpoints, consider:

1. Creating a separate public API service with multi-tenancy disabled
2. Using API Gateway to add tenant header for public endpoints
3. Disabling multi-tenancy for that specific service

### Q: What if I want to use a default tenant when header is missing?

**A:** This is intentionally not supported to avoid security issues and configuration confusion. You should:

1. Always send explicit tenant ID from client
2. Or disable multi-tenancy and use appsettings.json

### Q: Can I extract tenant ID from subdomain instead of header?

**A:** Yes! Modify `TenantMiddleware.cs` to extract tenant ID from subdomain:

```csharp
// Extract from subdomain instead of header
var host = context.Request.Host.Host; // e.g., "acme.example.com"
var tenantId = host.Split('.')[0]; // Extract "acme"
```

### Q: How do I test locally with multiple tenants?

**A:** Use the `x-tenant-id` header in your requests:

```bash
# Tenant 1
curl -H "x-tenant-id: tenant1" http://localhost:5001/api/auth/login

# Tenant 2
curl -H "x-tenant-id: tenant2" http://localhost:5001/api/auth/login
```

Or use Postman/Thunder Client collections with different tenant IDs.

---

## 📞 Support

For questions or issues:

1. Check this document first
2. Review `MULTI_TENANCY_GUIDE.md`
3. Check logs for error messages
4. Create a GitHub issue with:
   - Configuration (redact secrets)
   - Error message
   - Request headers
   - Expected vs actual behavior

---

**Last Updated:** October 29, 2025  
**Version:** 1.0.0  
**Status:** ✅ Production Ready
