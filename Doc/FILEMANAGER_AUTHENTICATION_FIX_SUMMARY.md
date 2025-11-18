# FileManager Authentication Fix Summary

**Date**: November 19, 2025  
**Service**: FileManager Service (Port 5005)  
**Issue**: Tenant users unable to upload files (401 Unauthorized)

---

## 🔍 Problem Statement

**Initial Issue**: User from tenant `ihsandev` could not upload files to FileManager service - received 401 Unauthorized error.

**Expected Behavior**:

- ✅ Tenant users with `x-tenant-id` header can upload to their tenant database
- ✅ Global SuperAdmin can upload to specific tenant database (with `tenantId` parameter)
- ✅ Global SuperAdmin can upload to global database (without `tenantId` parameter)

---

## 🐛 Root Causes Discovered

### 1. JWT Mode Mismatch

**Problem**: FileManager used `JwtMode: "Shared"` but Identity Service generates tokens with `JwtMode: "PerTenant"`

**Impact**: All tenant users got 401 Unauthorized because FileManager expected global JWT secret but received tenant-specific tokens

**Fix**: Changed `appsettings.Development.json`:

```json
{
  "MultiTenancy": {
    "JwtMode": "PerTenant" // Changed from "Shared"
  }
}
```

---

### 2. Wrong JWT Validation Pattern

**Problem**: JWT `OnMessageReceived` event used `ITenantContext` which is NOT populated yet during JWT validation

**Impact**: JWT validation couldn't determine tenant-specific secret, fell back to global secret incorrectly

**Fix**: Changed to use `ITenantConfigurationProvider` directly:

```csharp
// ❌ BEFORE (WRONG)
var tenantContext = context.HttpContext.RequestServices.GetService<ITenantContext>();
// tenantContext was null/empty!

// ✅ AFTER (CORRECT)
var provider = context.HttpContext.RequestServices
    .GetService<ITenantConfigurationProvider>();
var tenant = await provider.GetTenantConfigurationAsync(tenantId, ct);
```

---

### 3. Missing DbContext Fallback

**Problem**: DbContext threw `InvalidOperationException` when no tenant context for admin endpoints

**Impact**: Admin endpoints with `BypassTenantAttribute` failed with 400 Bad Request

**Fix**: Added fallback to global database:

```csharp
if (_tenantContext?.HasTenant != true ||
    _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
{
    // Use global database from appsettings.json
    connectionString = _configuration["DatabaseSettings:ConnectionString"];
}
```

---

### 4. Missing Global Database Migration

**Problem**: Only tenant-specific migration was running; global database tables never created

**Impact**: Admin endpoints without `tenantId` failed with `42P01: relation "FileManager" does not exist`

**Fix**: Implemented dual migration strategy:

```csharp
// Always migrate global database first
app.UseDefaultDatabaseMigration<FileManagerDbContext>();

// Then enable tenant-specific migrations
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<FileManagerDbContext>(config);
}
```

---

### 5. Required TenantId Parameter

**Problem**: Admin endpoints required `tenantId` parameter even when admin wanted to use global database

**Impact**: Admin couldn't save files to global database, only to specific tenant databases

**Fix**: Made `tenantId` optional and only set tenant context when provided:

```csharp
adminGroup.MapPost("/files", async (
    [FromQuery] string? tenantId, // ✅ Optional
    ITenantContext tenantContext,
    ITenantConfigurationProvider tenantConfigProvider) =>
{
    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        var tenant = await tenantConfigProvider
            .GetTenantConfigurationAsync(tenantId, ct);
        tenantContext.SetTenant(tenant);
    }
    // else: No tenant context, uses global database
});
```

---

### 6. SuperAdmin Role Not Allowed in Tenant Endpoints

**Problem**: Tenant endpoints only allowed `User` and `Admin` roles, excluded `SuperAdmin`

**Impact**: Global SuperAdmin couldn't access tenant endpoints even with valid `x-tenant-id` header

**Fix**: Added `SuperAdmin` to tenant endpoint roles:

```csharp
.RequireAuthorization(policy =>
    policy.RequireRole("User", "Admin", "SuperAdmin"))  // Added SuperAdmin
```

---

## ✅ Solutions Implemented

### 1. JWT Configuration Fix

**File**: `FileManager.API/appsettings.Development.json`

**Changes**:

- Changed `MultiTenancy:JwtMode` from `"Shared"` to `"PerTenant"`
- Ensures consistency with Identity Service

---

### 2. JWT Validation Pattern Fix

**File**: `FileManager.API/Program.cs` (Lines 105-165)

**Changes**:

- Changed `OnMessageReceived` event to use `ITenantConfigurationProvider`
- Reads `x-tenant-id` header directly from HttpContext
- Fetches tenant configuration without requiring populated ITenantContext
- Creates fresh `TokenValidationParameters` per request (tenant-specific OR global)
- Always explicitly sets global JWT params when no tenant header

**Key Code**:

```csharp
OnMessageReceived = context =>
{
    var tenantId = context.HttpContext.Request.Headers["x-tenant-id"]
        .FirstOrDefault();

    if (!string.IsNullOrEmpty(tenantId))
    {
        var provider = context.HttpContext.RequestServices
            .GetService<ITenantConfigurationProvider>();
        var tenant = provider.GetTenantConfigurationAsync(tenantId, ct)
            .GetAwaiter().GetResult();

        // Use tenant-specific JWT secret
        context.Options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(tenant.Configuration.Jwt.Secret)
            ),
            // ... other settings
        };
    }
    else
    {
        // ✅ CRITICAL: Always set global JWT params
        context.Options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(globalSecret)
            ),
            // ... other settings
        };
    }
}
```

---

### 3. DbContext Fallback Implementation

**File**: `FileManager.Infrastructure/Persistence/FileManagerDbContext.cs` (Lines 40-65)

**Changes**:

- Modified `OnConfiguring` to check if tenant context is available
- Falls back to global database from `appsettings.json` when no tenant context
- Supports both tenant-specific and global database operations

**Key Code**:

```csharp
if (multiTenancyEnabled)
{
    if (_tenantContext?.HasTenant != true ||
        _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
    {
        // Fallback to global database
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
```

---

### 4. Dual Migration Strategy

**File**: `FileManager.API/Program.cs` (Lines 383-390)

**Changes**:

- Always runs `UseDefaultDatabaseMigration` (global database)
- Then runs `UseTenantDatabaseMigration` if multi-tenancy enabled
- Ensures both global and tenant databases are created and migrated

**Key Code**:

```csharp
// ✅ CRITICAL: Always migrate global database first
app.UseDefaultDatabaseMigration<FileManagerDbContext>();

// Then enable tenant-specific migrations
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<FileManagerDbContext>(builder.Configuration);
}
```

---

### 5. Optional Tenant Context Endpoints

**File**: `FileManager.API/Endpoints/FileManagerEndpoints.cs` (Lines 53-300+)

**Changes**:

- Made `tenantId` parameter optional (`string?`)
- Added manual tenant resolution logic
- Only sets tenant context if `tenantId` is provided
- Falls back to global database when `tenantId` is omitted

**Key Code**:

```csharp
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
    // else: No tenant context, uses global database

    var command = new SaveFileCommand(file);
    var result = await mediator.Send(command, ct);
    return Results.Created($"/api/filemanager/admin/files/{result.Id}", result);
})
.WithMetadata(new BypassTenantAttribute())
.RequireAuthorization(policy =>
    policy.RequireRole("Service", "SuperAdmin"));
```

---

### 6. SuperAdmin Role Addition

**File**: `FileManager.API/Endpoints/FileManagerEndpoints.cs` (Lines 24-44)

**Changes**:

- Added `SuperAdmin` to tenant endpoint roles
- Allows global SuperAdmin to access tenant endpoints with `x-tenant-id` header

**Key Code**:

```csharp
tenantGroup.MapPost("/files", async (
    [FromForm] IFormFile file,
    IMediator mediator,
    CancellationToken ct) =>
{
    var command = new SaveFileCommand(file);
    var result = await mediator.Send(command, ct);
    return Results.Created($"/api/filemanager/files/{result.Id}", result);
})
.RequireAuthorization(policy =>
    policy.RequireRole("User", "Admin", "SuperAdmin"))  // ✅ Added SuperAdmin
.WithName("SaveFile");
```

---

## 🎯 Testing Results

### Scenario 1: Tenant User Upload (x-tenant-id header)

**Endpoint**: `POST /api/filemanager/files`  
**Headers**: `x-tenant-id: ihsandev`, `Authorization: Bearer <tenant-jwt>`  
**Result**: ✅ **SUCCESS** - File saved to ihsandev tenant database

### Scenario 2: Global SuperAdmin Upload to Specific Tenant

**Endpoint**: `POST /api/filemanager/admin/files?tenantId=ihsandev`  
**Headers**: `Authorization: Bearer <global-jwt>`  
**Result**: ✅ **SUCCESS** - File saved to ihsandev tenant database

### Scenario 3: Global SuperAdmin Upload to Global Database

**Endpoint**: `POST /api/filemanager/admin/files`  
**Headers**: `Authorization: Bearer <global-jwt>`  
**Result**: ✅ **SUCCESS** - File saved to global database

---

## 📚 Documentation Created

### 1. BYPASS_TENANT_ENDPOINTS_GUIDE.md

**Purpose**: Comprehensive guide for implementing admin/global endpoints  
**Content**:

- Critical concepts (JWT mode, validation pattern, DbContext fallback)
- Common pitfalls with solutions
- Implementation patterns with complete code examples
- Database migration strategy
- Testing examples
- Troubleshooting section

### 2. BYPASS_TENANT_QUICK_REFERENCE.md

**Purpose**: Quick checklist for developers  
**Content**:

- 5-step critical checklist
- Quick troubleshooting table
- Usage examples
- Links to complete guide

### 3. Updated: .github/copilot-instructions.md

**Changes**:

- Added "Admin Endpoints with BypassTenant" section
- Documented JWT mode consistency requirement
- Added JWT validation pattern warning
- Documented DbContext fallback pattern
- Added dual migration requirement
- Added optional tenant context pattern

### 4. Updated: Doc/00_START_HERE.md

**Changes**:

- Added BYPASS_TENANT_ENDPOINTS_GUIDE.md to "New to Project" section
- Added BYPASS_TENANT_QUICK_REFERENCE.md to "Need Something Specific" section
- Added to Core Architecture section in documentation structure

### 5. Updated: Doc/NEW_SERVICE_INTEGRATION_GUIDE.md

**Changes**:

- Added "Part 3: Admin Endpoints with Optional Tenant Context" section
- Updated table of contents
- Added troubleshooting entries for BypassTenant issues:
  - Issue 5: Admin Endpoints Return 400 Bad Request
  - Issue 6: JWT Validation Fails for Tenant Users in PerTenant Mode
- Added link to BYPASS_TENANT_ENDPOINTS_GUIDE.md in resources

---

## 🚀 Key Learnings

### 1. FileManager is Unique

- Identity Service: Always tenant-specific (no global operations)
- Notification Service: Two DbContexts (global queue + tenant history), separate concerns
- **FileManager Service**: One DbContext serving dual purpose (tenant + global), requires special handling

### 2. JWT Validation Timing is Critical

- `OnMessageReceived` event runs **BEFORE** `TenantMiddleware`
- `ITenantContext` is **NOT** populated yet during JWT validation
- Must use `ITenantConfigurationProvider` directly to fetch tenant config
- Must always explicitly set global JWT params as fallback

### 3. Admin Endpoints Require Special Care

- Need `BypassTenantAttribute` to skip tenant middleware
- Must make `tenantId` parameter optional
- DbContext must fall back to global database when no tenant context
- Must run both global and tenant database migrations

### 4. JwtMode Must Be Consistent

- ALL services must use same `MultiTenancy:JwtMode`
- Mismatch causes 401 Unauthorized for tenant users
- Identity Service dictates the mode (other services must match)

---

## ⚠️ Warnings for Future Services

### ❌ DON'T: Mismatch JwtMode Configuration

**Bad**:

```json
// Identity Service
{ "MultiTenancy": { "JwtMode": "PerTenant" } }

// Your Service
{ "MultiTenancy": { "JwtMode": "Shared" } }  // ❌ Will cause 401!
```

### ❌ DON'T: Use ITenantContext in OnMessageReceived

**Bad**:

```csharp
OnMessageReceived = context =>
{
    var tenantContext = context.HttpContext.RequestServices
        .GetService<ITenantContext>();  // ❌ Will be null!
}
```

### ❌ DON'T: Forget Global Database Migration

**Bad**:

```csharp
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<YourDbContext>(...);
    // ❌ Global DB never migrated!
}
```

### ❌ DON'T: Throw Exception in DbContext When No Tenant

**Bad**:

```csharp
if (_tenantContext?.HasTenant != true)
{
    throw new InvalidOperationException("Tenant required!");
    // ❌ Breaks admin endpoints!
}
```

---

## ✅ Best Practices Established

### 1. Always Check JwtMode Consistency

```bash
# Check Identity Service
cat src/Services/Identity/Identity.API/appsettings.Development.json | grep JwtMode

# Check Your Service
cat src/Services/YourService/YourService.API/appsettings.Development.json | grep JwtMode
```

### 2. Always Use ITenantConfigurationProvider for JWT Validation

```csharp
// In OnMessageReceived event
var provider = context.HttpContext.RequestServices
    .GetService<ITenantConfigurationProvider>();
var tenant = await provider.GetTenantConfigurationAsync(tenantId, ct);
```

### 3. Always Implement DbContext Fallback for Services with Admin Endpoints

```csharp
if (_tenantContext?.HasTenant != true ||
    _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
{
    // Fallback to global database
    connectionString = _configuration["DatabaseSettings:ConnectionString"];
}
```

### 4. Always Run Dual Migration for Services with Admin Endpoints

```csharp
app.UseDefaultDatabaseMigration<YourDbContext>(); // Global DB
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<YourDbContext>(config); // Tenant DBs
}
```

### 5. Always Make TenantId Optional for Admin Endpoints

```csharp
adminGroup.MapPost("/resource", async (
    [FromQuery] string? tenantId, // ✅ Optional
    ITenantContext tenantContext,
    ITenantConfigurationProvider tenantConfigProvider) =>
{
    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        var tenant = await tenantConfigProvider
            .GetTenantConfigurationAsync(tenantId, ct);
        tenantContext.SetTenant(tenant);
    }
});
```

---

## 📊 Impact Analysis

### Before Fix

- ❌ Tenant users: 401 Unauthorized
- ❌ Admin endpoints: 400 Bad Request
- ❌ Global database: Not migrated
- ❌ SuperAdmin: Can't access tenant endpoints

### After Fix

- ✅ Tenant users: Can upload to their tenant database
- ✅ Admin endpoints: Work with or without tenantId
- ✅ Global database: Created and migrated automatically
- ✅ SuperAdmin: Can access both tenant and admin endpoints

---

## 🔗 Related Documentation

- [BYPASS_TENANT_ENDPOINTS_GUIDE.md](BYPASS_TENANT_ENDPOINTS_GUIDE.md) - Complete implementation guide
- [BYPASS_TENANT_QUICK_REFERENCE.md](BYPASS_TENANT_QUICK_REFERENCE.md) - Quick checklist
- [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) - Service creation guide
- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Multi-tenancy overview
- [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Architecture guide

---

**Status**: ✅ **RESOLVED**  
**Last Updated**: November 19, 2025  
**Verified By**: User confirmed "it is working now"
