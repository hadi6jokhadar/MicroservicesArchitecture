# Tenant Timezone Guide

**Status:** Implemented
**Stack:** .NET 10 · Tenant-configuration-driven · No external dependency

---

## Overview

Every DateTime stored and transmitted by the platform is UTC (see "DateTime Standardization" in the backend `CLAUDE.md`). That's correct for storage, but it leaves a gap: nothing lets a service reason about a tenant's **local wall-clock time** — e.g. "is it business hours for this tenant right now" or "run this tenant's digest at their local 8am".

This is a **different concept** from the frontend converting UTC to the end user's device timezone for display. That conversion is per-*viewer*. This feature is per-*tenant* — the business timezone a tenant operates in, used server-side (background jobs, business-rule evaluation), independent of who happens to be looking at a screen.

Like Feature Flags, the tenant's timezone lives inside the tenant's existing `data` JSON blob — no new service, table, or infrastructure.

---

## How It Works

1. A tenant's configuration payload includes an optional `timeZoneId` string — an **IANA** identifier (e.g. `"Europe/Istanbul"`), not a Windows identifier (e.g. `"Turkey Standard Time"`). .NET 6+ resolves IANA ids cross-platform via the bundled ICU data, so this stays portable if a service ever runs on Linux/containers.
2. At request time, `TenantTimeService` reads `timeZoneId` from the resolved `ITenantContext` and falls back to `"UTC"` if it's missing.
3. Background jobs that loop over many tenants have no single "current" tenant, so they call the dependency-free `TenantTimeZoneResolver` directly per tenant instead of going through `ITenantContext`.
4. `TimeZoneId` is validated at tenant create/update time — an invalid IANA id is rejected up front rather than silently falling back to UTC later.

---

## Tenant Configuration Payload

Add the id inside the `data` payload when creating or updating a tenant:

```json
{
  "timeZoneId": "Europe/Istanbul"
}
```

Omitting `timeZoneId` (or leaving it null/empty) is valid — it means "no business timezone configured", and every consumer falls back to UTC.

---

## Fallback Behavior (explicit)

| Condition | Result |
|---|---|
| Tenant has no `timeZoneId` set | Falls back to `"UTC"` |
| Tenant's `timeZoneId` is an invalid/unknown IANA id | Rejected at create/update time by `CreateTenantCommandValidator` / `UpdateTenantCommandValidator`; if bad data still exists at read time, the resolver falls back to `"UTC"` rather than throwing |
| Multi-tenancy disabled / no tenant context | `ITenantTimeService.TimeZoneId` returns `"UTC"` |

---

## Core Utility — `TenantTimeZoneResolver` (`IhsanDev.Shared.Kernel/Utilities`)

Dependency-free static class — no DI, no `ITenantContext`. Use this directly in code that iterates over multiple tenants (background jobs, batch processors):

```csharp
public static class TenantTimeZoneResolver
{
    public const string DefaultTimeZoneId = "UTC";

    public static TimeZoneInfo Resolve(string? timeZoneId);
    public static DateTime ConvertUtcToTenantLocal(DateTime utc, string? timeZoneId);
    public static DateTime ConvertTenantLocalToUtc(DateTime local, string? timeZoneId);
    public static bool IsValidTimeZoneId(string timeZoneId);
}
```

`Resolve` and the `Convert*` methods never throw on a bad or missing id — they fall back to `TimeZoneInfo.Utc`.

### Usage in a background job

```csharp
// Looping over multiple tenants — there is no single "current" tenant here
foreach (var tenant in activeTenants)
{
    var tenantLocalNow = TenantTimeZoneResolver.ConvertUtcToTenantLocal(
        DateTime.UtcNow,
        tenant.Configuration?.TimeZoneId);

    if (tenantLocalNow.Hour == 8)
    {
        // e.g. send this tenant's local-8am digest
    }
}
```

---

## Request-Scoped Wrapper — `ITenantTimeService` (`IhsanDev.Shared.Application` / `IhsanDev.Shared.Infrastructure`)

```csharp
public interface ITenantTimeService
{
    string TimeZoneId { get; }        // resolved id, or "UTC" if unset
    TimeZoneInfo TimeZone { get; }
    DateTime ConvertUtcToTenantLocal(DateTime utc);
    DateTime ConvertTenantLocalToUtc(DateTime local);
}
```

`TenantTimeService` reads `ITenantContext.CurrentTenant?.Configuration?.TimeZoneId`. Use this in request-scoped handlers/services where `ITenantContext` is already available:

```csharp
// Inject in constructor
private readonly ITenantTimeService _tenantTime;

// Use in Handle()
var localDeadline = _tenantTime.ConvertUtcToTenantLocal(entity.DeadlineUtc);
```

---

## DI Registration

`AddTenantTimeService()` registers `TenantTimeService` as Scoped. Call it from any service's `Program.cs` that uses multi-tenancy and needs tenant-local-time logic in request-scoped code:

```csharp
// Program.cs — after AddMultiTenancy()
builder.Services.AddTenantTimeService();
```

Not every service needs this wired up — register it only where a handler actually consumes `ITenantTimeService`, the same way `AddFeatureFlagService()` is only called by services that check flags (see `FEATURE_FLAGS_GUIDE.md`).

---

## Validation

`CreateTenantCommandValidator` and `UpdateTenantCommandValidator` (`Tenant.Application/Commands/Tenant/`) validate `Data.TimeZoneId` with `TenantTimeZoneResolver.IsValidTimeZoneId` whenever a value is supplied. An invalid id returns the `validation_invalid_time_zone` localized error instead of being silently accepted and falling back to UTC later.

---

## Architecture Notes

- `TimeZoneId` is **per-tenant** — there is no global override.
- Timezone changes take effect after the tenant config cache expires (default: 30 minutes) or on next service restart — same cache invalidation path as Feature Flags and the rest of `TenantConfiguration`.
- Storing UTC in the database is unaffected — this feature only changes how UTC is *interpreted* for tenant-local business logic, never how it's persisted.
- No new database column or migration is required — `TimeZoneId` is part of the existing `Data` JSON blob on `TenantSettings`, deserialized the same way as `FeatureFlags`, `Cors`, etc.

---

## Adding a Timezone-Gated Background Job

1. Fetch tenants and their `Configuration.TimeZoneId` (already included in the cached `TenantInfo` — no extra query needed).
2. Call `TenantTimeZoneResolver.ConvertUtcToTenantLocal(DateTime.UtcNow, tenant.Configuration?.TimeZoneId)` per tenant to get that tenant's current local time.
3. Branch on the result (e.g. "only process if `tenantLocalNow.Hour == 8`").
4. Do **not** inject `ITenantContext`/`ITenantTimeService` into a job that loops over many tenants — those are request-scoped and assume one "current" tenant.
