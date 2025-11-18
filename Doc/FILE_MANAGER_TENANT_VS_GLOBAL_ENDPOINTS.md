# 📁 FileManager: Tenant vs Global Endpoints Guide

**Last Updated:** November 19, 2025  
**Status:** ✅ Production Ready

---

## Overview

The FileManager service has **two separate sets of endpoints** to properly handle multi-tenancy authentication:

1. **Tenant Endpoints** (`/api/filemanager/*`) - For regular users within a specific tenant
2. **Global Admin Endpoints** (`/api/filemanager/admin/*`) - For SuperAdmin and Services across all tenants

This separation solves the authentication issue where tenant users couldn't access endpoints due to JWT validation against the wrong secret.

---

## The Problem (Before Split)

### Original Issue

```bash
# User from tenant "ihsandev" tries to upload file
POST http://localhost:5005/api/filemanager/files
Authorization: Bearer {jwt_for_ihsandev}  # Signed with ihsandev's secret
x-tenant-id: ihsandev

# Result: ❌ 401 Unauthorized
# Why? JWT validation happens before tenant resolution,
# so it tries to validate against global secret instead of tenant secret
```

### Root Cause

With `JwtMode: PerTenant`, JWT tokens are signed with **tenant-specific secrets**. However:

1. JWT validation middleware runs early in the pipeline
2. Tenant resolution happens in `TenantMiddleware`
3. By the time tenant is resolved, JWT validation already failed
4. Global endpoints need to bypass tenant resolution entirely

---

## The Solution: Separate Endpoints

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     TENANT USER FLOW                            │
├─────────────────────────────────────────────────────────────────┤
│ POST /api/filemanager/files                                     │
│ Authorization: Bearer {tenant_jwt}                              │
│ x-tenant-id: ihsandev          ← REQUIRED                       │
│                                                                 │
│ 1. TenantMiddleware resolves tenant                            │
│ 2. JwtTenantVerification validates tenant_id matches header    │
│ 3. JWT validated against TENANT's secret                       │
│ 4. Access tenant's database                                    │
│ 5. Upload file to tenant's storage                             │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                   GLOBAL USER FLOW                              │
├─────────────────────────────────────────────────────────────────┤
│ POST /api/filemanager/admin/files?tenantId=ihsandev            │
│ Authorization: Bearer {global_jwt}                              │
│ (NO x-tenant-id header)        ← OPTIONAL                       │
│                                                                 │
│ 1. BypassTenant attribute skips TenantMiddleware               │
│ 2. JWT validated against GLOBAL secret                         │
│ 3. Manually set tenant context if tenantId provided            │
│ 4. Access any tenant's database                                │
│ 5. Upload file to specified tenant's storage                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Endpoint Comparison

### Tenant Endpoints (`/api/filemanager/*`)

| Endpoint               | Method | Auth Required | Tenant Header | Description                  |
| ---------------------- | ------ | ------------- | ------------- | ---------------------------- |
| `/files`               | POST   | User, Admin   | ✅ Required   | Upload file to user's tenant |
| `/files/{id}`          | GET    | User, Admin   | ✅ Required   | Get file metadata            |
| `/files`               | GET    | User, Admin   | ✅ Required   | List files with filters      |
| `/files/{id}`          | PUT    | User, Admin   | ✅ Required   | Update file metadata         |
| `/files/{id}`          | DELETE | User, Admin   | ✅ Required   | Delete file                  |
| `/files/{id}/download` | GET    | Anonymous     | ❌ Optional   | Download file (public)       |

**Characteristics:**

- ✅ Require `x-tenant-id` header
- ✅ JWT must have `tenant_id` claim matching header
- ✅ JWT validated against **tenant's secret** (PerTenant mode)
- ✅ Automatic tenant database isolation
- ✅ JwtTenantVerification enforced

### Global Admin Endpoints (`/api/filemanager/admin/*`)

| Endpoint              | Method | Auth Required       | Tenant Header | Description                          |
| --------------------- | ------ | ------------------- | ------------- | ------------------------------------ |
| `/files?tenantId=xxx` | POST   | Service, SuperAdmin | ❌ Optional   | Upload file to any tenant            |
| `/files/{id}`         | GET    | Service, SuperAdmin | ❌ Optional   | Get file from any tenant             |
| `/files`              | GET    | Service, SuperAdmin | ❌ Optional   | List files across tenants            |
| `/files/{id}`         | PUT    | Service, SuperAdmin | ❌ Optional   | Update file in any tenant            |
| `/files/{id}`         | DELETE | Service, SuperAdmin | ❌ Optional   | Delete file from any tenant          |
| `/files/temp/all`     | DELETE | Service, SuperAdmin | ❌ Optional   | Delete all temp files (cross-tenant) |
| `/files/temp/old`     | DELETE | Service, SuperAdmin | ❌ Optional   | Delete old temp files (cross-tenant) |

**Characteristics:**

- ❌ `x-tenant-id` header **NOT required**
- ✅ JWT has **NO `tenant_id` claim** (global user)
- ✅ JWT validated against **global secret** (from appsettings.json)
- ✅ Use `[BypassTenant]` attribute to skip tenant middleware
- ✅ Can access any tenant by query parameter
- ✅ JwtTenantVerification allows (no tenant_id claim)

---

## Usage Examples

### Example 1: Tenant User Upload File ✅

**Scenario:** User A from tenant "ihsandev" uploads a profile picture

```bash
# Step 1: Login
POST http://localhost:5001/api/auth/login
Content-Type: application/json
x-tenant-id: ihsandev

{
  "email": "userA@ihsandev.com",
  "password": "Password123!"
}

# Response:
{
  "accessToken": "eyJ...",  # Contains tenant_id claim = "ihsandev"
  "tenantId": "ihsandev"
}

# Step 2: Upload file
POST http://localhost:5005/api/filemanager/files
Authorization: Bearer eyJ...
x-tenant-id: ihsandev
Content-Type: multipart/form-data

file: [binary data]
group: 1
userId: 123

# Response: ✅ 201 Created
{
  "id": 456,
  "name": "profile.jpg",
  "path": "/ihsandev/2025/11/profile_abc123.jpg",
  ...
}
```

---

### Example 2: Tenant User CANNOT Access Another Tenant ❌

**Scenario:** User A tries to upload to tenant "ihsanorg"

```bash
# User A's JWT is for tenant "ihsandev"
POST http://localhost:5005/api/filemanager/files
Authorization: Bearer eyJ...  # tenant_id = "ihsandev"
x-tenant-id: ihsanorg  # ❌ Different tenant!

# Response: ❌ 403 Forbidden
{
  "error": "Forbidden",
  "message": "Access denied. Your authentication token is for tenant 'ihsandev', but you are trying to access tenant 'ihsanorg'."
}
```

---

### Example 3: Global User (SuperAdmin) Upload to Any Tenant ✅

**Scenario:** SuperAdmin uploads file to tenant "ihsandev"

```bash
# Step 1: SuperAdmin login (no tenant)
POST http://localhost:5001/api/auth/login/admin
Content-Type: application/json

{
  "email": "admin@system.com",
  "password": "AdminPass123!"
}

# Response:
{
  "accessToken": "eyJ...",  # NO tenant_id claim
  "role": "SuperAdmin"
}

# Step 2: Upload file to tenant "ihsandev"
POST http://localhost:5005/api/filemanager/admin/files?tenantId=ihsandev
Authorization: Bearer eyJ...
Content-Type: multipart/form-data

file: [binary data]
group: 1
userId: 123

# Response: ✅ 201 Created
{
  "id": 789,
  "name": "admin_upload.pdf",
  "path": "/ihsandev/2025/11/admin_upload_xyz789.pdf",
  ...
}
```

**Note:** No `x-tenant-id` header needed! Tenant specified in query parameter.

---

### Example 4: Service-to-Service Call ✅

**Scenario:** Notification Service uploads image for a notification

```bash
# Service authentication (no JWT needed)
POST http://localhost:5005/api/filemanager/admin/files?tenantId=ihsandev
X-Service-Secret: shared-service-secret
X-Service-Name: NotificationService
Content-Type: multipart/form-data

file: [binary data]
group: 2
userId: 456

# Service middleware creates JWT with role=Service (no tenant_id)
# Response: ✅ 201 Created
```

---

## Authentication Flow Diagrams

### Tenant User Authentication Flow

```
┌──────────────┐
│ Client       │
└──────┬───────┘
       │ POST /api/filemanager/files
       │ Authorization: Bearer {tenant_jwt}
       │ x-tenant-id: ihsandev
       ▼
┌──────────────────────────────┐
│ TenantMiddleware             │
│ - Extracts x-tenant-id       │
│ - Fetches tenant config      │
│ - Sets tenant context        │
└──────┬───────────────────────┘
       ▼
┌──────────────────────────────┐
│ JwtTenantVerificationMiddleware │
│ - Verifies JWT tenant_id = ihsandev │
│ - Matches x-tenant-id = ihsandev    │
│ ✅ PASS                       │
└──────┬───────────────────────┘
       ▼
┌──────────────────────────────┐
│ JWT Authentication           │
│ - Validates signature        │
│ - Uses TENANT's secret       │
│ ✅ PASS                       │
└──────┬───────────────────────┘
       ▼
┌──────────────────────────────┐
│ Authorization                │
│ - Checks role: User/Admin    │
│ ✅ PASS                       │
└──────┬───────────────────────┘
       ▼
┌──────────────────────────────┐
│ Handler                      │
│ - Access tenant DB           │
│ - Upload file                │
│ - Return 201 Created         │
└──────────────────────────────┘
```

### Global User Authentication Flow

```
┌──────────────┐
│ Client       │
└──────┬───────┘
       │ POST /api/filemanager/admin/files?tenantId=ihsandev
       │ Authorization: Bearer {global_jwt}
       │ (NO x-tenant-id header)
       ▼
┌──────────────────────────────┐
│ TenantMiddleware             │
│ - Checks BypassTenant attribute │
│ ⏭️  SKIP (bypass)             │
└──────┬───────────────────────┘
       ▼
┌──────────────────────────────┐
│ JwtTenantVerificationMiddleware │
│ - JWT has NO tenant_id claim │
│ - User is SuperAdmin         │
│ ✅ PASS (global user)         │
└──────┬───────────────────────┘
       ▼
┌──────────────────────────────┐
│ JWT Authentication           │
│ - Validates signature        │
│ - Uses GLOBAL secret         │
│ ✅ PASS                       │
└──────┬───────────────────────┘
       ▼
┌──────────────────────────────┐
│ Authorization                │
│ - Checks role: SuperAdmin    │
│ ✅ PASS                       │
└──────┬───────────────────────┘
       ▼
┌──────────────────────────────┐
│ Handler                      │
│ - Manually set tenant context │
│ - Access specified tenant DB │
│ - Upload file                │
│ - Return 201 Created         │
└──────────────────────────────┘
```

---

## Implementation Details

### Tenant Endpoints Code

```csharp
// Tenant user endpoint - requires x-tenant-id header
group.MapPost("/files", async (
    [FromForm] IFormFile file,
    [FromForm] int? group,
    [FromForm] int? userId,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    // Automatic tenant context from TenantMiddleware
    var command = new SaveFileCommand(file, (Domain.Enums.FileGroup)group, userId);
    var result = await mediator.Send(command, cancellationToken);
    return Results.Created($"/api/filemanager/files/{result.Id}", result);
})
.RequireAuthorization(policy => policy.RequireRole("User", "Admin"))  // Tenant roles only
.WithName("SaveFile");
// No BypassTenant attribute - tenant middleware runs
```

### Global Admin Endpoints Code

```csharp
// Global admin endpoint - optional tenantId query parameter
var adminGroup = app.MapGroup("/api/filemanager/admin")
    .WithTags("FileManager - Admin");

adminGroup.MapPost("/files", async (
    [FromForm] IFormFile file,
    [FromForm] int? group,
    [FromForm] int? userId,
    [FromQuery] string? tenantId,  // Optional tenant target
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    // Manual tenant context if tenantId provided
    var command = new SaveFileCommand(file, (Domain.Enums.FileGroup)group, userId);
    var result = await mediator.Send(command, cancellationToken);
    return Results.Created($"/api/filemanager/admin/files/{result.Id}", result);
})
.RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))  // Global roles only
.WithName("SaveFileAdmin")
.WithMetadata(new BypassTenantAttribute());  // ← Skip tenant middleware
```

---

## Configuration Requirements

### appsettings.json (FileManager Service)

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant", // ← Tenant-specific JWT secrets
    "TenantServiceUrl": "https://localhost:5002"
  },
  "Jwt": {
    "Secret": "global-secret-for-superadmin-32-chars", // ← Global secret
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp"
  }
}
```

### Tenant Configuration (in Tenant Service DB)

```json
{
  "tenantId": "ihsandev",
  "configuration": {
    "jwt": {
      "secret": "ihsandev-tenant-secret-32-chars", // ← Tenant-specific secret
      "issuer": "IhsanDevIdentity",
      "audience": "IhsanDevApp"
    }
  }
}
```

---

## Middleware Order (Critical)

```csharp
// Program.cs
app.UseGlobalExceptionHandler();
app.UseTenantResolution(builder.Configuration);       // 1. Tenant resolution
app.UseJwtTenantVerification(builder.Configuration);  // 2. Verify tenant_id claim
app.UseTenantAwareCors();
app.UseServiceAuthentication();                       // 3. Service auth
app.UseAuthentication();                              // 4. JWT validation
app.UseAuthorization();                               // 5. Role checks
```

**Why This Order?**

1. **Tenant resolution first** - Sets tenant context for PerTenant JWT validation
2. **JWT tenant verification** - Ensures tenant_id matches header (security)
3. **Service authentication** - Creates service identity before JWT validation
4. **JWT authentication** - Validates token (uses tenant secret if available)
5. **Authorization** - Checks roles/policies

---

## Testing

### Test Case 1: Tenant User - Own Tenant ✅

```bash
curl -X POST http://localhost:5005/api/filemanager/files \
  -H "Authorization: Bearer {tenant_jwt_for_ihsandev}" \
  -H "x-tenant-id: ihsandev" \
  -F "file=@test.jpg" \
  -F "group=1"

# Expected: 201 Created
```

### Test Case 2: Tenant User - Wrong Tenant ❌

```bash
curl -X POST http://localhost:5005/api/filemanager/files \
  -H "Authorization: Bearer {tenant_jwt_for_ihsandev}" \
  -H "x-tenant-id: ihsanorg" \
  -F "file=@test.jpg"

# Expected: 403 Forbidden
```

### Test Case 3: Global User - Any Tenant ✅

```bash
curl -X POST "http://localhost:5005/api/filemanager/admin/files?tenantId=ihsandev" \
  -H "Authorization: Bearer {global_jwt}" \
  -F "file=@test.jpg" \
  -F "group=1"

# Expected: 201 Created
```

### Test Case 4: Service Call ✅

```bash
curl -X POST "http://localhost:5005/api/filemanager/admin/files?tenantId=ihsandev" \
  -H "X-Service-Secret: shared-service-secret" \
  -H "X-Service-Name: NotificationService" \
  -F "file=@test.jpg"

# Expected: 201 Created
```

---

## Migration Guide

### For Frontend Developers

**Before (Single Endpoint):**

```javascript
// All users used same endpoint
const response = await fetch("/api/filemanager/files", {
  method: "POST",
  headers: {
    Authorization: `Bearer ${token}`,
    "x-tenant-id": tenantId,
  },
  body: formData,
});
```

**After (Separate Endpoints):**

```javascript
// Tenant users
const uploadForTenantUser = async (token, tenantId, formData) => {
  return await fetch("/api/filemanager/files", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "x-tenant-id": tenantId, // ← Required
    },
    body: formData,
  });
};

// Global users (SuperAdmin/Service)
const uploadForGlobalUser = async (token, tenantId, formData) => {
  return await fetch(`/api/filemanager/admin/files?tenantId=${tenantId}`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      // No x-tenant-id header
    },
    body: formData,
  });
};

// Usage
if (userRole === "User" || userRole === "Admin") {
  await uploadForTenantUser(token, tenantId, formData);
} else if (userRole === "SuperAdmin" || userRole === "Service") {
  await uploadForGlobalUser(token, tenantId, formData);
}
```

---

## Troubleshooting

### Issue: Tenant User Getting 401 Unauthorized

**Symptom:**

```
POST /api/filemanager/files
→ 401 Unauthorized
```

**Causes:**

1. **Using wrong endpoint**

   - ❌ Using `/api/filemanager/admin/files` (global endpoint)
   - ✅ Should use `/api/filemanager/files` (tenant endpoint)

2. **Missing x-tenant-id header**

   - Tenant endpoints require this header
   - Add: `x-tenant-id: your-tenant-id`

3. **JWT signed with wrong secret**

   - JWT must be signed with tenant's secret (PerTenant mode)
   - Re-login to get correct JWT

4. **tenant_id claim doesn't match header**
   - JWT has `tenant_id: ihsandev`
   - But header has `x-tenant-id: ihsanorg`
   - Fix: Use matching tenant ID

---

### Issue: Global User Getting 400 Bad Request

**Symptom:**

```
POST /api/filemanager/admin/files
→ 400 Bad Request: "x-tenant-id header is required"
```

**Cause:**

- Using tenant endpoint `/api/filemanager/files` instead of admin endpoint

**Solution:**

- Use `/api/filemanager/admin/files` (no x-tenant-id header needed)
- Pass tenantId as query parameter if targeting specific tenant

---

## Summary

### Key Points

| Aspect               | Tenant Endpoints                 | Global Admin Endpoints                  |
| -------------------- | -------------------------------- | --------------------------------------- |
| **Path**             | `/api/filemanager/*`             | `/api/filemanager/admin/*`              |
| **Users**            | User, Admin (tenant roles)       | SuperAdmin, Service (global roles)      |
| **Header**           | `x-tenant-id` required           | No `x-tenant-id` (optional query param) |
| **JWT Secret**       | Tenant-specific (PerTenant mode) | Global (appsettings.json)               |
| **JWT Claim**        | Has `tenant_id` claim            | NO `tenant_id` claim                    |
| **Tenant Isolation** | Automatic (from header)          | Manual (from query param)               |
| **Bypass Tenant**    | ❌ No                            | ✅ Yes (`[BypassTenant]`)               |
| **Use Case**         | Regular user operations          | Cross-tenant admin operations           |

### Benefits

- ✅ **Security:** Proper JWT validation for each user type
- ✅ **Isolation:** Tenant users can only access their tenant
- ✅ **Flexibility:** Global users can access any tenant
- ✅ **Clear API:** Explicit separation of concerns
- ✅ **Maintainable:** Easy to understand and extend

---

**Implementation Date:** November 19, 2025  
**Status:** ✅ Production Ready  
**Breaking Changes:** Yes (endpoint paths changed for global users)
