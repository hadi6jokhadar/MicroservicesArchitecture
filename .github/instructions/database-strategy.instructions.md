---
description: "Use when creating a new microservice, implementing a DbContext, adding multi-tenancy middleware, choosing database isolation strategy, or wiring a Program.cs pipeline. Covers all 4 database strategies: SingleGlobal, PerTenant, DualDb, GlobalWithDiscriminator. Includes complete DbContext code, Program.cs middleware order, BypassTenant and OptionalTenant attribute rules."
---

# Database Strategy Guide for New Services

## 🔑 Decision Tree — Choose One Strategy

```
Does the service STORE tenant-specific business data?
│
├─ NO  → Is it a system registry/provider (config, translations)?
│        ├─ Keeps per-tenant rows with a TenantId column?  → Strategy D (Global + Discriminator)
│        └─ No per-tenant rows at all?                     → Strategy A (Single Global DB)
│
└─ YES → Does it also need a SHARED global queue/inbox?
         ├─ YES (e.g. processing queue + per-tenant history) → Strategy C (Dual DB)
         └─ NO  (pure tenant data)                           → Strategy B (Per-Tenant DB)
```

| Strategy                   | Services using it     | ITenantContext in DbContext | x-tenant-id header required |
| -------------------------- | --------------------- | --------------------------- | --------------------------- |
| A – Single Global DB       | Tenant                | ✗                           | ✗                           |
| B – Per-Tenant DB          | Identity, FileManager | ✓                           | ✓                           |
| C – Dual DB                | Notification          | ✓ (tenant ctx only)         | ✓                           |
| D – Global + Discriminator | Translation           | ✗                           | ✗                           |

---

## Strategy A — Single Global DB

Use when the service IS the system registry (no tenant isolation needed).  
**Example:** `TenantService`

### DbContext

```csharp
// No ITenantContext. Options come entirely from DI registration.
public class MyServiceDbContext : BaseDbContext
{
    public MyServiceDbContext(
        DbContextOptions<MyServiceDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options, currentUserService) { }

    public DbSet<MyEntity> MyEntities => Set<MyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // entity configuration here
    }
}
```

### Program.cs (DI + Pipeline)

```csharp
// ── DI ──────────────────────────────────────────────
builder.Services.AddDatabaseContext<MyServiceDbContext>(
    builder.Configuration,
    typeof(MyServiceDbContext).Assembly.GetName().Name!);

// ── Pipeline ─────────────────────────────────────────
// NO UseTenantResolution()  — not needed
// NO UseTenantDatabaseMigration()

app.UseDefaultDatabaseMigration<MyServiceDbContext>();  // single global migration
app.UseAuthentication();
app.UseAuthorization();
```

### appsettings.json

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=myservice_db;Username=...;Password=..."
  }
}
```

---

## Strategy B — Per-Tenant DB (Full Isolation)

Use when every tenant has their own isolated database.  
**Example:** `IdentityService`, `FileManagerService`

### DbContext

```csharp
public class MyServiceDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<MyServiceDbContext>? _logger;

    public MyServiceDbContext(
        DbContextOptions<MyServiceDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<MyServiceDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<MyEntity> MyEntities => Set<MyEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // If already configured from DI (e.g. tests), skip
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        string? connectionString;
        string? provider;
        var multiTenancyEnabled = _configuration?.GetValue<bool>("MultiTenancy:Enabled", false) ?? false;

        if (multiTenancyEnabled)
        {
            if (_tenantContext?.HasTenant != true ||
                _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
            {
                // Fallback: no tenant context yet (BypassTenant endpoints or DI resolution)
                _logger?.LogDebug("No tenant context — using global fallback DB");
                connectionString = _configuration?["DatabaseSettings:ConnectionString"]
                    ?? throw new InvalidOperationException("DatabaseSettings:ConnectionString not configured");
                provider = _configuration?["DatabaseSettings:Provider"] ?? "PostgreSql";
            }
            else
            {
                var tenantDb = _tenantContext.CurrentTenant.Configuration.DatabaseSettings;
                connectionString = tenantDb.ConnectionString
                    ?? throw new InvalidOperationException(
                        $"Tenant '{_tenantContext.TenantId}' has no database connection string configured");
                provider = tenantDb.Provider ?? "PostgreSql";
            }
        }
        else
        {
            // Multi-tenancy disabled: always use global DB
            connectionString = _configuration?["DatabaseSettings:ConnectionString"]
                ?? throw new InvalidOperationException("DatabaseSettings:ConnectionString not configured");
            provider = _configuration?["DatabaseSettings:Provider"] ?? "PostgreSql";
        }

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
            optionsBuilder.UseNpgsql(connectionString, o =>
                o.MigrationsAssembly(typeof(MyServiceDbContext).Assembly.GetName().Name));
        }
        else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(connectionString, o =>
                o.MigrationsAssembly(typeof(MyServiceDbContext).Assembly.GetName().Name));
        }
    }
}
```

### Program.cs (DI + Pipeline)

```csharp
// ── DI ──────────────────────────────────────────────
builder.Services.AddMultiTenancy(builder.Configuration);   // registers ITenantContext + ITenantConfigurationProvider
builder.Services.AddDatabaseContext<MyServiceDbContext>(
    builder.Configuration,
    typeof(MyServiceDbContext).Assembly.GetName().Name!);

// ── Pipeline (ORDER IS CRITICAL) ─────────────────────
app.UseTenantResolution(builder.Configuration);        // 1. reads x-tenant-id, calls Tenant Service, sets ITenantContext
app.UseTenantAwareCors();                              // 2. CORS based on tenant config (BEFORE JWT verification)
app.UseJwtTenantVerification(builder.Configuration);   // 3. verifies JWT tenant_id claim == x-tenant-id header

// ALWAYS migrate the global DB first (fallback / BypassTenant endpoints)
app.UseDefaultDatabaseMigration<MyServiceDbContext>();

var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false);
if (multiTenancyEnabled)
{
    // Auto-create + migrate tenant-specific DB on first request per tenant
    app.UseTenantDatabaseMigration<MyServiceDbContext>(builder.Configuration);
}

app.UseAuthentication();
app.UseAuthorization();
```

### appsettings.json

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=myservice_global;Username=...;Password=..."
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 30
  }
}
```

### Endpoint Attributes

```csharp
// Standard tenant endpoint — requires x-tenant-id header
app.MapGet("/api/items", handler)
   .RequireAuthorization();

// Cross-tenant admin endpoint — no x-tenant-id needed, uses global DB
app.MapGet("/api/admin/items", handler)
   .WithMetadata(new BypassTenantAttribute())
   .RequireAuthorization("Admin");

// Endpoint that WORKS with or without x-tenant-id
app.MapGet("/api/public/items", handler)
   .WithMetadata(new OptionalTenantAttribute());
```

---

## Strategy C — Dual DB (Global Queue + Per-Tenant History)

Use when the service needs a **shared processing queue** (global) AND **per-tenant history/records** (isolated).  
**Example:** `NotificationService`

### DbContexts — Two Separate Classes

**Global context** (no ITenantContext):

```csharp
/// <summary>Global database — shared across all tenants. Stores queue/inbox items.</summary>
public class MyServiceGlobalDbContext : BaseDbContext
{
    public MyServiceGlobalDbContext(
        DbContextOptions<MyServiceGlobalDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options, currentUserService) { }

    public DbSet<QueueItem> Queue { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<QueueItem>(e =>
        {
            // TenantId column used for filtering only — NOT for routing
            e.Property(x => x.TenantId).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.Status, x.Created });
        });
    }
}
```

**Tenant context** (with ITenantContext — same pattern as Strategy B):

```csharp
/// <summary>Per-tenant database — each tenant has their own history/records.</summary>
public class MyServiceTenantDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<MyServiceTenantDbContext>? _logger;

    public MyServiceTenantDbContext(
        DbContextOptions<MyServiceTenantDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<MyServiceTenantDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<HistoryRecord> History { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Identical to Strategy B OnConfiguring — copy/adapt from there
    }
}
```

### Program.cs (DI + Pipeline)

```csharp
// ── DI ──────────────────────────────────────────────
builder.Services.AddMultiTenancy(builder.Configuration);
builder.Services.AddDatabaseContext<MyServiceGlobalDbContext>(
    builder.Configuration,
    typeof(MyServiceGlobalDbContext).Assembly.GetName().Name!);
builder.Services.AddDatabaseContext<MyServiceTenantDbContext>(
    builder.Configuration,
    typeof(MyServiceTenantDbContext).Assembly.GetName().Name!);

// ── Pipeline ─────────────────────────────────────────
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();
app.UseJwtTenantVerification(builder.Configuration);

var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false);
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<MyServiceGlobalDbContext>(builder.Configuration);   // global migrated per-tenant too (safe, idempotent)
    app.UseTenantDatabaseMigration<MyServiceTenantDbContext>(builder.Configuration);   // tenant DB
}
else
{
    app.UseDefaultDatabaseMigration<MyServiceGlobalDbContext>();
    app.UseDefaultDatabaseMigration<MyServiceTenantDbContext>();
}

app.UseAuthentication();
app.UseAuthorization();
```

---

## Strategy D — Global DB + Discriminator Column

Use when the service is a **shared provider** (like a translation/config service) where all tenants share one DB but tenant rows are distinguished by a nullable `TenantId` column.  
**Example:** `TranslationService`

### DbContext

```csharp
/// <summary>
/// All tenants share one global database.
/// TenantId = NULL → base/global record visible to all tenants.
/// TenantId = "abc" → tenant-specific override.
/// </summary>
public class MyServiceDbContext : BaseDbContext
{
    private readonly IConfiguration? _configuration;
    private readonly ILogger<MyServiceDbContext>? _logger;

    public MyServiceDbContext(
        DbContextOptions<MyServiceDbContext> options,
        ICurrentUserService? currentUserService = null,
        IConfiguration? configuration = null,
        ILogger<MyServiceDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<MySharedEntity> Entities => Set<MySharedEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        // ALWAYS use global DB — even when multi-tenancy is enabled system-wide
        var connectionString = _configuration?["DatabaseSettings:ConnectionString"]
            ?? throw new InvalidOperationException("DatabaseSettings:ConnectionString not configured");
        var provider = _configuration?["DatabaseSettings:Provider"] ?? "PostgreSql";

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
            optionsBuilder.UseNpgsql(connectionString, o =>
                o.MigrationsAssembly(typeof(MyServiceDbContext).Assembly.GetName().Name));
        }
    }
}
```

### Entity Pattern

```csharp
public class MySharedEntity : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string? TenantId { get; private set; }  // NULL = global, "abc" = tenant override

    // Factory for global record
    public static MySharedEntity CreateGlobal(string key, string value)
        => new() { Key = key, Value = value, TenantId = null };

    // Factory for tenant override
    public static MySharedEntity CreateForTenant(string key, string value, string tenantId)
        => new() { Key = key, Value = value, TenantId = tenantId };
}
```

### Query Pattern (resolve with fallback)

```csharp
// Always returns tenant-specific value if it exists, otherwise falls back to global
var value = await _context.Entities
    .Where(e => e.Key == key && (e.TenantId == tenantId || e.TenantId == null))
    .OrderByDescending(e => e.TenantId != null)  // tenant-specific first
    .FirstOrDefaultAsync(ct);
```

### Program.cs (DI + Pipeline)

```csharp
// ── DI ──────────────────────────────────────────────
// NO AddMultiTenancy() — not needed
builder.Services.AddDatabaseContext<MyServiceDbContext>(
    builder.Configuration,
    typeof(MyServiceDbContext).Assembly.GetName().Name!);

// ── Pipeline ─────────────────────────────────────────
// NO UseTenantResolution()
// NO UseTenantDatabaseMigration()
app.UseCors();  // standard CORS (no tenant-aware CORS needed)
app.UseDefaultDatabaseMigration<MyServiceDbContext>();
app.UseAuthentication();
app.UseAuthorization();
```

---

## ⚠️ Critical Rules

1. **Never call `UseTenantResolution` without `AddMultiTenancy`** — they are paired.
2. **Never call `UseTenantDatabaseMigration` without `UseDefaultDatabaseMigration`** — always migrate the global DB first as fallback for `[BypassTenant]` endpoints.
3. **`UseTenantAwareCors()` replaces `UseCors()`** — never call both; `TenantAwareCorsMiddleware` handles OPTIONS preflight too.
4. **Middleware order is fixed**: `UseTenantResolution` → `UseTenantAwareCors` → `UseJwtTenantVerification` → `UseTenantDatabaseMigration` → `UseAuthentication` → `UseAuthorization`.
5. **`[BypassTenant]` endpoints** must never depend on `ITenantContext.CurrentTenant` — the DbContext will use the global fallback connection.
6. **Strategy B/C DbContexts** must handle `optionsBuilder.IsConfigured` early — DI configuration from tests and `Program.cs` takes priority over `OnConfiguring`.
