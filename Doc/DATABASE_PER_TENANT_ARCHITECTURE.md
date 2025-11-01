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

### **Implementation Status**

✅ **FULLY IMPLEMENTED** - This architecture is now active in the codebase!

The Identity Service dynamically routes database connections based on tenant configuration:

- When `MultiTenancy:Enabled = false`: Uses static connection from appsettings.json
- When `MultiTenancy:Enabled = true`: Checks tenant configuration for database settings
- If tenant has custom database: Connects to tenant-specific database
- If tenant has no custom database: Falls back to default database from appsettings.json

### **Step-by-Step Flow**

#### **1. User Registration (New Tenant)**

```
1. User visits registration page
   → Email: john@acme.com
   → Password: SecurePass123
   → Company: Acme Corp

2. Identity Service:
   → Creates tenant in Tenant Service
   → Tenant Service stores configuration (includes DB connection string)
   → Returns: TenantId: 123

3. Identity Service:
   → Fetches tenant config (includes DB connection string)
   → DbContext.OnConfiguring checks tenant settings
   → Connects to Tenant 123's database dynamically
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

## JWT Mode Examples

### **Example 1: Superadmin with Shared JWT**

**Scenario:** Company has a superadmin who needs to access all tenant data for support/maintenance.

**Configuration:**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "Shared"
  },
  "Jwt": {
    "Secret": "superadmin-shared-secret-key",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "ExpiryInMinutes": 120
  }
}
```

**Flow:**

```
1. Superadmin logs in
   → Identity Service generates JWT using Jwt.Secret from appsettings.json
   → JWT contains: { "sub": "superadmin", "role": "superadmin" }

2. Superadmin accesses Tenant 123
   → Request: GET /api/tenant/123/users
   → Header: x-tenant-id: 123
   → Authorization: Bearer <JWT>

3. Service validates JWT
   → Uses Jwt.Secret from appsettings.json (all tenants share same secret)
   → JWT is valid ✅
   → Connects to Tenant 123's database
   → Returns users

4. Superadmin accesses Tenant 456 (same token!)
   → Request: GET /api/tenant/456/orders
   → Header: x-tenant-id: 456
   → Authorization: Bearer <same JWT>

5. Service validates JWT
   → Uses Jwt.Secret from appsettings.json
   → JWT is valid ✅
   → Connects to Tenant 456's database
   → Returns orders
```

**Benefits:**

- ✅ Superadmin can access all tenants with one token
- ✅ Simpler token management
- ✅ Easier for internal tools and support staff

---

### **Example 2: Per-Tenant JWT (Maximum Isolation)**

**Scenario:** SaaS platform with enterprise customers who require complete data isolation.

**Configuration:**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant"
  }
}
```

**Tenant 123 Configuration (in Tenant Service):**

```json
{
  "tenantId": "123",
  "configuration": {
    "jwt": {
      "secret": "tenant-123-unique-secret-abc123xyz",
      "issuer": "IdentityService",
      "audience": "MicroservicesApp"
    }
  }
}
```

**Tenant 456 Configuration (in Tenant Service):**

```json
{
  "tenantId": "456",
  "configuration": {
    "jwt": {
      "secret": "tenant-456-different-secret-def456uvw",
      "issuer": "IdentityService",
      "audience": "MicroservicesApp"
    }
  }
}
```

**Flow:**

```
1. User from Tenant 123 logs in
   → Identity Service fetches Tenant 123 config
   → Generates JWT using tenant-123-unique-secret
   → JWT contains: { "sub": "user-1", "tenantId": "123" }

2. User accesses Tenant 123 API
   → Request: GET /api/orders
   → Header: x-tenant-id: 123
   → Authorization: Bearer <JWT>

3. Service validates JWT
   → Fetches Tenant 123 config
   → Validates JWT using tenant-123-unique-secret
   → JWT is valid ✅
   → Returns orders

4. User tries to access Tenant 456 (with Tenant 123 token)
   → Request: GET /api/orders
   → Header: x-tenant-id: 456
   → Authorization: Bearer <JWT from Tenant 123>

5. Service validates JWT
   → Fetches Tenant 456 config
   → Tries to validate JWT using tenant-456-different-secret
   → JWT is INVALID ❌ (signed with different secret)
   → Returns 401 Unauthorized
```

**Benefits:**

- 🔒 Complete JWT isolation between tenants
- 🔒 Tenant 123 token cannot be used for Tenant 456
- 🔒 Compromised secret only affects one tenant
- 🔒 Each tenant can rotate their JWT secret independently

---

## Tenant Service Role

**Important**: The Tenant Service itself does NOT use multi-tenancy configuration. It operates with a single, static database connection from its `appsettings.json`. The Tenant Service is the **provider** of tenant configurations, not a **consumer**.

### Configuration

The Tenant Service always uses:

- **Database**: Static connection from `appsettings.json` → `DatabaseSettings:ConnectionString`
- **JWT**: Static settings from `appsettings.json` → `Jwt` section
- **CORS**: Static origins from `appsettings.json` → `Cors` section
- **No MultiTenancy section needed**: The service doesn't require `MultiTenancy` configuration

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
        data = tenant.Data  // TenantConfiguration object (stored as JSON string in DB, deserialized for response)
    });
}
```

**Important:** The API returns `data` as a **JSON object** (type: `TenantConfiguration`). Although it's stored as a JSON string in PostgreSQL, the application layer automatically deserializes it to a strongly-typed object for API responses and serializes it when saving to the database.

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

### **Dynamic DbContext Creation (IMPLEMENTED)**

The current implementation uses **OnConfiguring override** to dynamically resolve database connections based on tenant context. This approach is elegant and leverages EF Core's built-in configuration pipeline.

**How It Works:**

1. **Multi-Tenancy Enabled Check**: `DatabaseExtensions.AddDatabaseContext()` checks if multi-tenancy is enabled
2. **Minimal Registration**: When enabled, DbContext is registered without pre-configured options
3. **Dynamic Configuration**: `IdentityDbContext.OnConfiguring()` resolves the connection at runtime
4. **Tenant-Aware**: Checks `ITenantContext` for tenant-specific database settings
5. **Automatic Fallback**: Uses appsettings.json if no tenant database is configured

**Implementation in IdentityDbContext:**

```csharp
public class IdentityDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<IdentityDbContext>? _logger;

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<IdentityDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

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

        // Check if multi-tenancy is enabled and tenant has custom database settings
        if (_tenantContext?.HasTenant == true &&
            _tenantContext.CurrentTenant?.Configuration?.Database != null)
        {
            var tenantDb = _tenantContext.CurrentTenant.Configuration.Database;

            if (!string.IsNullOrWhiteSpace(tenantDb.ConnectionString))
            {
                connectionString = tenantDb.ConnectionString;
                provider = tenantDb.Provider ?? "PostgreSql";

                _logger?.LogInformation(
                    "Using tenant-specific database connection for tenant: {TenantId}",
                    _tenantContext.CurrentTenant.TenantId);
            }
        }

        // Fallback to appsettings.json if no tenant-specific database
        if (string.IsNullOrWhiteSpace(connectionString) && _configuration != null)
        {
            connectionString = _configuration["DatabaseSettings:ConnectionString"];
            provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

            _logger?.LogDebug("Using default database connection from appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured");
        }

        // Configure database provider
        switch (provider)
        {
            case "PostgreSql":
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.GetName().Name);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
                break;

            case "Sqlite":
                optionsBuilder.UseSqlite(connectionString);
                break;

            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported");
        }

        base.OnConfiguring(optionsBuilder);
    }
}
```

**Key Features:**

✅ **Automatic Tenant Detection**: Checks `ITenantContext.HasTenant` to determine if tenant database should be used  
✅ **Graceful Fallback**: Uses appsettings.json if tenant doesn't have custom database settings  
✅ **Multi-Provider Support**: Supports PostgreSQL and SQLite  
✅ **Logging**: Logs which database connection is being used for debugging  
✅ **Error Handling**: Clear error messages if configuration is missing

### **Service Registration (IMPLEMENTED)**

**In DatabaseExtensions.cs (Shared Infrastructure):**

```csharp
public static IServiceCollection AddDatabaseContext<TContext>(
    this IServiceCollection services,
    IConfiguration configuration) where TContext : DbContext
{
    var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled");

    if (multiTenancyEnabled)
    {
        // Multi-tenancy mode: Register DbContext with minimal config
        // Connection will be resolved dynamically in OnConfiguring
        services.AddDbContext<TContext>(options =>
        {
            // Minimal configuration - actual connection determined at runtime
        });
    }
    else
    {
        // Single-tenant mode: Use static connection from appsettings.json
        var connectionString = configuration["DatabaseSettings:ConnectionString"]
            ?? throw new InvalidOperationException("Database connection string is not configured");

        var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

        services.AddDbContext<TContext>(options =>
        {
            switch (provider)
            {
                case "PostgreSql":
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
                        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    });
                    break;

                case "Sqlite":
                    options.UseSqlite(connectionString);
                    break;

                default:
                    throw new NotSupportedException($"Database provider '{provider}' is not supported");
            }
        });
    }

    return services;
}
```

**In Program.cs (Identity.API):**

```csharp
// Register database with multi-tenancy support
builder.Services.AddDatabaseContext<IdentityDbContext>(builder.Configuration);
```

**Key Features:**

✅ **Configuration-Driven**: Checks `MultiTenancy:Enabled` flag from appsettings.json  
✅ **Two Modes**: Supports both single-tenant and multi-tenant deployments  
✅ **Lazy Resolution**: In multi-tenant mode, connection resolved per-request in OnConfiguring  
✅ **Static Configuration**: In single-tenant mode, connection set once at startup

### **Configuration Setup**

**appsettings.json (Identity Service):**

```json
{
  "MultiTenancy": {
    "Enabled": true, // Set to false for single-tenant mode
    "JwtMode": "Shared", // "Shared" or "PerTenant"
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=identity_default;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "your-secret-key-minimum-32-characters",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "ExpiryInMinutes": 60
  }
}
```

**JWT Mode Configuration:**

### **Option 1: Shared JWT Mode (Recommended for Superadmin Access)** ✅

When `JwtMode = "Shared"`:

- All tenants use the **same JWT secret** from the `Jwt` section in appsettings.json
- Superadmin can access all tenants with a single token
- Token contains `tenantId` claim to identify which tenant the user belongs to
- Simpler configuration, easier management

**Use Cases:**

- Superadmin needs to access multiple tenants
- Internal tools that work across tenants
- Simpler multi-tenant setups

**Configuration:**

```json
{
  "MultiTenancy": {
    "JwtMode": "Shared"
  },
  "Jwt": {
    "Secret": "shared-secret-for-all-tenants-32-chars-minimum",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "ExpiryInMinutes": 60
  }
}
```

---

### **Option 2: Per-Tenant JWT Mode (Maximum Isolation)** 🔒

When `JwtMode = "PerTenant"`:

- Each tenant has their **own JWT secret** stored in Tenant Service
- Token issued for Tenant A cannot be used for Tenant B
- Complete JWT isolation between tenants
- Enhanced security (compromised secret affects only one tenant)

**Use Cases:**

- Maximum security and tenant isolation required
- Regulatory compliance (GDPR, HIPAA)
- Enterprise customers who want their own JWT secrets
- Need to rotate JWT secrets per tenant

**Configuration:**

```json
{
  "MultiTenancy": {
    "JwtMode": "PerTenant"
  }
}
```

**Tenant Configuration (stored in Tenant Service):**

```json
{
  "tenantId": "123",
  "configuration": {
    "jwt": {
      "secret": "tenant-123-unique-secret-key",
      "issuer": "IdentityService",
      "audience": "MicroservicesApp",
      "accessTokenExpirationMinutes": 60
    },
    "database": {
      "connectionString": "Host=tenant-db-1;Database=tenant_123;..."
    }
  }
}
```

---

**Configuration Behavior:**

| Scenario                         | JWT Source                               | Behavior                                                       |
| -------------------------------- | ---------------------------------------- | -------------------------------------------------------------- |
| **JwtMode = Shared**             | `Jwt` section from appsettings.json      | All tenants use same secret, superadmin can access all tenants |
| **JwtMode = PerTenant**          | Tenant configuration from Tenant Service | Each tenant uses own secret, complete isolation                |
| **MultiTenancy:Enabled = false** | `Jwt` section from appsettings.json      | Single-tenant mode, no multi-tenancy                           |

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

| Aspect                 | Implementation                                                            |
| ---------------------- | ------------------------------------------------------------------------- |
| **Identity Service**   | ONE deployment, dynamically connects to different DBs                     |
| **Tenant Service**     | Stores DB connection strings per tenant                                   |
| **Isolation Level**    | Database-per-tenant (complete isolation)                                  |
| **JWT Mode**           | Configurable: "Shared" (superadmin access) or "PerTenant" (max isolation) |
| **User Accounts**      | One user account per tenant (shared across projects)                      |
| **Project Separation** | ProjectId column (soft filter within same DB)                             |
| **Same Email?**        | ✅ john@acme.com in Tenant 123 DB, john@example.com in Tenant 456 DB      |

### **JWT Configuration Summary**

| JWT Mode      | Secret Source                          | Use Case                               | Isolation Level                   |
| ------------- | -------------------------------------- | -------------------------------------- | --------------------------------- |
| **Shared**    | `Jwt` section in appsettings.json      | Superadmin access, internal tools      | All tenants share same JWT secret |
| **PerTenant** | Tenant configuration in Tenant Service | Enterprise customers, maximum security | Each tenant has unique JWT secret |

**When to use Shared JWT:**

- ✅ Superadmin needs to access all tenants
- ✅ Internal support tools
- ✅ Simpler configuration and management

**When to use PerTenant JWT:**

- 🔒 Maximum security and isolation required
- 🔒 Regulatory compliance (GDPR, HIPAA)
- 🔒 Enterprise customers who demand separate secrets
- 🔒 Need to rotate JWT secrets per tenant

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
