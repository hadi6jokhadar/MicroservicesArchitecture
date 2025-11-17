# Performance Optimization Guide

**Last Updated:** November 18, 2025 \
**Status:** ✅ Production Ready

This guide consolidates the patterns that unlocked 25x throughput and 100k+ SignalR connections in the Notification Service and supporting microservices.

---

## Core Pillars

1. **Batching & Parallelism**
   - Dynamic batch sizing (50–500) based on queue depth.
   - Parallel processing per tenant to guarantee isolation.
2. **Caching & State**
   - Redis backplane plus in-memory fallback (see `CACHING_STRATEGY_COMPARISON.md`).
   - Tenant configuration cached for 30 minutes with automatic invalidation.
3. **Database Efficiency**
   - Composite indexes on queue tables (`TenantId`, `Status`, `NextRetryAt`).
   - Cleanup jobs limited to filtered batches (≤ 5k records) to avoid table scans.
4. **Resiliency & Backpressure**
   - Exponential backoff on retries; jitter prevents stampedes.
   - Rate limiting: 100k/min global, 10k/min per tenant, 2k/min per user.
5. **Infrastructure**
   - PostgreSQL primary/replica pair with health-checked failover.
   - Redis cluster or Azure Cache for horizontal scaling.

---

## Checklist

| Area            | Action Item                                             | Status |
| --------------- | ------------------------------------------------------- | ------ |
| Startup         | Enable health checks and readiness probes               | ✅     |
| Queue Processor | Tune `MaxParallelTenants` to match CPU cores            | ✅     |
| SignalR Hub     | Configure Redis backplane connection resiliency         | ✅     |
| Database        | Apply migration `AddNextRetryAtAndOptimizedIndexes`     | ✅     |
| Cleanup         | Run `CleanupService` every 5 minutes with 5k batch size | ✅     |
| Monitoring      | Track queue depth, CPU, and Redis latency               | ✅     |

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

- **NotificationProcessor:** Increase `MaxParallelTenants` gradually while monitoring DB CPU.
- **Rate Limiting:** Adjust per-tenant quotas through configuration for premium plans.
- **Redis:** Use `abortConnect=false` and reconnect policies to tolerate failovers.
- **EF Core:** Disable tracking for read-only queries to cut allocations by ~30%.
- **Logging:** Use structured logging with Serilog sinks; sample verbose logs under heavy load.

---

## Supporting Documents

- [BOTTLENECKS_COMPLETION_SUMMARY.md](BOTTLENECKS_COMPLETION_SUMMARY.md)
- [DATABASE_REPLICATION_SETUP_GUIDE.md](DATABASE_REPLICATION_SETUP_GUIDE.md)
- [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md)
- [NOTIFICATION_HUB_GUIDE.md](NOTIFICATION_HUB_GUIDE.md)
