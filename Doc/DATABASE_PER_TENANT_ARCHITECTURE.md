# 🗄️ Database-Per-Tenant Architecture Guide

## Multi-Tenant Architecture: One Identity Service, Multiple Databases

**TL;DR: You have ONE Identity Service that dynamically routes users to different databases based on their tenant configuration. Each tenant has its own isolated database.**

---

## 📋 Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [How It Works](#how-it-works)
3. [Database Strategy](#database-strategy)
4. [Identity Service Configuration](#identity-service-configuration)
5. [Tenant Service Role](#tenant-service-role)
6. [Request Flow](#request-flow)
7. [Implementation Details](#implementation-details)
8. [Project Isolation Revisited](#project-isolation-revisited)
9. [Security & Isolation](#security--isolation)
10. [Best Practices](#best-practices)

---

## Architecture Overview

### **Your Current Architecture**

```
┌─────────────────────────────────────────────────────────────────┐
│                    IDENTITY SERVICE (ONE)                        │
│                                                                   │
│  • ONE codebase, ONE deployment                                  │
│  • Routes users to DIFFERENT databases based on tenant          │
│  • Fetches tenant config from Tenant Service                    │
└──────────────────┬────────────────────────────────────────────── ┘
                   │
                   │ Fetches tenant config (including DB connection)
                   │
                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                    TENANT SERVICE (ONE)                          │
│                                                                   │
│  Stores tenant configurations:                                   │
│  ┌──────────────────────────────────────────────────────┐       │
│  │ Tenant 123:                                          │       │
│  │   DatabaseConnectionString: "Server=db1;Database=..." │       │
│  │   IsActive: true                                     │       │
│  ├──────────────────────────────────────────────────────┤       │
│  │ Tenant 456:                                          │       │
│  │   DatabaseConnectionString: "Server=db2;Database=..." │       │
│  │   IsActive: true                                     │       │
│  └──────────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        │                     │
        ▼                     ▼
┌──────────────┐      ┌──────────────┐
│  Database 1  │      │  Database 2  │
│              │      │              │
│  tenant_123  │      │  tenant_456  │
│  • Users     │      │  • Users     │
│  • Orders    │      │  • Orders    │
│  • Products  │      │  • Products  │
└──────────────┘      └──────────────┘
```

**Key Points:**

- ✅ **ONE Identity Service** (shared code, single deployment)
- ✅ **Multiple Databases** (one per tenant, or grouped)
- ✅ **Dynamic Routing** (based on tenant ID in request)
- ✅ **Complete Data Isolation** (tenant 123 cannot see tenant 456 data)

---

## How It Works

### **Step-by-Step Flow**

#### **1. User Registration (New Tenant)**

```
1. User visits registration page
   → Email: john@acme.com
   → Password: SecurePass123
   → Company: Acme Corp

2. Identity Service:
   → Creates tenant in Tenant Service
   → Tenant Service creates new database (or uses shared DB with schema)
   → Returns: TenantId: 123

3. Identity Service:
   → Fetches tenant config (includes DB connection string)
   → Connects to Tenant 123's database
   → Creates user record: john@acme.com
   → Returns: JWT token with TenantId: 123
```

#### **2. User Login (Existing Tenant)**

```
1. User visits login page
   → Email: john@acme.com
   → Password: SecurePass123
   → TenantId: 123 (from subdomain or header)

2. Identity Service:
   → Receives TenantId: 123
   → Calls Tenant Service: GET /api/tenant/config/123
   → Gets response:
      {
        "tenantId": "123",
        "database": {
          "connectionString": "Server=db1;Database=tenant_123;..."
        }
      }

3. Identity Service:
   → Uses connection string to connect to Tenant 123's database
   → Validates john@acme.com credentials in that database
   → Generates JWT with TenantId: 123
   → Caches tenant config for 5 minutes (IMemoryCache)
```

#### **3. API Request with JWT**

```
1. User makes request to Project A
   → GET /api/orders
   → Authorization: Bearer <JWT>
   → x-tenant-id: 123 (or from subdomain)

2. Project A:
   → Extracts TenantId: 123 from header/JWT
   → Calls Tenant Service (cached): GET /api/tenant/config/123
   → Gets database connection string
   → Connects to Tenant 123's database
   → Queries: SELECT * FROM Orders WHERE TenantId = 123
   → Returns orders
```

---

## Database Strategy

### **Option 1: Database-Per-Tenant (Full Isolation) ✅ Most Secure**

```
PostgreSQL Server:
├─ tenant_123 (Acme Corp)
│  ├─ Users
│  ├─ Orders
│  └─ Products
├─ tenant_456 (Widget Inc)
│  ├─ Users
│  ├─ Orders
│  └─ Products
└─ tenant_789 (Global Corp)
   ├─ Users
   ├─ Orders
   └─ Products
```

**Pros:**

- ✅ Complete isolation (regulatory compliance)
- ✅ Tenant-specific backups
- ✅ Easy to migrate tenant to different server
- ✅ Can customize database settings per tenant

**Cons:**

- ❌ Higher cost (N databases)
- ❌ Complex connection pool management
- ❌ Database server limits (max databases)

### **Option 2: Shared Database with Schema-Per-Tenant (Moderate Isolation)**

```
PostgreSQL Database: "multitenant"
├─ Schema: tenant_123 (Acme Corp)
│  ├─ Users
│  ├─ Orders
│  └─ Products
├─ Schema: tenant_456 (Widget Inc)
│  ├─ Users
│  ├─ Orders
│  └─ Products
└─ Schema: tenant_789 (Global Corp)
   ├─ Users
   ├─ Orders
   └─ Products
```

**Pros:**

- ✅ Lower cost (one database)
- ✅ Easier management
- ✅ Logical isolation

**Cons:**

- ❌ Less isolation (same database server)
- ❌ Can't easily migrate one tenant
- ❌ Shared resource limits

### **Option 3: Shared Database with Discriminator Column (Least Isolation)**

```
PostgreSQL Database: "multitenant"
└─ Tables (shared):
   ├─ Users (TenantId column)
   ├─ Orders (TenantId column)
   └─ Products (TenantId column)

Query: SELECT * FROM Users WHERE TenantId = '123'
```

**Pros:**

- ✅ Lowest cost
- ✅ Simplest management
- ✅ Best for small tenants

**Cons:**

- ❌ No database-level isolation
- ❌ Risk of data leakage (missing WHERE clause)
- ❌ Can't customize per tenant

**Your Current Setup:** Based on your `TenantInfo` class with `DatabaseSettings.ConnectionString`, you're using **Option 1** (Database-Per-Tenant) or **Option 2** (Schema-Per-Tenant).

---

## Identity Service Configuration

### **Your Current TenantInfo Structure**

```csharp
public class TenantInfo
{
    public required string TenantId { get; set; }
    public string? TenantName { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; set; }

    public TenantConfiguration? Configuration { get; set; }  // ← Contains DB connection!
}

public class TenantConfiguration
{
    public JwtSettings? Jwt { get; set; }
    public DatabaseSettings? Database { get; set; }  // ← Tenant-specific DB!
    public CorsSettings? Cors { get; set; }
}

public class DatabaseSettings
{
    public string? Provider { get; set; }              // "PostgreSQL", "MySQL"
    public string? ConnectionString { get; set; }      // ← Different per tenant!
}
```

### **How Identity Service Uses This**

```csharp
// Identity Service - Login endpoint
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    // 1. Get tenant ID (from header, subdomain, or separate tenant selection)
    var tenantId = HttpContext.Request.Headers["x-tenant-id"].FirstOrDefault()
        ?? throw new BadRequestException("Tenant ID required");

    // 2. Fetch tenant configuration (includes DB connection string)
    var tenantConfig = await _tenantConfigProvider.GetTenantConfigurationAsync(tenantId);
    if (tenantConfig == null || !tenantConfig.IsActive)
        return Unauthorized("Tenant not found or inactive");

    // 3. Create DbContext with tenant-specific connection string
    var dbContext = CreateTenantDbContext(tenantConfig.Configuration.Database.ConnectionString);

    // 4. Validate user credentials in TENANT'S database
    var user = await dbContext.Users
        .FirstOrDefaultAsync(u => u.Email == request.Email);

    if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        return Unauthorized("Invalid credentials");

    // 5. Generate JWT with tenant ID
    var token = GenerateJwtToken(user, tenantId);

    return Ok(new { accessToken = token, tenantId });
}

private IdentityDbContext CreateTenantDbContext(string connectionString)
{
    var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
    optionsBuilder.UseNpgsql(connectionString);
    return new IdentityDbContext(optionsBuilder.Options);
}
```

---

## Tenant Service Role

### **Tenant Service Stores Connection Strings**

**Tenant Service Database (Centralized):**

```sql
CREATE TABLE Tenants (
    TenantId VARCHAR(50) PRIMARY KEY,
    TenantName VARCHAR(255) NOT NULL,
    UserId INT NOT NULL,
    IsActive BOOLEAN DEFAULT TRUE,
    Data JSONB,  -- Contains: {"database": {"connectionString": "..."}, "jwt": {...}}
    CreatedAt TIMESTAMP DEFAULT NOW()
);

-- Example data
INSERT INTO Tenants (TenantId, TenantName, UserId, IsActive, Data)
VALUES (
    '123',
    'Acme Corp',
    1,
    TRUE,
    '{
        "database": {
            "provider": "PostgreSQL",
            "connectionString": "Server=db1.azure.com;Port=5432;Database=tenant_123;Username=acme_user;Password=secure123"
        },
        "jwt": {
            "issuer": "IdentityService",
            "audience": "MicroservicesApp",
            "accessTokenExpirationMinutes": 60
        }
    }'
);
```

**Tenant Service API:**

```csharp
[HttpGet("config/{tenantId}")]
public async Task<IActionResult> GetTenantConfig(string tenantId)
{
    var tenant = await _db.Tenants.FindAsync(tenantId);
    if (tenant == null)
        return NotFound();

    return Ok(new
    {
        tenantId = tenant.TenantId,
        tenantName = tenant.TenantName,
        userId = tenant.UserId,
        isActive = tenant.IsActive,
        data = tenant.Data  // JSON string with DB connection, JWT settings, etc.
    });
}
```

---

## Request Flow

### **Complete Flow: User Login → API Request**

```
┌────────────┐
│   Client   │
└─────┬──────┘
      │
      │ 1. POST /api/auth/login
      │    { email, password, tenantId: 123 }
      ▼
┌─────────────────────────────┐
│   IDENTITY SERVICE          │
│                             │
│  2. GET /api/tenant/config/123
│     ────────────────────────┼─────────────┐
│                             │             │
│                             │             ▼
│                             │    ┌─────────────────┐
│                             │    │ TENANT SERVICE  │
│  4. Connect to DB           │◄───┤ Returns:        │
│     using connection string │    │ {               │
│     from tenant config      │    │   tenantId:123, │
│                             │    │   database: {   │
│  5. Query Users table       │    │     connectionString: "Server=db1..." │
│     WHERE Email = john...   │    │   }             │
│                             │    │ }               │
│  6. Verify password         │    └─────────────────┘
│                             │
│  7. Generate JWT            │
│     { sub, tenantId: 123 }  │
└─────────────┬───────────────┘
              │
              │ 8. Return JWT
              ▼
┌────────────────────────────┐
│   Client                   │
│   Stores JWT + TenantId    │
└───────────┬────────────────┘
            │
            │ 9. GET /api/orders
            │    Authorization: Bearer <JWT>
            │    x-tenant-id: 123
            ▼
┌─────────────────────────────┐
│   PROJECT A (Orders)        │
│                             │
│  10. Extract TenantId: 123  │
│                             │
│  11. GET /api/tenant/config/123
│      (cached, 5-min TTL)    │◄───┐ From cache or Tenant Service
│                             │    │
│  12. Connect to DB          │    │
│      using connection string│    │
│                             │    │
│  13. Query Orders           │    │
│      SELECT * FROM Orders   │    │
│      WHERE TenantId = 123   │    │
│                             │    │
│  14. Return orders          │    │
└─────────────┬───────────────┘    │
              │                     │
              ▼                     │
         [Client]                   │
                                    │
         Database 1 (Tenant 123)◄──┘
         ├─ Users table
         └─ Orders table
```

---

## Implementation Details

### **Dynamic DbContext Creation**

**Option A: DbContext Factory (Recommended)**

```csharp
public interface ITenantDbContextFactory<TContext> where TContext : DbContext
{
    TContext CreateDbContext(string tenantId);
}

public class TenantDbContextFactory<TContext> : ITenantDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly ITenantConfigurationProvider _tenantConfigProvider;

    public TenantDbContextFactory(ITenantConfigurationProvider tenantConfigProvider)
    {
        _tenantConfigProvider = tenantConfigProvider;
    }

    public TContext CreateDbContext(string tenantId)
    {
        var tenantConfig = await _tenantConfigProvider.GetTenantConfigurationAsync(tenantId);
        if (tenantConfig?.Configuration?.Database == null)
            throw new Exception($"Tenant {tenantId} database configuration not found");

        var connectionString = tenantConfig.Configuration.Database.ConnectionString;
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options);
    }
}
```

**Usage in Identity Service:**

```csharp
public class AuthService
{
    private readonly ITenantDbContextFactory<IdentityDbContext> _dbContextFactory;
    private readonly ITenantContext _tenantContext;

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var tenantId = _tenantContext.TenantId ?? throw new Exception("Tenant ID required");

        // Create DbContext for this tenant
        using var dbContext = _dbContextFactory.CreateDbContext(tenantId);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        // ... validate password, generate token
    }
}
```

**Option B: Scoped DbContext with Tenant Resolution**

```csharp
public class IdentityDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantConfigurationProvider _tenantConfigProvider;

    public IdentityDbContext(
        ITenantContext tenantContext,
        ITenantConfigurationProvider tenantConfigProvider)
    {
        _tenantContext = tenantContext;
        _tenantConfigProvider = tenantConfigProvider;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            throw new Exception("Tenant ID not set");

        var tenantConfig = _tenantConfigProvider.GetTenantConfigurationAsync(tenantId).Result;
        var connectionString = tenantConfig?.Configuration?.Database?.ConnectionString
            ?? throw new Exception($"Tenant {tenantId} database not configured");

        optionsBuilder.UseNpgsql(connectionString);
    }
}
```

---

## Project Isolation Revisited

### **NEW UNDERSTANDING: Projects Share Same Tenant Database**

**Previous Understanding (WRONG for your architecture):**

```
❌ Each project has separate database
❌ User has separate accounts per project
```

**Correct Understanding (YOUR architecture):**

```
✅ Each TENANT has separate database
✅ All projects for that tenant use SAME database
✅ User has ONE account per tenant
✅ ProjectId is just a filter column in shared tenant database
```

**Example:**

```
Tenant 123 (Acme Corp) → Database: tenant_123
├─ Users table
│  └─ john@acme.com (ONE user record)
│
├─ UserProjects table
│  ├─ UserId: 1, ProjectId: A, Role: Admin
│  ├─ UserId: 1, ProjectId: B, Role: Viewer
│  └─ UserId: 1, ProjectId: C, Role: Editor
│
├─ Orders table (Project A data)
│  ├─ OrderId: 1, ProjectId: A, TenantId: 123, CustomerId: ...
│  └─ OrderId: 2, ProjectId: A, TenantId: 123, CustomerId: ...
│
└─ Products table (Project B data)
   ├─ ProductId: 1, ProjectId: B, TenantId: 123, Name: ...
   └─ ProductId: 2, ProjectId: B, TenantId: 123, Name: ...

Tenant 456 (Widget Inc) → Database: tenant_456
├─ Users table
│  └─ jane@widget.com (Different database, separate user)
└─ ... (same schema, different data)
```

**Key Insight:**

- **TenantId** = Database boundary (complete isolation)
- **ProjectId** = Logical filter within same database (soft isolation)

**This means:**

- ❌ Tenant 123 CANNOT access Tenant 456 data (different databases)
- ✅ Project A CAN access Project B data (same database, different ProjectId filter)
- ✅ john@acme.com uses SAME login for Project A, B, C (all in tenant_123 database)

---

## Security & Isolation

### **Isolation Levels**

#### **Level 1: Database-Level Isolation (Tenant)**

```
Tenant 123 → Database: tenant_123
Tenant 456 → Database: tenant_456

✅ COMPLETE ISOLATION
✅ Tenant 123 CANNOT access Tenant 456 data (different databases)
✅ No SQL injection can cross tenant boundaries
✅ Regulatory compliance (GDPR, HIPAA)
```

#### **Level 2: Application-Level Isolation (Project)**

```
Within tenant_123 database:
├─ Project A data (ProjectId: A)
├─ Project B data (ProjectId: B)
└─ Project C data (ProjectId: C)

⚠️ SOFT ISOLATION (application enforces filter)
⚠️ Developer must remember WHERE ProjectId = 'A'
⚠️ Missing filter = data leakage within same tenant
✅ Same tenant, different projects (logical separation)
```

### **Security Best Practices**

#### **1. Always Filter by ProjectId in Queries**

```csharp
// ✅ CORRECT
var orders = await _db.Orders
    .Where(o => o.TenantId == tenantId && o.ProjectId == projectId)  // ← Both filters!
    .ToListAsync();

// ❌ WRONG (Project B can see Project A data)
var orders = await _db.Orders
    .Where(o => o.TenantId == tenantId)  // Missing ProjectId filter!
    .ToListAsync();
```

#### **2. Use Query Filters (EF Core Global Filters)**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Global filter: Always filter by TenantId
    modelBuilder.Entity<Order>()
        .HasQueryFilter(o => o.TenantId == _tenantContext.TenantId);

    // Global filter: Always filter by ProjectId
    modelBuilder.Entity<Order>()
        .HasQueryFilter(o => o.ProjectId == _projectContext.ProjectId);
}
```

#### **3. Connection String Security**

```csharp
// ❌ NEVER expose connection strings in API responses
[HttpGet("config/{tenantId}")]
public IActionResult GetConfig(string tenantId)
{
    return Ok(new
    {
        tenantId,
        connectionString = "Server=db1..."  // ❌ Security risk!
    });
}

// ✅ ONLY backend services should access connection strings
// Frontend gets opaque tenant ID only
```

---

## Best Practices

### **1. Tenant Resolution Strategy**

**Option A: Subdomain**

```
https://acme.myapp.com → TenantId: 123
https://widget.myapp.com → TenantId: 456

Middleware extracts subdomain → Resolves to TenantId
```

**Option B: Header**

```
GET /api/orders
x-tenant-id: 123

Middleware reads header → Sets ITenantContext.TenantId
```

**Option C: JWT Claim**

```
JWT payload:
{
  "sub": "user-123",
  "tenantId": "123"  ← Embedded in token
}

Middleware reads JWT → Sets ITenantContext.TenantId
```

### **2. Connection Pool Management**

```csharp
// Problem: N tenants × M projects = N×M connection pools (memory leak!)

// Solution: Use connection string pooling
services.AddDbContextPool<IdentityDbContext>(options =>
{
    options.UseNpgsql(connectionString);
}, poolSize: 128);
```

### **3. Caching Tenant Configuration**

```csharp
// Your current implementation (5-minute cache)
_cache.Set($"tenant_config_{tenantId}", tenantConfig, TimeSpan.FromMinutes(5));

// Benefits:
// ✅ Reduces API calls to Tenant Service
// ✅ Faster response times (0.001ms vs 50ms)

// Drawbacks:
// ⚠️ Tenant DB connection change takes 5 minutes to propagate
// ⚠️ Solution: Provide "clear cache" API endpoint
```

### **4. Database Migrations Per Tenant**

```csharp
public async Task MigrateAllTenantsAsync()
{
    var tenants = await _tenantService.GetAllTenantsAsync();

    foreach (var tenant in tenants)
    {
        var connectionString = tenant.Configuration.Database.ConnectionString;
        var dbContext = CreateDbContext(connectionString);

        await dbContext.Database.MigrateAsync();  // Run migrations

        _logger.LogInformation("Migrated database for tenant {TenantId}", tenant.TenantId);
    }
}
```

---

## Summary

### **Your Architecture (Clarified)**

```
┌──────────────────────────────────────────────────────────────┐
│                  SHARED SERVICES (ONE Each)                   │
│                                                                │
│  ┌────────────────────┐      ┌──────────────────────┐        │
│  │ Identity Service   │◄────►│  Tenant Service      │        │
│  │ (ONE deployment)   │      │  (Stores DB configs) │        │
│  └────────────────────┘      └──────────────────────┘        │
└──────────────────────────────────────────────────────────────┘
            │                              │
            │                              │
    ┌───────┴────────────┬─────────────────┴──────┐
    │                    │                        │
    ▼                    ▼                        ▼
┌──────────────┐   ┌──────────────┐       ┌──────────────┐
│ Database 1   │   │ Database 2   │       │ Database N   │
│              │   │              │       │              │
│ tenant_123   │   │ tenant_456   │  ...  │ tenant_xxx   │
│ (Acme Corp)  │   │ (Widget Inc) │       │              │
│              │   │              │       │              │
│ ├─ Users     │   │ ├─ Users     │       │ ├─ Users     │
│ ├─ Orders    │   │ ├─ Orders    │       │ ├─ Orders    │
│ └─ Products  │   │ └─ Products  │       │ └─ Products  │
└──────────────┘   └──────────────┘       └──────────────┘
```

### **Key Takeaways**

| Aspect                 | Implementation                                                       |
| ---------------------- | -------------------------------------------------------------------- |
| **Identity Service**   | ONE deployment, dynamically connects to different DBs                |
| **Tenant Service**     | Stores DB connection strings per tenant                              |
| **Isolation Level**    | Database-per-tenant (complete isolation)                             |
| **User Accounts**      | One user account per tenant (shared across projects)                 |
| **Project Separation** | ProjectId column (soft filter within same DB)                        |
| **Same Email?**        | ✅ john@acme.com in Tenant 123 DB, john@example.com in Tenant 456 DB |

### **Impact on File Manager Service**

Your File Manager should:

- ✅ Store files in tenant-specific storage paths: `tenant_123/ProjectA/file.pdf`
- ✅ Store metadata (FileMetadata table) in tenant's database
- ✅ Use ProjectId as filter column (not isolation boundary)
- ✅ Use TenantId as database boundary (complete isolation)

**See updated FILE_MANAGER_SERVICE_GUIDE.md for implementation details.**

---

**Last Updated:** October 19, 2025  
**Version:** 1.0.0
