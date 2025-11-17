# Caching Strategy Comparison

**Last Updated:** November 18, 2025 \
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
2. **Cache Keys**
   - Tenant configs follow `tenant_config_{tenantId}`.
   - Device tokens and notification payloads use `tenant:{id}:device:{hash}` for sharding.
3. **Warmup Strategy**
   - Preload critical tenants via `TenantConfigurationProvider` during deployment to avoid cold-start latency.
4. **Fallback Behavior**
   - If Redis becomes unavailable, the cache layer automatically fails over to in-memory cache for that process while retrying Redis connections.
   - Log warnings and alert DevOps; horizontal scaling will be limited until Redis recovers.

---

## Related Documentation

- [REDIS_ENABLED_VS_DISABLED_GUIDE.md](REDIS_ENABLED_VS_DISABLED_GUIDE.md)
- [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md)
- [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md)
