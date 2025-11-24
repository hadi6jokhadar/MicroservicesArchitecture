# 🔧 CORS Error Response Fix

**Version:** 1.0.0  
**Date:** November 24, 2025  
**Status:** ✅ Fixed & Tested

## Problem

CORS errors occurred when API returned error responses (401, 403, 500, etc.) even though:

- The origin was in the allowed list
- OPTIONS preflight requests succeeded
- Successful requests worked correctly

### Error Messages

**Browser Console:**

```
Access to XMLHttpRequest at 'http://localhost:5001/api/auth/login' from origin
'http://localhost:4201' has been blocked by CORS policy: No 'Access-Control-Allow-Origin'
header is present on the requested resource.
```

**Application Log:**

```
warn: IhsanDev.Shared.Infrastructure.Middleware.TenantMiddleware[0]
      Multi-tenancy is enabled but x-tenant-id header is missing
```

### Root Causes

1. **OPTIONS Preflight Not Skipped** - `TenantMiddleware` was checking for tenant header even on OPTIONS requests before CORS could handle them
2. **Middleware Order Incorrect** - `UseJwtTenantVerification()` ran before `UseTenantAwareCors()`, blocking preflight requests
3. **CORS Headers Lost on Errors** - When middleware returned error responses, CORS headers set earlier were not included in the final response

---

## Solution

### Fix 1: Skip OPTIONS Requests in TenantMiddleware

**File:** `IhsanDev.Shared.Infrastructure/Middleware/TenantMiddleware.cs`

**Change:**

```csharp
public async Task InvokeAsync(
    HttpContext context,
    ITenantContext tenantContext,
    ITenantConfigurationProvider tenantConfigProvider,
    ILocalizationService localizationService)
{
    SetCultureFromRequest(context, localizationService);

    // ✅ NEW: Skip OPTIONS preflight requests (CORS) - they don't need tenant resolution
    if (context.Request.Method == "OPTIONS")
    {
        _logger.LogDebug("Skipping tenant resolution for OPTIONS preflight request");
        await _next(context);
        return;
    }

    // Check if multi-tenancy is enabled
    if (!tenantContext.IsMultiTenantMode)
    {
        // ... rest of logic
    }
    // ...
}
```

**Why:** OPTIONS preflight requests don't carry authentication headers or tenant context. They must be handled early by CORS middleware without tenant validation.

---

### Fix 2: Ensure CORS Headers on Error Responses

**File:** `IhsanDev.Shared.Infrastructure/Middleware/TenantAwareCorsMiddleware.cs`

**Change:**

```csharp
if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
{
    // Set CORS headers for all valid origins (both OPTIONS and actual requests)
    context.Response.Headers["Access-Control-Allow-Origin"] = origin;
    context.Response.Headers["Access-Control-Allow-Credentials"] = "true";

    // Handle preflight requests (OPTIONS)
    if (context.Request.Method == "OPTIONS")
    {
        // ... preflight handling
        return;
    }

    // ✅ NEW: For actual requests, ensure CORS headers are set even on error responses
    context.Response.OnStarting(() =>
    {
        // Re-apply CORS headers before sending response (in case they were cleared)
        if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }
        return Task.CompletedTask;
    });
}
```

**Why:** When middleware returns error responses (401, 403, etc.), the response headers can be cleared. The `OnStarting` callback ensures CORS headers are always present right before the response is sent to the client.

---

### Fix 3: Correct Middleware Order

**Files:**

- `Identity.API/Program.cs`
- `FileManager.API/Program.cs`
- `Notification.API/Program.cs`

**Before (Incorrect):**

```csharp
app.UseTenantResolution(builder.Configuration);
app.UseJwtTenantVerification(builder.Configuration);  // ❌ Runs before CORS
app.UseTenantAwareCors();
```

**After (Correct):**

```csharp
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();                             // ✅ Handles OPTIONS first
app.UseJwtTenantVerification(builder.Configuration);  // ✅ Runs after CORS
```

**Why:**

- `UseTenantAwareCors()` must run before `UseJwtTenantVerification()` to handle OPTIONS preflight requests
- OPTIONS requests don't include JWT tokens, so JWT verification would fail or skip them anyway
- CORS must validate and respond to preflight requests before any authentication logic

---

## Middleware Pipeline Order (Final)

```
1. UseGlobalExceptionHandler()
2. UseHttpsRedirection()
3. UseTenantResolution()          ← Extracts tenant ID (skips OPTIONS)
4. UseTenantAwareCors()           ← Validates origins & handles OPTIONS preflight ✅
5. UseJwtTenantVerification()     ← Verifies JWT tenant_id matches header
6. UseServiceAuthentication()     ← Service-to-service authentication
7. UseAuthentication()            ← JWT validation
8. UseAuthorization()             ← Role/policy checks
```

---

## Testing

### Test 1: Successful Login (200 OK)

**Request:**

```bash
curl -X POST "http://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:4201" \
  -d '{"email": "user@example.com", "password": "correct"}'
```

**Expected Response Headers:**

```
Access-Control-Allow-Origin: http://localhost:4201
Access-Control-Allow-Credentials: true
```

✅ **Result:** Works correctly

---

### Test 2: Failed Login (401 Unauthorized)

**Request:**

```bash
curl -X POST "http://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:4201" \
  -d '{"email": "user@example.com", "password": "wrong"}'
```

**Expected Response Headers:**

```
Access-Control-Allow-Origin: http://localhost:4201
Access-Control-Allow-Credentials: true
```

**Before Fix:** ❌ CORS headers missing - browser blocks response  
**After Fix:** ✅ CORS headers present - browser shows 401 error with body

---

### Test 3: OPTIONS Preflight Request

**Request:**

```bash
curl -X OPTIONS "http://localhost:5001/api/auth/login" \
  -H "Origin: http://localhost:4201" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: content-type"
```

**Expected Response:**

```
HTTP/1.1 204 No Content
Access-Control-Allow-Origin: http://localhost:4201
Access-Control-Allow-Methods: GET, POST, PUT, DELETE, PATCH, OPTIONS
Access-Control-Allow-Headers: content-type
Access-Control-Allow-Credentials: true
Access-Control-Max-Age: 86400
```

**Before Fix:** ❌ 400 Bad Request - tenant header missing  
**After Fix:** ✅ 204 No Content with CORS headers

---

## Impact

### Services Fixed

- ✅ Identity.API
- ✅ FileManager.API
- ✅ Notification.API

### Affected Scenarios

- ✅ Authentication failures (401)
- ✅ Authorization failures (403)
- ✅ Validation errors (400)
- ✅ Server errors (500)
- ✅ OPTIONS preflight requests
- ✅ Requests without x-tenant-id header (when OptionalTenant)

---

## Documentation Updated

1. ✅ `TENANT_AWARE_CORS_GUIDE.md` - Updated middleware order and added OnStarting callback
2. ✅ `JWT_TENANT_VERIFICATION_QUICK_SUMMARY.md` - Corrected middleware order
3. ✅ `JWT_TENANT_VERIFICATION_FIX_SUMMARY.md` - Updated critical order section
4. ✅ `FILE_MANAGER_TENANT_VS_GLOBAL_ENDPOINTS.md` - Fixed middleware sequence
5. ✅ `CORS_ERROR_RESPONSE_FIX.md` - This document (NEW)

---

## Key Takeaways

1. **OPTIONS requests must be handled early** - Skip tenant validation and JWT verification for preflight requests
2. **CORS headers must persist through errors** - Use `Response.OnStarting` callback to ensure headers are set
3. **Middleware order is critical** - CORS must run before authentication/authorization middleware
4. **Always test error scenarios** - Success paths often hide CORS issues that appear on errors

---

## Related Issues

- CORS errors only on failed authentication
- OPTIONS preflight returning 400/401 instead of 204
- "x-tenant-id header missing" warnings for OPTIONS requests
- CORS headers missing from error responses

---

**Status:** ✅ Fixed and deployed to all microservices  
**Tested:** Identity, FileManager, and Notification services  
**Production Ready:** Yes
