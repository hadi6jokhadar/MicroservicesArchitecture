# 🔄 Automatic Database Migration

## Overview

The system uses a **three-layer migration approach**: global DBs are migrated at startup, per-tenant DBs are migrated eagerly the moment a tenant is created (if Redis is enabled), and lazily on that tenant's first request as a fallback. Layers 1 and 2 are NOT conditional on `MultiTenancy:Enabled` beyond their own guard checks — they always apply where relevant.

### Layer 1 — Startup Migration (`InitializeDatabaseAsync`)

- Runs **before `app.Run()`**, before hosted services start
- Migrates the **global / fallback DB** using `DatabaseSettings:ConnectionString`
- **Always called unconditionally** — never guard with `IsDevelopment()` or `!MultiTenancy:Enabled`
- Has built-in **retry with jitter** (`maxAttempts=3`, `retryDelaySeconds=5`) to handle concurrent-startup DB locking when multiple instances deploy simultaneously
- Required for services with background workers (`BackgroundService`) that query the DB before the first HTTP request arrives

### Layer 2 — Per-Tenant Migration (`UseTenantDatabaseMigration` middleware)

- Runs on **each tenant's first HTTP request**
- Migrates the **tenant-specific DB** using that tenant's connection string from the Tenant Service
- Only viable approach because tenant DBs are provisioned dynamically
- Only registered when `MultiTenancy:Enabled` is `true`
- Now mostly a fallback — Layer 3 (below) migrates most tenants before their first request ever arrives

### Layer 3 — Eager Migration on Tenant Creation (`TenantProvisioningListenerService<TContext>`)

- **Solves:** previously, adding a tenant required restarting every multi-tenant service to trigger `WarmTenantDatabaseMigrationsAsync` (startup warm-up) before that tenant could be used — Layer 2 alone only migrates on the tenant's own first request, which is fine for the request itself but meant every *other* already-running service still needed a restart to pick up the new tenant early.
- The **Tenant Service** publishes a lightweight Redis Pub/Sub message (`TenantProvisionedEventMessage`, channel `tenant:provisioned`) immediately after `CreateTenantCommandHandler` caches the new tenant's configuration.
- Every multi-tenant service that registers `AddTenantProvisioningListener<TContext>(configuration)` (Identity, FileManager, Category, Notification, Nasheed, PolySnap) runs a `TenantProvisioningListenerService<TContext>` background service that subscribes to that channel.
- On receipt, for **just that one new tenant**, it: fetches the tenant's config, sets `ITenantContext`, calls the same `IDatabaseMigrationService.EnsureDatabaseExistsAsync` used by Layer 2, invokes the DbContext's `SeedAsync()` method if it defines one (same reflection-based call `InitializeDatabaseAsync` uses for the global DB), and marks the tenant as migrated (`DatabaseMigrationMiddleware<TContext>.MarkAsMigrated`) so Layer 2 skips it on the real first request.
- **Best-effort, not delivery-guaranteed** — deliberately *not* built on the Transactional Outbox pattern (`EVENT_DRIVEN_PUBLISHER_PATTERN.md`) that business-critical entity events use. If Redis is briefly down or a service was mid-restart when the message was published, that service simply falls back to Layer 2 (the tenant's first request) or its own next startup's `WarmTenantDatabaseMigrationsAsync` warm-up — both of which already exist and already guarantee eventual correctness. Adding outbox durability here would only buy "slightly sooner" for an already-rare edge case, at the cost of a DB table + background processor per consuming service.
- Only active when **both** `MultiTenancy:Enabled` and `Redis:Enabled` are `true` for that service — falls back silently to Layer 2 otherwise (e.g. local dev with in-memory cache).
- Register with: `services.AddTenantProvisioningListener<TContext>(configuration)` (`IhsanDev.Shared.Infrastructure/Extensions/TenantProvisioningExtensions.cs`) — call it right after `AddDatabaseContext<TContext>` in `Program.cs`. No middleware/pipeline changes needed.
- To add tenant seed data for a service, define a `public async Task SeedAsync()` method directly on that service's `DbContext` — it is picked up automatically here (and by `InitializeDatabaseAsync` for the global DB) via reflection; no registration needed.
- **Resilient resubscription (August 2026):** the initial `SubscribeAsync` call to `tenant:provisioned` is wrapped in `SubscribeWithRetryAsync` — an exponential-backoff retry loop (2s initial delay, doubling up to a 30s cap, retried indefinitely) rather than a single subscribe attempt. Previously, a subscribe failure at startup (Redis unreachable at that exact moment) either crashed the host outright or left the listener permanently dead until a manual restart of the service. Now a transient Redis hiccup (down, slow/flaky during startup, auth handshake race) self-heals within seconds/minutes — the listener keeps retrying in the background and starts working the moment Redis becomes reachable, with every request in the meantime still correctly falling back to Layer 2. This mirrors the identical fix applied to `NasheedTenantConfigUpdatedListenerService` (`tenant:updated`) — see `Doc/MULTI_TENANCY_GUIDE.md` → "Live Config-Change Push for Local-Snapshot Consumers".

### `UseDefaultDatabaseMigration` — Safety Net Only

- Remains in the middleware pipeline as a fallback
- **Must be registered BEFORE `UseTenantResolution`** — see ordering rules below
- NOT the primary migration mechanism for global DBs (that is `InitializeDatabaseAsync`)

---

## How It Works

### Automatic Migration Flow

**Scenario 1: Multi-Tenancy Enabled (x-tenant-id header REQUIRED)**

```
1. Request arrives with x-tenant-id header
   │
   ↓
2. TenantMiddleware resolves tenant configuration from Tenant Service
   │
   ↓
3. DatabaseMigrationMiddleware (Tenant) checks if tenant's database exists
   │
   ├─ Database exists and is up-to-date → Continue
   │
   └─ Database missing or needs migration
      │
      ↓
4. Automatically:
   ├─ Creates the tenant database if it doesn't exist
   ├─ Applies all pending migrations
   └─ Caches result (won't check again for this tenant)
      │
      ↓
5. Request proceeds to your handlers with tenant-specific configuration
```

**⚠️ If x-tenant-id header is missing when MultiTenancy is enabled:**

```
1. Request arrives WITHOUT x-tenant-id header
   │
   ↓
2. TenantMiddleware: No tenant resolved
   │
   ↓
3. Request fails - tenant header is required when multi-tenancy is enabled
```

**Scenario 2: Multi-Tenancy Disabled**

```
1. Request arrives (no x-tenant-id header needed)
   │
   ↓
2. DefaultDatabaseMigrationMiddleware checks if default database exists
   │
   ├─ Database exists and is up-to-date → Continue
   │
   └─ Database missing or needs migration
      │
      ↓
3. Automatically:
   ├─ Creates the database from appsettings.json if it doesn't exist
   ├─ Applies all pending migrations
   └─ Caches result (won't check again)
      │
      ↓
4. Request proceeds to your handlers with appsettings.json configuration
```

### What Gets Automatically Created

When a tenant's database doesn't exist, the system:

- ✅ Creates the database on the database server
- ✅ Applies all EF Core migrations from the service
- ✅ Creates all tables, indexes, and constraints
- ✅ Logs the process for monitoring

**Result:** The tenant can immediately use the service without manual database setup!

---

## Configuration

### Enable Automatic Migration (Already Configured)

The automatic migration is **enabled by default** for ALL services. It works whether multi-tenancy is enabled or disabled!

**With Multi-Tenancy Enabled - `appsettings.json`:**

```json
{
  "MultiTenancy": {
    "Enabled": true, // Auto-migration works for tenant-specific databases
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres;"
    // This is used as fallback when no tenant header is provided
  }
}
```

**With Multi-Tenancy Disabled - `appsettings.json`:**

```json
{
  "MultiTenancy": {
    "Enabled": false // Auto-migration works for the default database below
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres;"
    // Auto-migration will ensure this database exists on first request
  }
}
```

### Service Registration

**In `Program.cs` - Add database migration service:**

```csharp
// Database Configuration
builder.Services.AddDatabaseContext<IdentityDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(IdentityDbContext).Assembly.GetName().Name);

// Add database migration service for automatic database creation
builder.Services.AddDatabaseMigration();
```

### Middleware Registration (Already Done)

**In `Program.cs` (Strategy B example — Identity, FileManager, Category):**

```csharp
var app = builder.Build();

// ── LAYER 1: Startup migration (before app.Run) ───────────────────────────────
// Migrates the global/fallback DB before hosted services start.
// Never guard with IsDevelopment() or !MultiTenancy:Enabled.
await app.Services.InitializeDatabaseAsync<IdentityDbContext>(applyMigrations: true, seedData: true);

// ── LAYER 2: Middleware pipeline (ORDER IS CRITICAL) ──────────────────────────
// UseDefaultDatabaseMigration MUST come before UseTenantResolution.
// When multi-tenancy is enabled, AddDatabaseContext leaves IsConfigured=false so
// OnConfiguring picks the connection string using ITenantContext at resolution time.
// Running UseDefaultDatabaseMigration after UseTenantResolution causes its static
// _isMigrated flag to fire against the tenant DB, permanently skipping the global DB.
app.UseDefaultDatabaseMigration<IdentityDbContext>();      // safety net — global DB

app.UseTenantResolution(builder.Configuration);            // sets ITenantContext
app.UseTenantAwareCors();
app.UseJwtTenantVerification(builder.Configuration);

var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false);
if (multiTenancyEnabled)
{
    // Migrates each tenant's DB on their first request
    app.UseTenantDatabaseMigration<IdentityDbContext>(builder.Configuration);
}

app.UseAuthentication();
app.UseAuthorization();
```

**✅ Correct full order:**

| Step | Where | What | Why |
|---|---|---|---|
| 1 | Before `app.Run()` | `InitializeDatabaseAsync` | Global DB migrated before background services start |
| 2 | Middleware | `UseDefaultDatabaseMigration` | Safety net; must be before tenant resolution |
| 3 | Middleware | `UseTenantResolution` | Sets `ITenantContext` on each request |
| 4 | Middleware | `UseTenantAwareCors` + `UseJwtTenantVerification` | Tenant-aware CORS and JWT validation |
| 5 | Middleware | `UseTenantDatabaseMigration` (if enabled) | Per-tenant DB migration on first request |
| 6 | Middleware | `UseAuthentication` / `UseAuthorization` | JWT validation |

**❌ Old pattern (wrong — do not use):**

```csharp
// WRONG: if-else approach left global DB unmigrated when multi-tenancy was on
var multiTenancyEnabled = ...;
if (multiTenancyEnabled)
    app.UseTenantDatabaseMigration<MyDbContext>(...);
else
    app.UseDefaultDatabaseMigration<MyDbContext>();
```

---

## Real-World Examples

### Scenario 1: Default Database (Multi-Tenancy Disabled or No Tenant Header)

**Configuration in `appsettings.json`:**

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres;"
  }
}
```

**Step 1: Database Doesn't Exist Yet**

The database `identity` doesn't exist on the PostgreSQL server - that's OK!

**Step 2: User Makes First Request (No Tenant Header)**

```bash
POST https://localhost:5001/api/auth/login

{
  "email": "admin@example.com",
  "password": "Admin123!"
}
```

**What Happens Automatically:**

```
1. Request arrives without x-tenant-id header
2. DatabaseMigrationMiddleware detects default database scenario
3. Checks if database "identity" exists
4. Database doesn't exist!
5. Automatically:
   ├─ Creates database: identity
   ├─ Applies migrations (creates Users table, etc.)
   └─ Logs: "Database migration check completed successfully for default database"
6. Request proceeds normally
7. Login completes successfully ✅
```

**Step 3: Subsequent Requests**

For all future requests:

- Database already exists
- Migration check is cached in memory
- No additional overhead
- Instant request processing

---

### Scenario 2: Onboarding a New Tenant (Multi-Tenancy Enabled)

**Step 1: Create Tenant via Tenant Service API**

```bash
POST https://localhost:5002/api/admin/tenant
Authorization: Bearer {admin_token}

{
  "tenantId": "acme-corp-123",
  "tenantName": "Acme Corporation",
  "userId": 1,
  "startDate": "2025-01-01T00:00:00Z",
  "expireDate": "2026-01-01T00:00:00Z",
  "data": "{\"Database\":{\"Provider\":\"PostgreSql\",\"ConnectionString\":\"Host=localhost;Database=tenant_acme_123;Username=postgres;Password=postgres\"}}"
}
```

**Note:** The database `tenant_acme_123` doesn't exist yet - that's OK!

**Step 1.5: Eager Migration Fires Automatically (Layer 3, no request needed)**

Immediately after the tenant is created, `CreateTenantCommandHandler` publishes `tenant:provisioned` on Redis. Every already-running multi-tenant service with `AddTenantProvisioningListener<TContext>` registered picks it up within milliseconds and migrates + seeds `tenant_acme_123` right then — **before any user ever makes a request.** This is what makes the "restart every service after adding a tenant" workaround unnecessary.

**Step 2: User Makes First Request**

```bash
POST https://localhost:5001/api/auth/register
x-tenant-id: acme-corp-123

{
  "email": "john@acme.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe"
}
```

**What Happens Automatically:**

```
1. TenantMiddleware extracts tenant ID: "acme-corp-123"
2. Fetches configuration from Tenant Service
3. DatabaseMigrationMiddleware checks database
4. Database was already migrated by Layer 3 when the tenant was created — cache hit, skip check
   (If Layer 3 didn't run — Redis disabled, or this service started after tenant creation —
    the database doesn't exist yet and this middleware creates + migrates it here instead.)
5. Request proceeds normally
6. User registration completes successfully ✅
```

**Step 3: Subsequent Requests**

For all future requests from this tenant:

- Database already exists
- Migration check is cached in memory
- No additional overhead
- Instant request processing

---

## Benefits

### 1. Zero Manual Database Setup

**Before:**

```bash
# Manual steps required:
1. Create database manually on server
2. Run migrations manually:
   dotnet ef database update --connection "Host=...;Database=tenant_acme_123"
3. Verify tables created
4. Test tenant can connect
```

**After:**

```bash
# Automatic - no manual steps!
# Just create tenant config → First request creates database
```

### 2. Faster Tenant Onboarding

- **Traditional:** 15-30 minutes (manual setup + verification)
- **Automatic:** < 5 seconds (first request handles everything)

### 3. Reduced Errors

- ❌ No forgot to run migrations
- ❌ No wrong connection string
- ❌ No missing tables
- ✅ Always correct schema
- ✅ Always up-to-date migrations

### 4. Easy Development

```csharp
// Testing with new tenant? Just add tenant config!
// No need to manually create test databases
var tenant = new TenantInfo
{
    TenantId = "test-tenant-1",
    Configuration = new TenantConfiguration
    {
        Database = new DatabaseSettings
        {
            ConnectionString = "Host=localhost;Database=test_tenant_1;..."
        }
    }
};

// First request automatically creates database ✅
```

---

## Performance Considerations

### Caching Strategy

The migration check is cached **per database in memory**:

```csharp
// First request to default database (no tenant)
Default DB → Check database → Migrate → Cache ✅

// All subsequent requests to default database
Default DB → Cache hit → Skip check → Instant ⚡

// First request from tenant
Tenant: acme-corp-123 → Check database → Migrate → Cache ✅

// All subsequent requests from same tenant
Tenant: acme-corp-123 → Cache hit → Skip check → Instant ⚡

// Different tenant
Tenant: widget-inc-456 → Check database → Already migrated → Cache ✅
```

### Performance Impact

| Scenario                    | Overhead           | Notes                                   |
| --------------------------- | ------------------ | --------------------------------------- |
| **First request**           | 2-5 seconds        | One-time database creation              |
| **Cached requests**         | < 0.1 milliseconds | Memory cache check only                 |
| **Database already exists** | < 50 milliseconds  | Connection check only (first time)      |
| **Migration needed**        | 1-3 seconds        | Apply new migrations (rare after setup) |

### Scalability

- **Single instance:** Caches in memory (works perfectly)
- **Multiple instances:** Each instance caches independently (still works, minimal duplication)
- **High traffic:** Negligible impact (cache hit rate > 99.9%)

---

## Monitoring & Logging

### Log Messages You'll See

#### Default Database Creation

```
[Debug] First request using default database, checking migration status...
[Information] Database for 'default' does not exist. Creating and migrating... (Context: IdentityDbContext)
[Information] Database for 'default' created and migrated successfully (Context: IdentityDbContext)
[Information] Database migration check completed successfully for default database
```

#### Tenant Database Creation

```
[Debug] First request for tenant 'acme-corp-123', checking database migration status...
[Information] Database for tenant 'acme-corp-123' does not exist. Creating and migrating... (Context: IdentityDbContext)
[Information] Database for tenant 'acme-corp-123' created and migrated successfully (Context: IdentityDbContext)
[Information] Database migration check completed successfully for tenant 'acme-corp-123'
```

#### Pending Migrations Applied

```
[Information] Found 3 pending migration(s) for tenant 'acme-corp-123'. Applying... (Context: IdentityDbContext)
[Information] Migrations applied successfully for tenant 'acme-corp-123' (Context: IdentityDbContext)
```

#### Database Up-to-Date

```
[Debug] Database for tenant 'acme-corp-123' is up to date (Context: IdentityDbContext)
```

### Health Monitoring

Monitor these metrics:

- Number of tenant databases created per day
- Average migration time per tenant
- Failed migration attempts
- Tenants with pending migrations

---

## Error Handling

### What If Database Creation Fails?

The system is **fault-tolerant**:

```csharp
// If migration fails:
1. Logs error with full details
2. Continues processing request anyway
3. Database operations will fail naturally
4. Returns appropriate error to client

// Example log:
[Warning] Database migration check failed for tenant 'acme-corp-123', continuing anyway...
[Error] Failed to ensure database exists for tenant 'acme-corp-123'. Error: Access denied for user 'postgres'
```

**Common Causes:**

- Invalid database credentials
- Database server not accessible
- Insufficient permissions
- Network issues

**Solution:** Fix tenant configuration and retry request

### Retry Mechanism

If database creation fails:

1. Error is logged
2. Request continues (might fail at database query)
3. **Next request will retry** (migration not cached on failure)
4. Once successful, cached forever

---

## Testing

### Test Automatic Migration

**1. Create Test Tenant with Non-Existent Database**

```bash
POST https://localhost:5002/api/admin/tenant
{
  "tenantId": "test-migration-123",
  "tenantName": "Test Tenant",
  "userId": 1,
  "data": "{\"Database\":{\"ConnectionString\":\"Host=localhost;Database=test_auto_migrate;Username=postgres;Password=postgres\"}}"
}
```

**2. Make Request to Service**

```bash
POST https://localhost:5001/api/auth/register
x-tenant-id: test-migration-123

{
  "email": "test@example.com",
  "password": "Test123!",
  "firstName": "Test",
  "lastName": "User"
}
```

**3. Verify Database Created**

```bash
# Connect to PostgreSQL
psql -U postgres -h localhost

# List databases
\l

# Should see: test_auto_migrate

# Connect and check tables
\c test_auto_migrate
\dt

# Should see: Users table (and others)
```

---

## Clearing Migration Cache

For testing or when tenant databases are modified externally:

```csharp
// Clear cache for specific tenant
DatabaseMigrationMiddleware<IdentityDbContext>.ClearMigrationCache("acme-corp-123");

// Clear cache for all tenants
DatabaseMigrationMiddleware<IdentityDbContext>.ClearMigrationCache();
```

**Use Cases:**

- Testing migration process
- Tenant database was deleted and recreated
- New migrations added after tenant created

---

## Integration with Other Services

### Apply to New Services

To enable automatic migration in a new service:

**1. Register Service in Program.cs:**

```csharp
builder.Services.AddMultiTenancy(builder.Configuration);
```

**2. Add Middleware (After Tenant Resolution):**

```csharp
app.UseTenantResolution(builder.Configuration);
app.UseTenantDatabaseMigration<YourDbContext>(builder.Configuration);
app.UseAuthentication();
```

**3. Register the eager (Layer 3) listener alongside your DbContext registration:**

```csharp
builder.Services.AddDatabaseContext<YourDbContext>(builder.Configuration, ...);
builder.Services.AddTenantProvisioningListener<YourDbContext>(builder.Configuration);  // ← Add this!
```

**That's it!** Automatic migration now works for your service — both lazily (Layer 2, on the tenant's first request) and eagerly (Layer 3, the moment the tenant is created, no restart needed).

### Example: Order Service

```csharp
// Order.API/Program.cs

// Register multi-tenancy
builder.Services.AddMultiTenancy(builder.Configuration);
builder.Services.AddDatabaseContext<OrderDbContext>(builder.Configuration);
builder.Services.AddTenantProvisioningListener<OrderDbContext>(builder.Configuration);  // ← Add this!

// ...

// Configure pipeline
app.UseTenantResolution(builder.Configuration);
app.UseTenantDatabaseMigration<OrderDbContext>(builder.Configuration);  // ← Add this!
app.UseAuthentication();
```

Now Orders service automatically creates tenant databases too — and picks up brand-new tenants within milliseconds of creation, not on next restart.

---

## Architecture Components

### Services

**`IDatabaseMigrationService`** (`IhsanDev.Shared.Kernel.Interfaces.Database`)

- Interface for database migration operations
- Used by both middleware components to check and migrate databases

**`DatabaseMigrationService`** (`IhsanDev.Shared.Infrastructure.Services.Database`)

- Implementation of migration service
- Handles database creation and migration logic
- Provides logging and error handling
- Shared by both tenant and default database migration

### Middleware Components

**`DatabaseMigrationMiddleware<TContext>`** (`IhsanDev.Shared.Infrastructure.Middleware`)

- ASP.NET Core middleware for **tenant databases** (multi-tenancy scenarios)
- Requires `x-tenant-id` header to be present
- Migrates tenant-specific database from Tenant Service configuration
- Caches results per tenant in memory
- Only runs when `MultiTenancy:Enabled` is `true`
- **Does NOT handle default database** - tenant header is mandatory

**`DefaultDatabaseMigrationMiddleware<TContext>`** (`IhsanDev.Shared.Infrastructure.Middleware`)

- ASP.NET Core middleware for **default database** (single-tenant scenarios)
- No tenant header required
- Calls migration service for default database from appsettings.json
- Caches result in memory (single check per application lifetime)
- Only runs when `MultiTenancy:Enabled` is `false`

### Extension Methods

**`AddDatabaseMigration()`** (`DatabaseExtensions`)

- Registers database migration service in DI container
- Required for both middleware components

**`UseTenantDatabaseMigration<TContext>()`** (`MultiTenancyExtensions`)

- Registers tenant database migration middleware
- Generic method (works with any DbContext)
- **Only handles tenant-specific databases** (x-tenant-id header required)
- Only runs when `MultiTenancy:Enabled` is `true`
- Used in **if** branch of middleware registration

**`InitializeDatabaseAsync<TContext>(applyMigrations, seedData, maxAttempts, retryDelaySeconds)`** (`DatabaseExtensions`)

- Called on `app.Services` **before `app.Run()`** — not middleware
- Migrates the global/fallback DB at startup, before hosted services start
- Built-in retry loop with per-instance jitter to handle concurrent-startup DB locking
- Defaults: `maxAttempts=3`, `retryDelaySeconds=5` (actual delay = retryDelaySeconds + random jitter)
- Each retry creates a fresh `IServiceScope` so the `DbContext` is never reused after a failure

**`UseDefaultDatabaseMigration<TContext>()`** (`DatabaseExtensions`)

- Safety-net middleware — handles edge cases where startup migration was skipped
- **Must be registered BEFORE `UseTenantResolution`** in the middleware pipeline
- Runs once per application lifetime (static `_isMigrated` flag + semaphore lock)
- Migrates the global/fallback DB using `DatabaseSettings:ConnectionString`

### Layer 3 Components (Eager Tenant Provisioning)

**`TenantProvisionedEventMessage`** (`IhsanDev.Shared.Kernel.Dto.Tenant`)

- Slim Redis Pub/Sub payload: `TenantId`, `SchemaVersion`, `OccurredAt`
- Channel: `tenant:provisioned` (global, not per-tenant — the tenant ID IS the payload)

**`PublishTenantProvisionedAsync()`** (`TenantProvisioningExtensions`, `IhsanDev.Shared.Infrastructure.Extensions`)

- Extension on `IConnectionMultiplexer?` — called by Tenant Service's `CreateTenantCommandHandler` right after it caches the new tenant's configuration
- No-op (does not throw) when `redis` is `null` (Redis disabled) or the publish itself fails — this is a best-effort optimization, not a guaranteed-delivery event

**`TenantProvisioningListenerService<TContext>`** (`IhsanDev.Shared.Infrastructure.Services.Tenant`)

- `BackgroundService` — subscribes to `tenant:provisioned` for the lifetime of the process
- Subscribes via `SubscribeWithRetryAsync` (added August 2026): an exponential-backoff loop — 2s initial delay, doubling up to a 30s cap — that retries the initial `SubscribeAsync` call indefinitely until it succeeds or the host stops, instead of a single subscribe attempt. A failed subscribe is logged and does not crash the host; every tenant simply falls back to Layer 2 until the retry succeeds
- On each message: resolves tenant config, sets `ITenantContext`, calls `IDatabaseMigrationService.EnsureDatabaseExistsAsync`, invokes `SeedAsync()` via reflection if the DbContext defines one, then calls `DatabaseMigrationMiddleware<TContext>.MarkAsMigrated`
- Failures are logged and swallowed — Layer 2 (that tenant's first request) or this service's own next startup warm-up (`TenantWarmupExtensions`) still catches it

**`AddTenantProvisioningListener<TContext>()`** (`TenantProvisioningExtensions`, `IhsanDev.Shared.Infrastructure.Extensions`)

- Registers `TenantProvisioningListenerService<TContext>` as a hosted service
- Only registers when **both** `MultiTenancy:Enabled` and `Redis:Enabled` are `true` — otherwise a no-op, so Layer 2 remains the only migration path (same as before this feature existed)

---

## Best Practices

### 1. Database Credentials

**✅ DO:** Use strong, unique credentials per tenant

```json
{
  "ConnectionString": "Host=localhost;Database=tenant_123;Username=tenant_123_user;Password=strong_random_password"
}
```

**❌ DON'T:** Use same credentials for all tenants

```json
{
  "ConnectionString": "Host=localhost;Database=tenant_123;Username=postgres;Password=postgres"
}
```

### 2. Connection String Validation

**✅ DO:** Validate connection strings before saving tenant config

```csharp
// In Tenant Service
var isValid = await ValidateConnectionStringAsync(connectionString);
if (!isValid)
{
    return BadRequest("Invalid connection string");
}
```

### 3. Migration Monitoring

**✅ DO:** Monitor migration logs in production

```csharp
// Set up alerts for:
- Migration failures
- Long migration times (> 10 seconds)
- Multiple tenants created in short time (potential attack)
```

### 4. Database Limits

**✅ DO:** Set reasonable limits

```csharp
// Check database count before creating tenant
var databaseCount = await GetDatabaseCountAsync();
if (databaseCount >= MAX_TENANTS)
{
    return BadRequest("Maximum tenant limit reached");
}
```

---

## Troubleshooting

### Database Not Created

**Symptoms:** Request fails with "database does not exist" error

**Possible Causes:**

1. Middleware not registered correctly
2. Database credentials invalid
3. Database server not accessible
4. Migration middleware not in correct order

**Solutions:**

```bash
# 1. Check Program.cs - ensure middleware is registered
app.UseTenantDatabaseMigration<IdentityDbContext>(builder.Configuration);

# 2. Verify middleware order
app.UseTenantResolution(builder.Configuration);  // First (if multi-tenancy)
app.UseTenantDatabaseMigration<IdentityDbContext>(builder.Configuration);  // Second
app.UseAuthentication();  // Third

# 3. Test connection string manually
psql "Host=localhost;Database=identity;Username=postgres;Password=postgres"

# 4. Check logs for error details
[Error] Failed to ensure database exists for 'default'...
[Error] Failed to ensure database exists for tenant 'tenant-123'...
```

### Migrations Not Applied

**Symptoms:** Database exists but tables are missing

**Possible Causes:**

1. No migrations in project
2. Migration assembly not configured
3. Migration already cached (false positive)

**Solutions:**

```bash
# 1. Verify migrations exist
dotnet ef migrations list --project Identity.Infrastructure

# 2. Check migration assembly
builder.Services.AddDatabaseContext<IdentityDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(IdentityDbContext).Assembly.GetName().Name);

# 3. Clear cache and retry
DatabaseMigrationMiddleware<IdentityDbContext>.ClearMigrationCache("tenant-123");
```

---

## Summary

### Key Takeaways

✅ **Automatic** - No manual database setup required  
✅ **Fast** - First request creates database (2-5 seconds)  
✅ **Cached** - Subsequent requests have zero overhead  
✅ **Safe** - Fault-tolerant, comprehensive logging  
✅ **Scalable** - Works with multiple instances  
✅ **Flexible** - Easy to integrate with new services

### Configuration Checklist

**For All Services (global DB):**

- [ ] `await app.Services.InitializeDatabaseAsync<TContext>(applyMigrations: true)` called before `app.Run()` — no environment guard
- [ ] `UseDefaultDatabaseMigration<TContext>()` registered in middleware pipeline
- [ ] `UseDefaultDatabaseMigration` is placed **before** `UseTenantResolution` in the pipeline
- [ ] Database credentials valid in `appsettings.json`
- [ ] Database server accessible from service
- [ ] EF Core migrations exist in the Infrastructure project

**For Multi-Tenancy Scenarios (Strategies B/C — additional):**

- [ ] `MultiTenancy:Enabled` set to `true`
- [ ] `UseTenantResolution()` registered after `UseDefaultDatabaseMigration`
- [ ] `UseTenantDatabaseMigration<TContext>()` registered after `UseTenantResolution`
- [ ] Tenant Service is running and accessible
- [ ] Tenant configurations include valid database connection strings

**For Eager (Layer 3) Migration on Tenant Creation (additional, requires Redis):**

- [ ] `Redis:Enabled` set to `true` for this service (Layer 3 is a silent no-op otherwise)
- [ ] `AddTenantProvisioningListener<TContext>(configuration)` registered alongside `AddDatabaseContext<TContext>`
- [ ] Tenant Service has `Redis:Enabled: true` and a working `IConnectionMultiplexer` (needed to publish)
- [ ] Define `public async Task SeedAsync()` on the DbContext if this service needs tenant seed data

**For Services with Background Workers (additional):**

- [ ] `InitializeDatabaseAsync` for the global queue context is confirmed unconditional (not dev-only)
- [ ] Background service is registered **after** database contexts in DI (hosted services start in registration order)

---

## Related Documentation

- [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Multi-database architecture
- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Complete multi-tenancy guide
- [TENANT_MIDDLEWARE_EXPLAINED.md](TENANT_MIDDLEWARE_EXPLAINED.md) - Tenant resolution flow
- [EVENT_DRIVEN_PUBLISHER_PATTERN.md](EVENT_DRIVEN_PUBLISHER_PATTERN.md) - The Transactional Outbox pattern Layer 3 deliberately does *not* use, and why

---

**Last Updated:** July 31, 2026  
**Version:** 3.0.0  
**Status:** ✅ Implemented and Production Ready

**Recent Updates (July 31, 2026 — v3.0.0):**

- **Three-layer migration architecture**: added Layer 3 — eager per-tenant migration + seed fired by a Redis Pub/Sub broadcast (`tenant:provisioned`) the moment `CreateTenantCommandHandler` creates a tenant, via `TenantProvisioningListenerService<TContext>` / `AddTenantProvisioningListener<TContext>()`
- **Removes the "restart every service" workaround** — previously, a newly created tenant was only migrated in an already-running service once that service was restarted (re-triggering `WarmTenantDatabaseMigrationsAsync`) or the tenant made its own first request. Layer 3 migrates (and seeds, via the DbContext's `SeedAsync()` if defined) every subscribed service within milliseconds of tenant creation instead.
- **Best-effort by design, not Outbox-backed** — a missed broadcast (Redis briefly down, service mid-restart) simply falls back to Layer 2 or the next startup warm-up, both already-existing and already-correct; this was a deliberate choice not to add Transactional Outbox overhead (DB table + background processor per service) for what is fundamentally a "do it sooner" optimization, not a correctness-critical event.
- **Wired into all 6 per-tenant services**: Identity, FileManager, Category, Nasheed, PolySnap (single DbContext each), and Notification (`TenantNotificationDbContext` only — its global `NotificationDbContext` queue context doesn't need per-tenant migration)

**Recent Updates (June 3, 2026 — v2.0.0):**

- **Two-layer migration architecture**: replaced the old if-else approach with `InitializeDatabaseAsync` (startup) + `UseTenantDatabaseMigration` (per-request)
- **`InitializeDatabaseAsync` is now unconditional** — removed `IsDevelopment()` and `!MultiTenancy:Enabled` guards that prevented global DB migration in production and when multi-tenancy was on
- **Retry logic added to `InitializeDatabaseAsync`** — built-in retry with jitter handles concurrent-startup DB locking when multiple instances deploy simultaneously
- **Middleware ordering bug fixed across all services** — `UseDefaultDatabaseMigration` now correctly placed before `UseTenantResolution` in Identity, FileManager, and Notification
- **Background service race condition resolved** — `InitializeDatabaseAsync` running before `app.Run()` ensures schema exists before `OutboxEventProcessorService`, `NotificationProcessor`, and `CleanupService` start polling

**Built with ❤️ for seamless tenant onboarding**
