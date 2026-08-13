# Caching Strategy Comparison

**Last Updated:** August 13, 2026  
**Status:** ✅ Production Ready

This guide compares the two supported caching modes—**Redis distributed cache** and the **in-memory fallback**—so you can pick the right configuration per environment.

---

## Quick Recommendation

| Scenario                        | Recommended Setting                        |
| ------------------------------- | ------------------------------------------ |
| Local development               | `Redis:Enabled = false` (in-memory cache)  |
| Single-instance QA              | `Redis:Enabled = false` (keep it simple)   |
| Multi-instance staging          | `Redis:Enabled = true` (Redis backplane)   |
| Production / horizontal scaling | `Redis:Enabled = true` (shared cache, FCM) |
| SignalR hub with backplane      | `Redis:Enabled = true` (required)          |
| Air-gapped or offline scenarios | `Redis:Enabled = false` (no external dep.) |

---

## Detailed Comparison

| Characteristic            | Redis Enabled                                 | Redis Disabled                                |
| ------------------------- | --------------------------------------------- | --------------------------------------------- |
| Cache implementation      | `RedisCacheService` + `IDistributedCache`     | `MemoryCacheService` + `IMemoryCache`         |
| Scope                     | Shared across all instances                   | Per-process, lost on restart                  |
| Tenant config retrieval   | 95% cache hit rate (shared)                   | 70–85% hit rate, more calls to Tenant Service |
| Notification throughput   | Required for 100k+ SignalR connections        | Limited to single instance                    |
| App restart behavior      | Cache preserved                               | Cache cleared                                 |
| Infrastructure dependency | Redis server/cluster                          | None                                          |
| Cost/complexity           | Higher (managed Redis or container)           | Minimal                                       |
| Failure mode              | Retry logic + circuit breaker on Redis client | Per-instance cache thrash                     |
| Recommended use cases     | Production, HA workloads, multi-tenant SaaS   | Local dev, unit tests, feature spikes         |

---

## Configuration Examples

### Redis Enabled (Production)

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "redis:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://tenant-service"
  }
}
```

### Redis Disabled (Fallback)

```json
{
  "Redis": {
    "Enabled": false
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "http://localhost:5002"
  }
}
```

> The shared abstractions register the correct cache provider automatically; no code changes or recompilation are required.

---

## Operational Guidance

1. **Health Monitoring**
   - When Redis is enabled, monitor connection multiplexer events and latency.
   - Use `redis-cli monitor` or Azure metrics to detect slow commands.
2. **Cache Keys & Namespacing**
   - Tenant configs follow `tenant_config_{tenantId}`.
   - Device tokens and notification payloads use `tenant:{id}:device:{hash}` for sharding.
   - Claims and roles use grouped namespaces: `admin:claims:*` and `admin:roles:*`
3. **Warmup Strategy**
   - Preload critical tenants via `TenantConfigurationProvider` during deployment to avoid cold-start latency.
4. **Fallback Behavior**
   - **Almost every service automatically falls back to MemoryCache when `Redis:Enabled = false`.** Any service that calls `AddMultiTenancy(configuration)` (`IhsanDev.Shared.Infrastructure/Extensions/MultiTenancyExtensions.cs`) gets this for free — `AddMultiTenancy` unconditionally calls `services.AddCacheService(configuration)`, and `RedisCacheExtensions.AddCacheService` reads `Redis:Enabled` itself: `true` registers `AddRedisCache` (Redis-backed `ICacheService`), `false` registers `AddInMemoryCache` (`IMemoryCache`-backed `ICacheService`) — no code changes or extra registration needed either way.
     - **Category, Identity, FileManager, Tenant, Nasheed**: all call `AddMultiTenancy`, so all get the same automatic Redis-or-in-memory fallback via `ICacheService` — confirmed directly in Category's and Nasheed's `Program.cs`. With Redis disabled, these services still start and serve requests correctly; they simply lose cross-instance cache sharing and any Redis-only feature (see below).
     - **Translation & Notification**: in addition to `ICacheService` (via the same `AddMultiTenancy`/`AddCacheService` path), these two also separately register `AddDistributedMemoryCache()` when `Redis:Enabled = false`, because they use `IDistributedCache` directly in places `ICacheService` doesn't cover (e.g. Translation's `GetTranslationsQueryHandler`).
   - **The one genuine Redis-only requirement is Translation's direct `IDistributedCache` usage.** `GetTranslationsQueryHandler` reads/writes `IDistributedCache` directly rather than going through `ICacheService`, and `ICacheService`'s own in-memory fallback path (`AddInMemoryCache`) only registers `IMemoryCache`, not `IDistributedCache` — so Translation.API's `Program.cs` explicitly calls `AddDistributedMemoryCache()` itself when Redis is disabled, to keep that handler working. No other service in this list has an equivalent hard dependency on Redis specifically; every one of Category/Identity/FileManager/Tenant/Nasheed reads and writes exclusively through `ICacheService`, which already abstracts the Redis-vs-memory choice away.
   - Redis is still required for anything that is inherently cross-instance/pub-sub in nature regardless of `ICacheService` (SignalR backplane, `RemoveByPatternAsync` cross-instance cache invalidation, the Category outbox's Redis Pub/Sub delivery) — those genuinely need Redis running, but that's a feature requirement, not a startup/fallback failure.
   - When Redis becomes unavailable at runtime (after startup) for a service that registered it, cache misses increase and calls to the owning service (e.g. Tenant Service) go up; `RedisCacheService` swallows connection errors defensively (see `WarmUpCacheAsync` in `RedisCacheExtensions.cs`), so the service keeps functioning in a degraded state rather than crashing.
   - Log warnings and alert DevOps; horizontal scaling will be limited until Redis recovers.

---

## Related Documentation

- [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md) - SignalR Redis backplane requirements
- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - How tenant configs are cached (Redis vs MemoryCache per service)
- [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Per-tenant database isolation
