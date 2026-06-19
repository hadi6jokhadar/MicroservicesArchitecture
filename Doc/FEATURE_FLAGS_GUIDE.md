# Feature Flags Guide

**Status:** Implemented  
**Stack:** .NET 10 · Tenant-configuration-driven · No external dependency

---

## Overview

Feature flags let you enable or disable capabilities per tenant without deploying new code. Flags live inside the tenant's existing `data` JSON blob — no new service, table, or infrastructure needed.

---

## How It Works

1. A tenant's configuration payload includes a `featureFlags` dictionary.
2. At request time, `TenantFeatureFlagService` reads that dictionary from the resolved `ITenantContext`.
3. Any handler, middleware, or background service can check `IFeatureFlagService.IsEnabled("flagName")` to gate behavior.

---

## Tenant Configuration Payload

Add flags inside the `featureFlags` key when creating or updating a tenant:

```json
{
  "featureFlags": {
    "aiChatEnabled": true,
    "nasheedIngestionEnabled": false
  }
}
```

Flags not present in the dictionary fall back to the `defaultValue` passed to `IsEnabled` (defaults to `false` unless overridden at the call site).

---

## Flag Name Constants

All flag name strings are defined in `IhsanDev.Shared.Application/Constants/FeatureFlags.cs`:

```csharp
public static class FeatureFlags
{
    public const string AiChatEnabled = "aiChatEnabled";
    public const string NasheedIngestionEnabled = "nasheedIngestionEnabled";
}
```

Add new flags here — never use raw strings at call sites.

---

## Using `IFeatureFlagService` in a Handler

```csharp
// Inject in constructor
private readonly IFeatureFlagService _featureFlags;

// Check in Handle()
if (!_featureFlags.IsEnabled(FeatureFlags.AiChatEnabled, defaultValue: true))
    throw new ForbiddenException(LocalizationKeys.Exceptions.FeatureNotEnabled);
```

- `defaultValue: true` — flag is ON unless explicitly set to `false` (safe for rollouts where tenants haven't configured the flag yet).
- `defaultValue: false` — flag is OFF unless explicitly set to `true` (safe for new/experimental features).

---

## Using Feature Flags in Background Services

Background services run outside of a request scope, so `ITenantContext` is not available. Access the tenant cache directly:

```csharp
private bool IsIngestionEnabled()
{
    var flags = _tenantCache.Tenant?.Configuration?.FeatureFlags;
    return flags is null || !flags.TryGetValue(FeatureFlags.NasheedIngestionEnabled, out var enabled) || enabled;
}
```

The pattern `flags is null || !flags.TryGetValue(...)` defaults to **enabled** when the flag key is absent — preserving existing behavior for tenants that haven't set the flag.

---

## DI Registration

`AddFeatureFlagService()` registers `TenantFeatureFlagService` as Scoped. Call it from any service that uses multi-tenancy:

```csharp
// Program.cs — after AddMultiTenancy()
builder.Services.AddFeatureFlagService();
```

---

## Current Gates

| Flag | Default | Gates |
|---|---|---|
| `aiChatEnabled` | `true` | `GenerateLyricsCommandHandler` — returns 403 if disabled |
| `nasheedIngestionEnabled` | `true` | `NasheedIngestionWorker` — skips the poll cycle if disabled |

---

## Adding a New Flag

1. Add the constant to `IhsanDev.Shared.Application/Constants/FeatureFlags.cs`.
2. Check it via `IFeatureFlagService.IsEnabled(FeatureFlags.YourFlag)` in the handler/service.
3. Document it in the table above.
4. Update the tenant admin API docs to document the payload key.

---

## Updating a Tenant's Flags

Use the standard `PUT /api/v1/admin/tenant/{tenantId}` endpoint and include the `featureFlags` section in the `data` payload. The updated configuration is cached in Redis and re-read on the next request/poll cycle.

---

## Architecture Notes

- Flags are **per-tenant** — there is no global override.
- Flag changes take effect after the tenant config cache expires (default: 30 minutes) or on next service restart.
- The `TenantConfiguration.FeatureFlags` dictionary is deserialized from the stored JSON — unknown keys are silently ignored, missing keys default to `false`.
