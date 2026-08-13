# Performance Optimization Guide

**Last Updated:** August 13, 2026 \
**Status:** ✅ Production Ready

> **Audit note (August 2026):** the "Database Pool" and "Queue Processor"/"Parallel Processing" rows below previously said to raise `MaxPoolSize` to 200-300 and to tune a `MaxParallelTenants` setting — neither matches the current codebase. Per-tenant Npgsql pools are deliberately **capped low** (`DatabaseSettings:MaxPoolSizePerTenant`, default 20) via `NpgsqlConnectionStringHelper.WithBoundedPoolSize`, and there is no `MaxParallelTenants` config anywhere in the repo — `NotificationProcessor` fans out one task per tenant group present in the current batch (`Task.WhenAll`), with no separate throttle. Corrected below against current source.

This guide consolidates the patterns that unlocked 25x throughput and 100k+ SignalR connections in the Notification Service and supporting microservices.

---

## Core Pillars

1. **Batching & Parallelism**
   - Dynamic batch sizing (50–500) based on queue depth (`NotificationProcessor.CalculateBatchSizeAsync`).
   - Parallel processing per tenant to guarantee isolation — `NotificationProcessor` groups a batch by `TenantId` and fans out one `Task` per tenant group via `Task.WhenAll` (`ProcessQueueAsync`). There is **no `MaxParallelTenants` config knob** — the fan-out is unbounded (one task per tenant group present in the current batch, not a tunable degree of parallelism); the natural ceiling is the batch size itself (≤ `MaxBatchSize`, default 500).
   - Multi-tenant parallel operations (5-50x speedup for cross-tenant tasks — global notifications persisting/broadcasting to every active tenant in parallel via `Task.WhenAll`, see `PersistGlobalNotificationToAllTenantsAsync`/`SendViaFirebaseAsync`).
2. **Caching & State**
   - Redis backplane plus in-memory fallback (`AddCacheService` — Redis when `Redis:Enabled=true`, otherwise `IMemoryCache`; see `CACHING_STRATEGY_COMPARISON.md`).
   - Tenant configuration cached for 30 minutes (`MultiTenancy:CacheExpirationMinutes`) with automatic invalidation on tenant save.
3. **Database Efficiency**
   - Composite indexes on queue tables (`TenantId`, `Status`, `NextRetryAt`).
   - Cleanup jobs limited to filtered batches to avoid table scans — `NotificationCleanupJob` (Hangfire, hourly) deletes old queue rows in a loop of 1,000-row batches (`const int batchSize = 1000` in `NotificationCleanupJob.RunAsync`) until fewer than a full batch remains, with a 100ms pause between batches.
   - Per-tenant Npgsql connection pools are **capped, not increased** — `NpgsqlConnectionStringHelper.WithBoundedPoolSize()` caps `Maximum Pool Size` on every dynamically-resolved tenant connection string (Identity, FileManager, Category, Notification's `TenantNotificationDbContext`, Nasheed, PolySnap), default **20** connections/tenant/service, configurable via `DatabaseSettings:MaxPoolSizePerTenant`. This is deliberately a low, governed default — without it, N tenants × M services multiplies into an ungoverned number of Npgsql pools (see `LOAD_TESTING_GUIDE.md`). It is separate from a service's own static/global-DB pool (e.g. Notification's own `Maximum Pool Size=500` on its queue database connection string), which is unrelated to per-tenant fan-out.
4. **Resiliency & Backpressure**
   - Exponential backoff on retries; jitter prevents stampedes.
   - Rate limiting: 100k/min global, 10k/min per tenant, 2k/min per user.
5. **Infrastructure**
   - PostgreSQL primary/replica pair with health-checked failover.
   - Redis cluster or Azure Cache for horizontal scaling.

---

## Checklist

| Area                | Action Item                                             | Status |
| ------------------- | ------------------------------------------------------- | ------ |
| Startup             | Enable health checks and readiness probes               | ✅     |
| Queue Processor     | Tune `MinBatchSize`/`MaxBatchSize` (`NotificationProcessing` config) to match load | ✅     |
| SignalR Hub         | Configure Redis backplane connection resiliency         | ✅     |
| Database            | Apply migration `AddNextRetryAtAndOptimizedIndexes`     | ✅     |
| Database Pool       | Cap `DatabaseSettings:MaxPoolSizePerTenant` (default 20) via `NpgsqlConnectionStringHelper.WithBoundedPoolSize` — do not raise it to 200-300, that reintroduces the ungoverned-pool-count problem this setting exists to prevent | ✅     |
| Cleanup             | `NotificationCleanupJob` (Hangfire, hourly) deletes in 1,000-row batches until queue is lean | ✅     |
| Parallel Processing | Per-tenant fan-out in `NotificationProcessor` is unbounded (one task per tenant group in the batch) — monitor DB CPU since there is no `MaxParallelTenants` throttle | ✅     |
| Monitoring          | Track queue depth, CPU, and Redis latency               | ✅     |

---

## Capacity Planning

| Metric                         | Target                        |
| ------------------------------ | ----------------------------- |
| Notification throughput        | 15,000 notifications / minute |
| SignalR concurrent connections | 100,000+                      |
| Database connections           | 500 (pooled)                  |
| Redis latency                  | < 5 ms p95                    |
| Tenant config cache hit rate   | ≥ 95%                         |

---

## Tuning Tips

- **NotificationProcessor:** Tune `NotificationProcessing:MinBatchSize`/`MaxBatchSize`/`ProcessingIntervalSeconds` while monitoring DB CPU — there is no `MaxParallelTenants` setting to increase (see the correction note above).
- **Rate Limiting:** Adjust per-tenant quotas through configuration for premium plans.
- **Redis:** Use `abortConnect=false` and reconnect policies to tolerate failovers.
- **EF Core:** Disable tracking for read-only queries to cut allocations by ~30%.
- **Logging:** The platform does not use Serilog — logging is the custom, non-blocking channel-based `LoggerManager` (`IhsanDev.Shared.Infrastructure/Services/Logging/`, see `Doc/CUSTOM_LOGGER_USAGE.md`). Its `Information`/`Debug` channel already sheds load under sustained heavy traffic (drops newest when its 20,000-entry bounded channel fills); there is no separate "sample verbose logs" toggle to configure.

---

## Supporting Documents

- [DATABASE_REPLICATION_SETUP_GUIDE.md](DATABASE_REPLICATION_SETUP_GUIDE.md)
- [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md)
- [LOAD_TESTING_GUIDE.md](LOAD_TESTING_GUIDE.md) — k6 scripts and empirically-measured bottlenecks (gateway connection ceiling, per-IP rate limit, per-tenant DB pooling) that this guide's capacity targets should be validated against
