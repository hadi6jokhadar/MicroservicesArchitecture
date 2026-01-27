# DbContext OnConfiguring Requirement for Multi-Tenancy

## ⚠️ CRITICAL: Read This Before Creating New Services

**Last Updated**: January 27, 2026  
**Severity**: High - Service will fail at runtime if not implemented correctly

---

## 📋 Table of Contents

1. [The Problem](#the-problem)
2. [Root Cause](#root-cause)
3. [When You Need OnConfiguring](#when-you-need-onconfiguring)
4. [Implementation Pattern](#implementation-pattern)
5. [Service Examples](#service-examples)
6. [Quick Checklist](#quick-checklist)

---

## The Problem

**Symptom:**

```
System.InvalidOperationException: No database provider has been configured for this DbContext.
```

**When It Happens:**

- Service has `MultiTenancy:Enabled = true` in appsettings.json
- DbContext inherits from `BaseDbContext` (or any DbContext without `OnConfiguring`)
- First request to the service fails with "No database provider has been configured"

**Why It's Critical:**
This error means your service **cannot access the database at all**. All endpoints will fail.

---

## Root Cause

When `MultiTenancy:Enabled = true`, the `AddDatabaseContext<TContext>()` extension method intentionally **does NOT configure** the database provider during service registration:

```csharp
// From DatabaseExtensions.cs
if (multiTenancyEnabled)
{
    // When multi-tenancy is enabled, still register DbContext but allow OnConfiguring to resolve connection
    services.AddDbContext<TContext>((serviceProvider, options) =>
    {
        // Don't configure the provider here - let OnConfiguring handle it
        // This allows dynamic tenant-based database selection
    }, ServiceLifetime.Scoped);
}
```

**The Assumption:**
Your DbContext will implement `OnConfiguring` to dynamically configure the connection based on:

- Tenant context (if tenant-specific database)
- Global configuration (if global database or no tenant context)

**What Happens If You Don't:**

- `optionsBuilder.IsConfigured` returns `false`
- EF Core throws "No database provider has been configured"
- Service crashes on first database access

---

## When You Need OnConfiguring

### ✅ You MUST Implement OnConfiguring When:

1. **Multi-tenancy is enabled** (`MultiTenancy:Enabled = true`)
2. **AND** your DbContext inherits from `BaseDbContext` (which doesn't have `OnConfiguring`)
3. **OR** your service uses a global database but needs to support optional tenant context

### ❌ You DON'T Need OnConfiguring When:

1. **Multi-tenancy is disabled** (`MultiTenancy:Enabled = false` or not specified)
2. **AND** your service uses a simple, static database connection

---

## Implementation Pattern

### Basic Template (Global Database)

Use this for services that use a **single global database** for all tenants:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;

namespace YourService.Infrastructure.Persistence;

public class YourServiceDbContext : BaseDbContext
{
    private readonly IConfiguration? _configuration;
    private readonly ILogger<YourServiceDbContext>? _logger;

    public YourServiceDbContext(
        DbContextOptions<YourServiceDbContext> options,
        ICurrentUserService? currentUserService = null,
        IConfiguration? configuration = null,
        ILogger<YourServiceDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // DbSets here...

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // If already configured (from DI), skip
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        // Service ALWAYS uses the global database from appsettings.json
        // Even when multi-tenancy is enabled
        if (_configuration != null)
        {
            var connectionString = _configuration.GetValue<string>("DatabaseSettings:ConnectionString");
            var provider = _configuration.GetValue<string>("DatabaseSettings:Provider");

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                _logger?.LogDebug("Configuring {DbContext} with global database connection",
                    GetType().Name);

                if (provider?.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
                    optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(GetType().Assembly.GetName().Name);
                        npgsqlOptions.CommandTimeout(_configuration.GetValue<int>("DatabaseSettings:CommandTimeout", 30));
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: _configuration.GetValue<int>("DatabaseSettings:MaxRetryCount", 3),
                            maxRetryDelay: TimeSpan.FromSeconds(_configuration.GetValue<int>("DatabaseSettings:MaxRetryDelay", 30)),
                            errorCodesToAdd: null);
                    });
                }
                else if (provider?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
                {
                    optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                    {
                        sqliteOptions.MigrationsAssembly(GetType().Assembly.GetName().Name);
                        sqliteOptions.CommandTimeout(_configuration.GetValue<int>("DatabaseSettings:CommandTimeout", 30));
                    });
                }

                if (_configuration.GetValue<bool>("DatabaseSettings:EnableSensitiveDataLogging", false))
                {
                    optionsBuilder.EnableSensitiveDataLogging();
                }

                if (_configuration.GetValue<bool>("DatabaseSettings:EnableDetailedErrors", false))
                {
                    optionsBuilder.EnableDetailedErrors();
                }
            }
        }

        base.OnConfiguring(optionsBuilder);
    }

    // OnModelCreating here...
}
```

### Advanced Template (Tenant-Specific Database with Fallback)

Use this for services that support **per-tenant databases** but can fall back to global:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    // If already configured (from DI), skip
    if (optionsBuilder.IsConfigured)
    {
        base.OnConfiguring(optionsBuilder);
        return;
    }

    string? connectionString = null;
    string? provider = null;
    var multiTenancyEnabled = _configuration?.GetValue<bool>("MultiTenancy:Enabled", false) ?? false;

    if (multiTenancyEnabled)
    {
        // Check if tenant context exists and has database config
        if (_tenantContext?.HasTenant != true ||
            _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
        {
            // Use global database from appsettings.json as fallback
            _logger?.LogDebug("No tenant context - using global database");

            connectionString = _configuration?["DatabaseSettings:ConnectionString"];
            provider = _configuration?["DatabaseSettings:Provider"] ?? "PostgreSql";
        }
        else
        {
            // Use tenant-specific database
            var tenantDb = _tenantContext.CurrentTenant.Configuration.DatabaseSettings;
            connectionString = tenantDb.ConnectionString;
            provider = tenantDb.Provider ?? "PostgreSql";

            _logger?.LogInformation(
                "Using tenant-specific database for tenant: {TenantId}",
                _tenantContext.CurrentTenant.TenantId);
        }
    }
    else
    {
        // Multi-tenancy disabled - use global database
        connectionString = _configuration?["DatabaseSettings:ConnectionString"];
        provider = _configuration?["DatabaseSettings:Provider"] ?? "PostgreSql";
    }

    // Configure provider (PostgreSQL or SQLite)
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        if (provider?.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) == true)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
            optionsBuilder.UseNpgsql(connectionString, /* options */);
        }
        else if (provider?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            optionsBuilder.UseSqlite(connectionString, /* options */);
        }
    }

    base.OnConfiguring(optionsBuilder);
}
```

---

## Service Examples

### ✅ Services With OnConfiguring (Working)

#### 1. **Identity Service**

- **Config**: `MultiTenancy:Enabled` NOT set (defaults to false)
- **Pattern**: Implements `OnConfiguring` for tenant-specific + global fallback
- **File**: `Identity.Infrastructure/Persistence/IdentityDbContext.cs`

#### 2. **FileManager Service**

- **Config**: `MultiTenancy:Enabled = true`
- **Pattern**: Implements `OnConfiguring` for tenant-specific + global fallback
- **File**: `FileManager.Infrastructure/Persistence/FileManagerDbContext.cs`

#### 3. **Notification Service**

- **Config**: `MultiTenancy:Enabled = true`
- **Pattern**: Uses TWO DbContexts:
  - `NotificationDbContext` (global) - No `OnConfiguring` needed (registered separately)
  - `TenantNotificationDbContext` (per-tenant) - Has `OnConfiguring`
- **Files**: `Notification.Infrastructure/Persistence/`

#### 4. **Translation Service** (Fixed Jan 27, 2026)

- **Config**: `MultiTenancy:Enabled = true`
- **Pattern**: Global database with optional tenant context
- **Issue**: Was missing `OnConfiguring` - added it
- **File**: `Translation.Infrastructure/Persistence/TranslationDbContext.cs`

### ❌ Services Without OnConfiguring (Working Only If Multi-Tenancy Disabled)

#### 1. **Tenant Service**

- **Config**: No `MultiTenancy:Enabled` (never multi-tenant)
- **Pattern**: Simple global database, no `OnConfiguring` needed
- **Why It Works**: Multi-tenancy is never enabled for Tenant service

---

## Quick Checklist

Use this checklist when creating a **new service**:

### Step 1: Check appsettings.json

```json
{
  "MultiTenancy": {
    "Enabled": true // ← Is this true?
  }
}
```

### Step 2: If MultiTenancy:Enabled = true

- [ ] Does your DbContext inherit from `BaseDbContext`?
- [ ] Did you add constructor parameters for `IConfiguration` and `ILogger`?
- [ ] Did you implement `OnConfiguring` method?
- [ ] Does `OnConfiguring` check `optionsBuilder.IsConfigured`?
- [ ] Does `OnConfiguring` read connection string from `_configuration`?
- [ ] Does `OnConfiguring` configure PostgreSQL or SQLite provider?
- [ ] Did you test the service with and without `x-tenant-id` header?

### Step 3: If MultiTenancy:Enabled = false (or not set)

- [ ] You **DON'T** need `OnConfiguring`
- [ ] `AddDatabaseContext` will configure everything at startup

---

## Common Mistakes

### ❌ Mistake 1: Forgetting Constructor Parameters

```csharp
// WRONG - No IConfiguration injected
public MyDbContext(DbContextOptions<MyDbContext> options)
    : base(options)
{
}
```

```csharp
// CORRECT - Inject IConfiguration and ILogger
public MyDbContext(
    DbContextOptions<MyDbContext> options,
    IConfiguration? configuration = null,
    ILogger<MyDbContext>? logger = null)
    : base(options)
{
    _configuration = configuration;
    _logger = logger;
}
```

### ❌ Mistake 2: Not Checking IsConfigured

```csharp
// WRONG - Might override DI configuration
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.UseNpgsql(...); // Always configures, even if already configured
}
```

```csharp
// CORRECT - Check first
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    if (optionsBuilder.IsConfigured)
    {
        base.OnConfiguring(optionsBuilder);
        return;
    }

    // Configure here...
}
```

### ❌ Mistake 3: Null Configuration

```csharp
// WRONG - No null check
var connectionString = _configuration["DatabaseSettings:ConnectionString"];
optionsBuilder.UseNpgsql(connectionString); // Crashes if _configuration is null
```

```csharp
// CORRECT - Null checks
if (_configuration != null)
{
    var connectionString = _configuration.GetValue<string>("DatabaseSettings:ConnectionString");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        optionsBuilder.UseNpgsql(connectionString);
    }
}
```

---

## Testing Your Implementation

After implementing `OnConfiguring`, test these scenarios:

### Test 1: With x-tenant-id Header

```bash
# Should use tenant-specific database (if supported) or global database
curl -H "x-tenant-id: test-tenant" http://localhost:5006/api/yourservice
```

### Test 2: Without x-tenant-id Header

```bash
# Should use global database
curl http://localhost:5006/api/yourservice
```

### Test 3: Check Logs

```
# Good log:
Configuring YourServiceDbContext with global database connection

# Or (for tenant-specific):
Using tenant-specific database for tenant: test-tenant
```

### Test 4: Database Connection

- Verify the service can create tables (migrations run)
- Verify the service can read/write data
- Verify no "No database provider configured" errors

---

## Related Documentation

- [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Multi-tenancy architecture
- [BYPASS_TENANT_ENDPOINTS_GUIDE.md](BYPASS_TENANT_ENDPOINTS_GUIDE.md) - Optional tenant context
- [NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md](NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md) - Service creation guide

---

## Summary

**Golden Rule:**

> If `MultiTenancy:Enabled = true` and your DbContext inherits from `BaseDbContext`, you **MUST** implement `OnConfiguring`.

**Copy-Paste This:**

1. Add `IConfiguration` and `ILogger` to constructor
2. Implement `OnConfiguring` method
3. Check `optionsBuilder.IsConfigured` first
4. Read connection from `_configuration`
5. Configure PostgreSQL or SQLite provider
6. Test with and without `x-tenant-id` header

**Reference Implementation:**
See [Translation.Infrastructure/Persistence/TranslationDbContext.cs](../src/Services/Translation/Translation.Infrastructure/Persistence/TranslationDbContext.cs) for a complete working example.

---

**Version**: 1.0  
**Last Incident**: Translation Service (Jan 27, 2026) - Missing `OnConfiguring` caused runtime failure
