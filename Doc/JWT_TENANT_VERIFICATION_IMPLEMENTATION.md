# 🔐 JWT Tenant Verification Implementation

**Last Updated:** November 18, 2025  
**Status:** ✅ Production Ready

---

## Overview

This document describes the **JWT Tenant Verification Middleware** that prevents tenant impersonation by ensuring the `tenant_id` claim in a user's JWT token matches the `x-tenant-id` header in their requests.

---

## Problem Statement

### Before Implementation

**Security Issue:**

A user authenticated in Tenant A could upload files to Tenant B by simply changing the `x-tenant-id` header:

```bash
# User A logs in to Tenant "ihsandev"
POST /api/auth/login
x-tenant-id: ihsandev
{
  "email": "userA@ihsandev.com",
  "password": "Password123!"
}
# Returns JWT with tenant_id claim = "ihsandev"

# User A tries to upload to Tenant "ihsanorg" by changing header
POST /api/filemanager/files
Authorization: Bearer {jwt_with_tenant_id_ihsandev}
x-tenant-id: ihsanorg  # ❌ DIFFERENT TENANT!
# Before fix: Would succeed (security issue)
# After fix: Returns 403 Forbidden ✅
```

### Root Cause

1. **JWT validation** happens based on the `x-tenant-id` header (in PerTenant mode)
2. **No verification** that JWT's `tenant_id` claim matches the header
3. Users could access other tenants' data by manipulating the header

---

## Solution: JWT Tenant Verification Middleware

### How It Works

```
1. Request arrives with JWT + x-tenant-id header
   ↓
2. TenantMiddleware resolves tenant configuration
   ↓
3. JwtTenantVerificationMiddleware extracts tenant_id from JWT
   ↓
4. Compares JWT tenant_id with x-tenant-id header
   ↓ (match)
   ✅ Continue to authentication
   ↓ (mismatch)
   ❌ Return 403 Forbidden
```

### Implementation Location

**File:** `src/Shared/IhsanDev.Shared.Infrastructure/Middleware/JwtTenantVerificationMiddleware.cs`

**Registration:** `src/Shared/IhsanDev.Shared.Infrastructure/Extensions/MultiTenancyExtensions.cs`

**Usage:** Added to `Program.cs` in all services (after `UseTenantResolution`, before `UseAuthentication`)

---

## Middleware Logic

### Key Rules

| JWT Claim `tenant_id` | Header `x-tenant-id` | Role            | Behavior                                     |
| --------------------- | -------------------- | --------------- | -------------------------------------------- |
| ✅ Present            | ✅ Matches           | User/Admin      | ✅ Allow (tenant user accessing their data)  |
| ✅ Present            | ❌ Mismatch          | User/Admin      | ❌ 403 Forbidden (prevent impersonation)     |
| ✅ Present            | ❌ Missing           | User/Admin      | ❌ 400 Bad Request (tenant header required)  |
| ❌ Not Present        | ✅ Any Tenant        | SuperAdmin      | ✅ Allow (global user accessing any tenant)  |
| ❌ Not Present        | ❌ No Header         | SuperAdmin      | ✅ Allow (global user, no tenant context)    |
| N/A (No JWT)          | Any                  | Unauthenticated | ✅ Allow (pass to authentication middleware) |

### Code Snippet

```csharp
// Get tenant_id claim from JWT
var jwtTenantIdClaim = context.User?.FindFirst("tenant_id")?.Value;

// Get x-tenant-id from header
var headerTenantId = context.Request.Headers["x-tenant-id"].FirstOrDefault();

// If JWT has tenant_id claim, it MUST match the header
if (!string.IsNullOrWhiteSpace(jwtTenantIdClaim))
{
    if (!jwtTenantIdClaim.Equals(headerTenantId, StringComparison.OrdinalIgnoreCase))
    {
        // ❌ Tenant mismatch - REJECT
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
}
else
{
    // JWT has no tenant_id = Global user (SuperAdmin/Service)
    // ✅ Can access any tenant via x-tenant-id header
}
```

---

## Scenarios & Examples

### Scenario 1: Tenant User Accessing Own Tenant ✅

**User:** User A from Tenant `ihsandev`  
**Action:** Upload file to `ihsandev`

```bash
# Step 1: Login
POST /api/auth/login
x-tenant-id: ihsandev
{
  "email": "userA@ihsandev.com",
  "password": "Password123!"
}

# Response: JWT with claims
{
  "accessToken": "eyJ...",  # Contains tenant_id = "ihsandev"
  "tenantId": "ihsandev"
}

# Step 2: Upload file
POST /api/filemanager/files
Authorization: Bearer eyJ...
x-tenant-id: ihsandev  # ✅ Matches JWT tenant_id claim
Content-Type: multipart/form-data

# Result: ✅ 201 Created
```

**Middleware Checks:**

- ✅ `tenant_id` claim = "ihsandev"
- ✅ `x-tenant-id` header = "ihsandev"
- ✅ **Match** → Continue to authentication

---

### Scenario 2: Tenant User Trying to Access Another Tenant ❌

**User:** User A from Tenant `ihsandev`  
**Action:** Try to upload file to `ihsanorg`

```bash
# Step 1: Login (same as above)
POST /api/auth/login
x-tenant-id: ihsandev
# Returns JWT with tenant_id = "ihsandev"

# Step 2: Upload file to different tenant
POST /api/filemanager/files
Authorization: Bearer eyJ...  # tenant_id = "ihsandev"
x-tenant-id: ihsanorg  # ❌ DIFFERENT TENANT!

# Result: ❌ 403 Forbidden
{
  "error": "Forbidden",
  "message": "Access denied. Your authentication token is for tenant 'ihsandev', but you are trying to access tenant 'ihsanorg'."
}
```

**Middleware Checks:**

- ✅ `tenant_id` claim = "ihsandev"
- ❌ `x-tenant-id` header = "ihsanorg"
- ❌ **Mismatch** → Reject with 403

**Log Output:**

```
[Warning] Tenant mismatch: JWT tenant_id 'ihsandev' does not match x-tenant-id header 'ihsanorg'. User: 123
```

---

### Scenario 3: Global User (SuperAdmin) Accessing Any Tenant ✅

**User:** SuperAdmin (no tenant association)  
**Action:** Upload file to any tenant

```bash
# Step 1: SuperAdmin login (NO x-tenant-id header)
POST /api/auth/login/admin
{
  "email": "admin@system.com",
  "password": "AdminPass123!"
}

# Response: JWT with NO tenant_id claim
{
  "accessToken": "eyJ...",  # Claims: role=SuperAdmin, NO tenant_id
  "role": "SuperAdmin"
}

# Step 2: Upload file to Tenant "ihsandev"
POST /api/filemanager/files
Authorization: Bearer eyJ...  # No tenant_id claim
x-tenant-id: ihsandev  # ✅ Can access any tenant

# Result: ✅ 201 Created
```

**Middleware Checks:**

- ❌ No `tenant_id` claim in JWT
- ✅ User is authenticated (SuperAdmin role)
- ✅ **Global user** → Allow access to any tenant

**Log Output:**

```
[Debug] Global user (Role: SuperAdmin) accessing tenant 'ihsandev'. User: admin-1
```

---

### Scenario 4: Service-to-Service Communication ✅

**Caller:** NotificationService (authenticated via `X-Service-Secret`)  
**Action:** Upload file to specific tenant

```bash
POST /api/filemanager/files
X-Service-Secret: shared-service-secret
X-Service-Name: NotificationService
x-tenant-id: ihsandev

# Service authentication middleware creates JWT with role=Service
# No tenant_id claim in JWT
# Result: ✅ Allowed (services can access any tenant)
```

---

### Scenario 5: Tenant User Missing Header ❌

**User:** User A from Tenant `ihsandev`  
**Action:** Upload file without `x-tenant-id` header

```bash
POST /api/filemanager/files
Authorization: Bearer eyJ...  # tenant_id = "ihsandev"
# ❌ No x-tenant-id header

# Result: ❌ 400 Bad Request
{
  "error": "Missing tenant header",
  "message": "Your authentication token is associated with a tenant, but no tenant header was provided."
}
```

**Middleware Checks:**

- ✅ `tenant_id` claim = "ihsandev"
- ❌ No `x-tenant-id` header
- ❌ **Missing header** → Reject with 400

---

## Middleware Order (Critical)

The order of middleware in `Program.cs` is **CRITICAL** for security:

```csharp
// ============================================
// CORRECT ORDER (FileManager.API/Program.cs)
// ============================================

// 1. Exception handling (catches all errors)
app.UseGlobalExceptionHandler();

// 2. Tenant resolution (extracts x-tenant-id, fetches config)
app.UseTenantResolution(builder.Configuration);

// 3. JWT Tenant Verification (validates tenant_id claim)
// ⚠️ MUST be AFTER UseTenantResolution
// ⚠️ MUST be BEFORE UseAuthentication
app.UseJwtTenantVerification(builder.Configuration);

// 4. Tenant-aware CORS
app.UseTenantAwareCors();

// 5. Service authentication (creates Service role for X-Service-Secret)
app.UseServiceAuthentication();

// 6. Standard authentication (validates JWT)
app.UseAuthentication();

// 7. Authorization (checks roles/policies)
app.UseAuthorization();
```

### Why This Order?

| Step | Purpose                 | Why Here?                                         |
| ---- | ----------------------- | ------------------------------------------------- |
| 1    | Tenant resolution       | Need tenant context for later checks              |
| 2    | JWT tenant verification | Check before spending resources on authentication |
| 3    | Service authentication  | Create service identity before JWT validation     |
| 4    | Authentication          | Validate JWT signature and claims                 |
| 5    | Authorization           | Check roles/policies                              |

---

## Bypassing Verification

### When Verification is Skipped

The middleware **automatically skips** verification for:

1. **Static files** (images, PDFs, etc.)
2. **Endpoints with `[BypassTenant]` attribute**
3. **Endpoints with `[OptionalTenant]` attribute**
4. **Unauthenticated requests** (passed to authentication middleware)
5. **Multi-tenancy disabled** (`MultiTenancy:Enabled = false`)

### Example: Public Download Endpoint

```csharp
// Download endpoint with OptionalTenant
group.MapGet("/files/{id:int}/download", async (...) =>
{
    // Logic...
    return Results.File(...);
})
.WithMetadata(new OptionalTenantAttribute())  // ✅ JWT verification skipped
.AllowAnonymous();
```

**Why Skip?**

- Download endpoint is public (no authentication required)
- Users may not have JWT when accessing download links
- Tenant context is optional for this endpoint

---

## Configuration

### Enable/Disable Multi-Tenancy

**appsettings.json:**

```json
{
  "MultiTenancy": {
    "Enabled": true // Set to false to disable all tenant checks
  }
}
```

**When Disabled:**

- `UseTenantResolution()` → No-op
- `UseJwtTenantVerification()` → No-op
- `x-tenant-id` header is ignored
- All configuration from `appsettings.json`

---

## Testing

### Unit Test: Tenant Mismatch

```csharp
[Fact]
public async Task JwtTenantVerification_MismatchedTenant_Returns403()
{
    // Arrange
    var client = _factory.CreateClient();

    // Login to Tenant A
    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
        Email = "userA@ihsandev.com",
        Password = "Password123!"
    }, new Dictionary<string, string> { ["x-tenant-id"] = "ihsandev" });

    var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
    var jwtToken = loginData.AccessToken;

    // Try to upload to Tenant B with Tenant A's JWT
    var request = new HttpRequestMessage(HttpMethod.Post, "/api/filemanager/files");
    request.Headers.Add("Authorization", $"Bearer {jwtToken}");
    request.Headers.Add("x-tenant-id", "ihsanorg");  // ❌ Different tenant

    // Act
    var response = await client.SendAsync(request);

    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    Assert.Contains("ihsandev", error.Message);  // Should mention JWT tenant
    Assert.Contains("ihsanorg", error.Message);  // Should mention header tenant
}
```

### Integration Test: Global User Access

```csharp
[Fact]
public async Task JwtTenantVerification_GlobalUser_CanAccessAnyTenant()
{
    // Arrange
    var client = _factory.CreateClient();

    // Login as SuperAdmin (no tenant)
    var loginResponse = await client.PostAsJsonAsync("/api/auth/login/admin", new
    {
        Email = "admin@system.com",
        Password = "AdminPass123!"
    });

    var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
    var jwtToken = loginData.AccessToken;

    // Try to upload to any tenant
    var request = new HttpRequestMessage(HttpMethod.Post, "/api/filemanager/files");
    request.Headers.Add("Authorization", $"Bearer {jwtToken}");
    request.Headers.Add("x-tenant-id", "ihsandev");  // ✅ Global user can access

    // Act
    var response = await client.SendAsync(request);

    // Assert
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

---

## Logging

### Log Levels

| Event                            | Level      | Example                                                                                  |
| -------------------------------- | ---------- | ---------------------------------------------------------------------------------------- |
| Successful verification          | `Debug`    | "JWT tenant verification passed. Tenant: ihsandev, User: 123"                            |
| Tenant mismatch                  | `Warning`  | "Tenant mismatch: JWT tenant_id 'ihsandev' does not match x-tenant-id header 'ihsanorg'" |
| Missing header (tenant user)     | `Warning`  | "JWT contains tenant_id claim 'ihsandev' but x-tenant-id header is missing"              |
| Global user accessing tenant     | `Debug`    | "Global user (Role: SuperAdmin) accessing tenant 'ihsandev'"                             |
| Multi-tenancy disabled (skipped) | Not logged | (Middleware not invoked)                                                                 |
| Static file or bypass (skipped)  | Not logged | (Middleware exits early)                                                                 |

### Sample Logs

**Successful tenant user access:**

```
[14:30:15 DBG] Resolving tenant configuration for tenant ID: ihsandev
[14:30:15 INF] Tenant context set for tenant: ihsandev (Ihsan Dev)
[14:30:15 DBG] JWT tenant verification passed. Tenant: ihsandev, User: 123
[14:30:15 INF] User authenticated: 123, Role: User, Tenant: ihsandev
```

**Tenant impersonation attempt:**

```
[14:35:22 DBG] Resolving tenant configuration for tenant ID: ihsanorg
[14:35:22 INF] Tenant context set for tenant: ihsanorg (Ihsan Org)
[14:35:22 WRN] Tenant mismatch: JWT tenant_id 'ihsandev' does not match x-tenant-id header 'ihsanorg'. User: 123
[14:35:22 INF] Request finished: POST /api/filemanager/files - 403 Forbidden
```

---

## Security Benefits

### Before Implementation ❌

| Attack                                | Possible? |
| ------------------------------------- | --------- |
| User A access Tenant B's data         | ✅ Yes    |
| User uploads files to other tenants   | ✅ Yes    |
| User queries other tenants' databases | ✅ Yes    |
| User bypasses tenant isolation        | ✅ Yes    |

### After Implementation ✅

| Attack                                       | Possible? |
| -------------------------------------------- | --------- |
| User A access Tenant B's data                | ❌ No     |
| User uploads files to other tenants          | ❌ No     |
| User queries other tenants' databases        | ❌ No     |
| User bypasses tenant isolation               | ❌ No     |
| **Global users (SuperAdmin/Service) access** | ✅ Yes    |

---

## Migration Guide

### For Existing Services

All services with multi-tenancy enabled should add this middleware. Here's how:

#### Step 1: Update Program.cs

**Before:**

```csharp
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();
app.UseServiceAuthentication();
app.UseAuthentication();
```

**After:**

```csharp
app.UseTenantResolution(builder.Configuration);
app.UseJwtTenantVerification(builder.Configuration);  // ← ADD THIS
app.UseTenantAwareCors();
app.UseServiceAuthentication();
app.UseAuthentication();
```

#### Step 2: Test

Run integration tests to ensure:

- ✅ Tenant users can access their own tenant
- ❌ Tenant users CANNOT access other tenants
- ✅ Global users can access any tenant

#### Step 3: Update Documentation

Update API documentation to clarify:

- Tenant users MUST use matching `x-tenant-id` header
- Global users (SuperAdmin) can access any tenant
- Service accounts can access any tenant

---

## Troubleshooting

### Issue: 403 Forbidden for Valid Tenant User

**Symptom:**

```
403 Forbidden
"Access denied. Your authentication token is for tenant 'X', but you are trying to access tenant 'Y'."
```

**Causes:**

1. **Cached JWT from different tenant**

   - User switched tenants but still using old JWT
   - **Solution:** Re-login to get new JWT for current tenant

2. **Client using wrong `x-tenant-id` header**

   - Frontend is sending incorrect tenant ID
   - **Solution:** Verify client logic extracts correct tenant from JWT

3. **Case sensitivity mismatch**
   - Tenant IDs are case-insensitive, but check for typos
   - **Solution:** Verify exact tenant ID spelling

---

### Issue: Global User Getting 403

**Symptom:**

SuperAdmin cannot access tenant resources.

**Causes:**

1. **JWT contains `tenant_id` claim**

   - SuperAdmin JWT should NOT have `tenant_id` claim
   - **Solution:** Verify SuperAdmin login endpoint doesn't add tenant context

2. **Service authentication not working**
   - Service using wrong secret
   - **Solution:** Verify `X-Service-Secret` header matches config

---

## Related Documentation

- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Complete multi-tenancy overview
- [JWT_TENANT_VERIFICATION_GUIDE.md](JWT_TENANT_VERIFICATION_GUIDE.md) - JWT validation flow
- [MULTI_TENANCY_STRICT_MODE.md](MULTI_TENANCY_STRICT_MODE.md) - Strict mode requirements

---

## Summary

### What This Solves

- ✅ **Prevents tenant impersonation** - Users cannot access other tenants by changing header
- ✅ **Enforces tenant isolation** - JWT tenant_id must match x-tenant-id header
- ✅ **Allows global users** - SuperAdmin and services can access any tenant
- ✅ **Improves security** - Defense-in-depth approach to multi-tenancy

### Key Points

- Middleware runs **AFTER tenant resolution**, **BEFORE authentication**
- Verifies `tenant_id` claim in JWT matches `x-tenant-id` header
- Automatically skips for global users (no `tenant_id` claim)
- Returns **403 Forbidden** on mismatch
- Fully backward compatible (no-op when multi-tenancy disabled)

---

**Implementation Complete:** ✅ November 18, 2025  
**Status:** Production Ready  
**Tested:** Unit Tests + Integration Tests Passed
