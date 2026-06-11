# Hangfire Background Jobs Guide

## Overview

Four platform services use Hangfire 1.8.x for scheduled background jobs: Category, FileManager, Notification, and Tenant. Each service has its own isolated Hangfire schema inside its PostgreSQL database (no shared schema). Dashboards are secured with HTTP Basic Auth and accessed directly per service — not through the API Gateway.

---

## Architecture

### Per-Service Schemas (Not Per-Tenant)

Hangfire stores job state in its own schema inside the service's **global** database. It is completely separate from tenant data:

| Service      | Database      | Hangfire Schema       | Dashboard Path                            |
| ------------ | ------------- | --------------------- | ----------------------------------------- |
| Category     | `category`    | `hangfire_category`   | `http://localhost:5007/admin/jobs/category` |
| FileManager  | `filemanager` | `hangfire_filemanager`| `http://localhost:5005/admin/jobs/filemanager` |
| Notification | `notification`| `hangfire_notification`| `http://localhost:5004/admin/jobs/notification` |
| Tenant       | `tenant`      | `hangfire_tenant`     | `http://localhost:5002/admin/jobs/tenant` |

Hangfire runs once per service process, not once per tenant. Jobs operate on all tenants (e.g., outbox processor publishes events for every tenant's outbox records in a single job run).

### Why Not Through the Gateway

The YARP gateway rewrites paths before forwarding to downstream services. Hangfire renders all its dashboard links as relative URLs based on its mount path. When the gateway rewrites the prefix (e.g., `/admin/jobs/category` → `/admin/jobs/category`), the links in the Hangfire HTML point to paths the gateway doesn't know about, breaking page navigation inside the dashboard. Direct service URLs keep Hangfire's relative URLs correct.

---

## Authentication — HTTP Basic Auth

Each dashboard uses `HangfireBasicAuthFilter` (implements `IDashboardAuthorizationFilter`) with credentials from `appsettings.json`:

```json
"Hangfire": {
  "Dashboard": {
    "Username": "admin",
    "Password": "Hangfire@<ServiceName>2024!"
  }
}
```

### Credentials per Service

| Service      | URL                                               | Password                     |
| ------------ | ------------------------------------------------- | ---------------------------- |
| Category     | `http://localhost:5007/admin/jobs/category`       | `CHANGE_ME_HANGFIRE_PASSWORD`     |
| FileManager  | `http://localhost:5005/admin/jobs/filemanager`    | `CHANGE_ME_HANGFIRE_PASSWORD`  |
| Notification | `http://localhost:5004/admin/jobs/notification`   | `CHANGE_ME_HANGFIRE_PASSWORD` |
| Tenant       | `http://localhost:5002/admin/jobs/tenant`         | `CHANGE_ME_HANGFIRE_PASSWORD`       |

Username for all services: `admin`

### How It Works

When the browser first opens a dashboard URL, the filter returns `401` with a `WWW-Authenticate: Basic realm="Hangfire Dashboard"` header. The browser shows a native login dialog. Once credentials are entered, the browser caches them for the tab session and sends `Authorization: Basic base64(user:pass)` on every subsequent page navigation — solving the problem of Hangfire's page-to-page navigation losing auth state.

### Filter Implementation

Lives in each service's `HangfireExtensions.cs` (e.g., `Category.Infrastructure/Extensions/HangfireExtensions.cs`):

```csharp
internal sealed class HangfireBasicAuthFilter : IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    public HangfireBasicAuthFilter(IConfiguration configuration)
    {
        _username = configuration["Hangfire:Dashboard:Username"]
            ?? throw new InvalidOperationException("Hangfire:Dashboard:Username not configured");
        _password = configuration["Hangfire:Dashboard:Password"]
            ?? throw new InvalidOperationException("Hangfire:Dashboard:Password not configured");
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var header = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        { Challenge(httpContext); return false; }
        try
        {
            var encoded = header["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var colon = decoded.IndexOf(':');
            if (colon < 0) { Challenge(httpContext); return false; }
            if (decoded[..colon] == _username && decoded[(colon + 1)..] == _password) return true;
        }
        catch { }
        Challenge(httpContext);
        return false;
    }

    private static void Challenge(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
    }
}
```

---

## TenantMiddleware Bypass

The `/admin/jobs` path prefix is added to the bypass list in `IhsanDev.Shared.Infrastructure/Middleware/TenantMiddleware.cs` alongside `/health` and `/metrics`:

```csharp
if (path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
    path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
    path.StartsWith("/admin/jobs", StringComparison.OrdinalIgnoreCase))
{
    await _next(context);
    return;
}
```

Without this bypass the tenant middleware would reject the request with a `Missing required header` error before the Hangfire dashboard filter could run.

---

## Recurring Jobs

### Category — Outbox Event Processor

- **Job class:** `OutboxEventProcessorJob`
- **Schedule:** Every minute (`* * * * *`)
- **Purpose:** Reads unprocessed outbox records and publishes them to Redis Pub/Sub. Replaces the previous `OutboxEventProcessorService` polling loop.
- **Conditional registration:** Only when `Redis:Enabled = true` (Category service checks this in `Program.cs`)

### FileManager — Temp File Cleanup

- **Job class:** `TempFileCleanupJob`
- **Schedule:** Daily at 02:00 UTC (`0 2 * * *`)
- **Purpose:** Removes temporary/orphaned files from storage. Replaces the previous `TempFileCleanupService` polling loop.

### Notification — Cleanup

- **Job class:** `NotificationCleanupJob`
- **Schedule:** Hourly (`Cron.Hourly`)
- **Purpose:** Purges expired notification records per retention policy. Replaces the previous `CleanupService` polling loop.

### Tenant — Cache Refresh

- **Job class:** `TenantCacheRefreshJob`
- **Schedule:** Every 30 minutes (`*/30 * * * *`)
- **Purpose:** Proactively refreshes the Redis tenant config cache. Replaces the previous `TenantCacheRefreshService` polling loop.

### NotificationProcessor — Kept as BackgroundService

`NotificationProcessor` is **not** a Hangfire job. It is a sub-second real-time queue poller that must run continuously. Hangfire's minimum schedule granularity is 1 minute — unsuitable for latency-sensitive queue processing.

---

## Registration Pattern

Each service exposes two extension methods from its `HangfireExtensions.cs`:

```csharp
// Registers the dashboard middleware (wires up Basic Auth filter)
public static IApplicationBuilder UseXxxHangfireDashboard(
    this IApplicationBuilder app, IConfiguration configuration)

// Registers all recurring jobs for that service
public static void RegisterXxxRecurringJobs()
```

These are called from each service's `Program.cs` after `app.Build()`:

```csharp
app.UseXxxHangfireDashboard(app.Configuration);
HangfireExtensions.RegisterXxxRecurringJobs();
```

Category is the exception — it guards the registration inside a Redis check:

```csharp
var redisEnabled = app.Configuration.GetValue<bool>("Redis:Enabled", false);
if (redisEnabled)
{
    app.UseCategoryHangfireDashboard(app.Configuration);
    HangfireExtensions.RegisterCategoryRecurringJobs();
}
```

---

## NuGet Packages

Versions are centralized in `Directory.Packages.props`:

| Package | Purpose |
| --- | --- |
| `Hangfire.AspNetCore` | ASP.NET Core integration, `IDashboardAuthorizationFilter` |
| `Hangfire.PostgreSql` | PostgreSQL storage with schema isolation |

---

## Frontend Integration

The Angular `BackgroundJobsService` (in `libs/core/`) opens dashboards in a new tab using direct service URLs from environment config:

```typescript
openDashboard(target: BackgroundJobsService_Target): void {
  window.open(this._baseUrls[target], '_blank');
}
```

Targets: `'category'`, `'filemanager'`, `'notification'`, `'tenant'`.

URLs are built from `ENVIRONMENT.apiUrls.*` — never hardcoded.

---

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| `{"error":"Missing required header",...}` on `/admin/jobs/*` | TenantMiddleware ran before Hangfire | Verify `/admin/jobs` is in the bypass list in `TenantMiddleware.cs` |
| Browser shows 401 repeatedly without login dialog | Response missing `WWW-Authenticate` header | Verify `Challenge()` sets the header correctly |
| Dashboard opens but links return 404 | Using gateway URL instead of direct service URL | Use direct port URL (e.g., `localhost:5007/admin/jobs/category`) |
| Jobs don't appear in dashboard | Redis disabled for Category / service not started | Check `Redis:Enabled` in appsettings; ensure service is running |
| `InvalidOperationException: Hangfire:Dashboard:Username not configured` | Missing appsettings section | Add `"Hangfire": { "Dashboard": { "Username": ..., "Password": ... } }` to service's `appsettings.json` |
