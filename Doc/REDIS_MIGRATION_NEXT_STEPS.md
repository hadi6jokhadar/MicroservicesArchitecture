# ✅ Redis Migration - Next Steps Checklist

**Date:** November 10, 2025  
**Current Status:** Code implementation complete, build successful  
**Next Phase:** Runtime testing and deployment

---

## 📋 Immediate Next Steps (Required Before Production)

### Step 1: Install Redis Server ⏳

**Option A: Docker (Recommended for Development)**

```bash
# Start Redis container
docker run -d --name redis-cache -p 6379:6379 redis:7-alpine

# Verify it's running
docker ps | grep redis-cache

# Test connection
redis-cli ping
# Expected output: PONG
```

**Option B: Windows (WSL2)**

```bash
# In WSL2 terminal
sudo apt update
sudo apt install redis-server

# Start Redis
sudo service redis-server start

# Test connection
redis-cli ping
# Expected output: PONG
```

**Option C: Redis Cloud (Production)**

Sign up at https://redis.com/cloud/ or use Azure Cache for Redis

---

### Step 2: Test Basic Cache Functionality ⏳

**2.1 Start Identity Service**

```bash
cd src/Services/Identity/Identity.API
dotnet run
```

**Expected logs:**

```
Redis is enabled
SignalR running without backplane (single instance only)
```

**2.2 Verify Cache Connection**

Check Redis for cache keys:

```bash
redis-cli KEYS "MicroservicesApp:*"
```

**2.3 Test Tenant Configuration Caching**

```bash
# Make API call requiring tenant config
curl -H "x-tenant-id: test-tenant-123" http://localhost:5001/api/some-endpoint

# Check Redis cache
redis-cli GET "MicroservicesApp:tenant_config_test-tenant-123"
```

**Expected:**

- ✅ First request: Cache miss → Calls Tenant Service → Caches result
- ✅ Second request: Cache hit → Returns from Redis (no Tenant Service call)

---

### Step 3: Test SignalR Redis Backplane ⏳

**3.1 Start Notification Service (Instance 1)**

```bash
cd src/Services/Notification/Notification.API
dotnet run --urls "http://localhost:5004"
```

**Expected logs:**

```
SignalR Redis backplane configured with connection: localhost:6379
```

**3.2 Start Notification Service (Instance 2)**

```bash
cd src/Services/Notification/Notification.API
dotnet run --urls "http://localhost:5005"
```

**3.3 Connect SignalR Clients**

Connect client 1 to instance 1 (port 5004)  
Connect client 2 to instance 2 (port 5005)

**3.4 Send Notification**

Send notification to instance 1

**Expected:**

- ✅ Client 1 (connected to instance 1) receives notification
- ✅ Client 2 (connected to instance 2) ALSO receives notification

**This confirms the Redis backplane is working!**

---

### Step 4: Performance Testing ⏳

**4.1 Test Cache Hit Rate**

```bash
# Monitor Redis
redis-cli MONITOR

# Make 100 requests to same endpoint
for i in {1..100}; do
  curl -H "x-tenant-id: test-tenant" http://localhost:5001/api/endpoint
done

# Check hit/miss ratio
redis-cli INFO stats | grep keyspace_hits
redis-cli INFO stats | grep keyspace_misses
```

**Target:** 95%+ hit rate

**4.2 Test Response Time**

```bash
# First request (cache miss)
time curl -H "x-tenant-id: test-tenant" http://localhost:5001/api/endpoint

# Second request (cache hit)
time curl -H "x-tenant-id: test-tenant" http://localhost:5001/api/endpoint
```

**Expected:** Cache hit should be 50-70% faster

**4.3 Monitor Tenant Service Load**

Before Redis:

- 1000 calls/min to Tenant Service

After Redis:

- <200 calls/min to Tenant Service (80% reduction)

---

### Step 5: Update Documentation ⏳

**5.1 Update README.md**

Add section about Redis requirement:

```markdown
## Prerequisites

- .NET 9.0
- PostgreSQL
- **Redis Server** (for distributed caching)
```

**5.2 Update Deployment Guide**

Document Redis setup for different environments

**5.3 Add to Architecture Diagram**

Update diagrams to show Redis layer

---

## 🚀 Deployment Checklist

### Development Environment

- [ ] Install Redis locally (Docker/WSL2)
- [ ] Verify Redis connection (`redis-cli ping`)
- [ ] Set `Redis:Enabled = true` in appsettings.Development.json
- [ ] Start all services
- [ ] Test cache functionality
- [ ] Monitor logs for cache hits
- [ ] Test SignalR multi-instance delivery
- [ ] Run automated tests
- [ ] Verify no errors in logs

### Staging Environment

- [ ] Provision Redis server (managed service)
- [ ] Update connection strings in appsettings.Staging.json
- [ ] Configure Redis persistence (AOF + RDB)
- [ ] Deploy all services
- [ ] Run smoke tests
- [ ] Run load tests
- [ ] Monitor cache performance (24 hours)
- [ ] Verify no memory leaks
- [ ] Check cache hit rate >90%
- [ ] Validate no regressions

### Production Environment

- [ ] Use Azure Cache for Redis or AWS ElastiCache
- [ ] Configure high availability (clustering)
- [ ] Enable Redis persistence
- [ ] Set up monitoring and alerting
- [ ] Update connection strings in appsettings.Production.json
- [ ] Plan deployment window
- [ ] Deploy with canary strategy (10% → 50% → 100%)
- [ ] Monitor cache hit rates
- [ ] Monitor API call reductions
- [ ] Monitor response times
- [ ] Keep rollback plan ready
- [ ] Monitor for 48 hours post-deployment

---

## 📊 Success Metrics

### Cache Performance

- [ ] Cache hit rate >90%
- [ ] Average cache response time <5ms
- [ ] Tenant Service API calls reduced by 80%
- [ ] Overall response time improved by 50%+

### SignalR Performance

- [ ] Multiple Notification Service instances running
- [ ] SignalR messages delivered to all instances
- [ ] No SignalR connection errors
- [ ] Horizontal scaling validated

### System Stability

- [ ] No increase in error rates
- [ ] No memory leaks
- [ ] No Redis connection failures
- [ ] All services healthy

---

## 🔧 Configuration Validation

### Identity Service

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "http://localhost:5002",
    "CacheExpirationMinutes": 30
  }
}
```

- [ ] Redis:Enabled = true ✅
- [ ] Redis:ConnectionString points to valid Redis server ✅
- [ ] Redis:InstanceName is set ✅
- [ ] MultiTenancy:CacheExpirationMinutes configured ✅

### Tenant Service

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  }
}
```

- [ ] Redis configuration added ✅

### Notification Service

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  }
}
```

- [ ] Redis configuration added ✅
- [ ] SignalR Redis backplane enabled in Program.cs ✅

---

## 🧪 Testing Scenarios

### Scenario 1: Cache Hit/Miss

**Test:**

1. Clear Redis cache: `redis-cli FLUSHALL`
2. Make API request with x-tenant-id header
3. Check logs: Should show "Cache miss" and "Tenant Service API call"
4. Make same request again
5. Check logs: Should show "Cache hit"

**Expected:**

- ✅ First request fetches from Tenant Service
- ✅ Second request served from Redis cache
- ✅ No duplicate Tenant Service calls

### Scenario 2: Cache Expiration

**Test:**

1. Set CacheExpirationMinutes = 1
2. Make API request
3. Wait 61 seconds
4. Make same request
5. Check logs

**Expected:**

- ✅ First request caches data
- ✅ After 61 seconds, cache expires
- ✅ Second request fetches from Tenant Service again

### Scenario 3: SignalR Multi-Instance

**Test:**

1. Start Notification Service on port 5004
2. Start Notification Service on port 5005
3. Connect SignalR client to port 5004
4. Connect SignalR client to port 5005
5. Send notification via port 5004

**Expected:**

- ✅ Both clients receive notification
- ✅ Redis backplane broadcasts message

### Scenario 4: Redis Failure Handling

**Test:**

1. Start services with Redis enabled
2. Stop Redis: `docker stop redis-cache`
3. Make API request

**Expected:**

- ⚠️ Cache operations fail gracefully
- ✅ Service continues to work (falls back to direct calls)
- ✅ Logs show cache errors but no service crashes

### Scenario 5: Cache Invalidation

**Test:**

1. Cache tenant config
2. Update tenant config via Tenant Service
3. Call cache invalidation endpoint
4. Make API request

**Expected:**

- ✅ Old cache is cleared
- ✅ New config is fetched and cached

---

## 📈 Monitoring Setup

### Redis Health Checks

```bash
# Add to health check endpoint
redis-cli ping

# Monitor memory
redis-cli INFO memory | grep used_memory_human

# Monitor operations
redis-cli INFO stats | grep instantaneous_ops_per_sec
```

### Application Logs

**Monitor for:**

- ✅ "Cache hit" messages (should be >90%)
- ✅ "SignalR Redis backplane configured"
- ⚠️ "Redis connection failed" (should be 0%)
- ⚠️ "Cache error" (should be <1%)

### Performance Metrics

**Track:**

- Cache hit rate (target: >90%)
- Cache response time (target: <5ms)
- Tenant Service API calls (target: 80% reduction)
- Overall response time (target: 50% improvement)
- Redis memory usage (target: <80%)

---

## 🚨 Rollback Triggers

**Rollback immediately if:**

- ❌ Cache hit rate drops below 50%
- ❌ Response times increase by >30%
- ❌ Redis connection failures >10%
- ❌ Error rate increases by >20%
- ❌ Service becomes unstable
- ❌ SignalR messages not delivered

**Rollback process:**

1. Set `Redis:Enabled = false` in all appsettings.json
2. Restart all services
3. System automatically falls back to memory cache
4. Investigate issues
5. Fix problems
6. Re-enable Redis when ready

---

## 📞 Support Contacts

**If you encounter issues:**

1. Check this checklist
2. Review logs in `Logs/` directory
3. Check Redis: `redis-cli ping`
4. Review migration documentation
5. Test with Redis disabled
6. Contact development team

---

## 📚 Reference Documents

- ✅ `REDIS_CACHE_MIGRATION_PLAN.md` - Complete migration guide
- ✅ `REDIS_CACHE_MIGRATION_SUMMARY.md` - Implementation summary
- ✅ `REDIS_CACHE_QUICK_REFERENCE.md` - Developer quick reference
- ✅ `NOTIFICATION_SERVICE_BOTTLENECKS.md` - Performance issues fixed by Redis

---

## 🎯 Final Checklist Before Production

### Code ✅

- [x] Redis packages added
- [x] ICacheService interface created
- [x] RedisCacheService implemented
- [x] MemoryCacheService fallback implemented
- [x] TenantConfigurationProvider updated
- [x] MultiTenancyExtensions updated
- [x] SignalR Redis backplane added
- [x] All appsettings.json updated
- [x] Solution builds successfully

### Testing ⏳

- [ ] Redis server installed
- [ ] Basic cache functionality tested
- [ ] SignalR multi-instance tested
- [ ] Performance benchmarks run
- [ ] Load testing completed
- [ ] Automated tests passing

### Documentation ⏳

- [ ] README.md updated
- [ ] Deployment guide updated
- [ ] Architecture diagrams updated
- [ ] Monitoring documentation added

### Deployment ⏳

- [ ] Development environment tested
- [ ] Staging environment tested
- [ ] Production Redis provisioned
- [ ] Monitoring configured
- [ ] Rollback plan documented
- [ ] Team trained on new system

---

**Current Status:** 🟡 **Code Complete, Testing Required**  
**Next Action:** Install Redis and begin runtime testing  
**Target Production Date:** TBD (after successful testing)

---

**Last Updated:** November 10, 2025  
**Document Version:** 1.0  
**Author:** AI Assistant
