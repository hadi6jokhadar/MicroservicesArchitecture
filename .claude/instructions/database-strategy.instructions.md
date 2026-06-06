# Database Strategy Guide for New Services

Use when creating a new microservice, implementing a DbContext, adding multi-tenancy middleware, choosing database isolation strategy, or wiring a Program.cs pipeline. Covers all 4 database strategies: SingleGlobal, PerTenant, DualDb, GlobalWithDiscriminator.

## Decision Tree — Choose One Strategy

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
builder.Services.AddAuditService();                         // automatic before/after audit for every entity change
builder.Services.AddAuditLogQueries<MyServiceDbContext>();  // registers GET /api/admin/audit-logs handler
// ...then call app.MapAuditLogEndpoints() after app.Build()

// ── Before app.Run() ─────────────────────────────────
// Migrate the global DB at startup (before background services start).
// InitializeDatabaseAsync has built-in retry logic for concurrent-startup locking.
await app.Services.InitializeDatabaseAsync<MyServiceDbContext>(applyMigrations: true);

// ── Pipeline ─────────────────────────────────────────
// NO UseTenantResolution()  — not needed
// NO UseTenantDatabaseMigration()

app.UseDefaultDatabaseMigration<MyServiceDbContext>();  // safety net for edge cases
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
builder.Services.AddAuditService();                         // automatic before/after audit for every entity change
builder.Services.AddAuditLogQueries<MyServiceDbContext>();  // registers GET /api/admin/audit-logs handler
// ...then call app.MapAuditLogEndpoints() after app.Build()

// ── Before app.Run() ─────────────────────────────────
await app.Services.InitializeDatabaseAsync<MyServiceDbContext>(applyMigrations: true);

// ── Pipeline (ORDER IS CRITICAL) ─────────────────────
// UseDefaultDatabaseMigration MUST be before UseTenantResolution (see note above)
app.UseDefaultDatabaseMigration<MyServiceDbContext>();      // safety net for global DB

app.UseTenantResolution(builder.Configuration);        // 1. reads x-tenant-id, calls Tenant Service, sets ITenantContext
app.UseTenantAwareCors();                              // 2. CORS based on tenant config (BEFORE JWT verification)
app.UseJwtTenantVerification(builder.Configuration);   // 3. verifies JWT tenant_id claim == x-tenant-id header

var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false);
if (multiTenancyEnabled)
{
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
builder.Services.AddAuditService();                               // automatic before/after audit for every entity change
builder.Services.AddAuditLogQueries<MyServiceGlobalDbContext>();  // registers GET /api/admin/audit-logs handler (use global context)
// ...then call app.MapAuditLogEndpoints() after app.Build()

// ── Before app.Run() ─────────────────────────────────
await app.Services.InitializeDatabaseAsync<MyServiceGlobalDbContext>(applyMigrations: true);

// ── Pipeline ─────────────────────────────────────────
app.UseDefaultDatabaseMigration<MyServiceGlobalDbContext>();
app.UseDefaultDatabaseMigration<MyServiceTenantDbContext>();

app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();
app.UseJwtTenantVerification(builder.Configuration);

var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false);
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<MyServiceGlobalDbContext>(builder.Configuration);
    app.UseTenantDatabaseMigration<MyServiceTenantDbContext>(builder.Configuration);
}

app.UseAuthentication();
app.UseAuthorization();
```

---

## Strategy D — Global DB + Discriminator Column

Use when the service is a **shared provider** where all tenants share one DB but tenant rows are distinguished by a nullable `TenantId` column.
**Example:** `TranslationService`

### DbContext

```csharp
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

    public static MySharedEntity CreateGlobal(string key, string value)
        => new() { Key = key, Value = value, TenantId = null };

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
builder.Services.AddAuditService();                         // automatic before/after audit for every entity change
builder.Services.AddAuditLogQueries<MyServiceDbContext>();  // registers GET /api/admin/audit-logs handler
// ...then call app.MapAuditLogEndpoints() after app.Build()

// ── Before app.Run() ─────────────────────────────────
await app.Services.InitializeDatabaseAsync<MyServiceDbContext>(applyMigrations: true);

// ── Pipeline ─────────────────────────────────────────
// NO UseTenantResolution()
// NO UseTenantDatabaseMigration()
app.UseCors();
app.UseDefaultDatabaseMigration<MyServiceDbContext>();
app.UseAuthentication();
app.UseAuthorization();
```

---

## Critical Rules

1. **Never call `UseTenantResolution` without `AddMultiTenancy`** — they are paired.

2. **`UseDefaultDatabaseMigration` MUST be called BEFORE `UseTenantResolution`** — when multi-tenancy is enabled, `AddDatabaseContext` leaves `IsConfigured=false` so `OnConfiguring` picks the connection string dynamically using `ITenantContext`. If `UseDefaultDatabaseMigration` runs after `UseTenantResolution`, the static `_isMigrated` flag fires against the first tenant's DB, leaving the global fallback DB permanently unmigrated.

3. **Always call `InitializeDatabaseAsync` before `app.Run()` for every global DB** — hosted services (`BackgroundService`) start at the same time as the HTTP server, before any HTTP request triggers middleware migration. Do NOT guard it with `IsDevelopment()` or `!MultiTenancy:Enabled`.

4. **`UseTenantAwareCors()` replaces `UseCors()`** — never call both.

5. **Correct full middleware order (Strategies B/C)**:
   `InitializeDatabaseAsync` (before app.Run) →
   `UseDefaultDatabaseMigration` →
   `UseTenantResolution` → `UseTenantAwareCors` → `UseJwtTenantVerification` →
   `UseTenantDatabaseMigration` (if multi-tenancy enabled) →
   `UseAuthentication` → `UseAuthorization`

6. **`[BypassTenant]` endpoints** must never depend on `ITenantContext.CurrentTenant` — the DbContext will use the global fallback connection.

7. **Strategy B/C DbContexts** must handle `optionsBuilder.IsConfigured` early — DI configuration from tests and `Program.cs` takes priority over `OnConfiguring`.

8. **Strategy C: `InitializeDatabaseAsync` is only needed for the global context** — the tenant history context is only accessed per-request with tenant context; `UseTenantDatabaseMigration` handles it lazily.
