# 🚀 Redis Cache Quick Reference Guide

**For Developers Working with the Microservices Architecture**

---

## 🎯 Quick Start

### Enable Redis Caching

**1. Ensure Redis is running:**

```bash
# Docker
docker run -d --name redis-cache -p 6379:6379 redis:7-alpine

# Verify
redis-cli ping
# Expected: PONG
```

**2. Enable in appsettings.json:**

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  }
}
```

**3. Start your service:**

```bash
dotnet run
```

✅ **That's it!** Your service now uses Redis for distributed caching.

---

## 💻 Using the Cache Service

### Inject ICacheService

```csharp
public class MyService
{
    private readonly ICacheService _cache;

    public MyService(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<User?> GetUserAsync(string userId)
    {
        // Try cache first
        var cacheKey = $"user_{userId}";
        var cachedUser = await _cache.GetAsync<User>(cacheKey);

        if (cachedUser != null)
        {
            return cachedUser; // Cache hit ✅
        }

        // Cache miss - fetch from database
        var user = await _database.GetUserByIdAsync(userId);

        if (user != null)
        {
            // Cache for 10 minutes
            await _cache.SetAsync(cacheKey, user, TimeSpan.FromMinutes(10));
        }

        return user;
    }
}
```

---

## 🔑 Cache Methods

### Get Value

```csharp
var value = await _cache.GetAsync<TenantInfo>("tenant_config_123");
```

### Set Value

```csharp
await _cache.SetAsync(
    key: "user_456",
    value: userObject,
    expiration: TimeSpan.FromMinutes(30)
);
```

### Remove Value

```csharp
await _cache.RemoveAsync("user_456");
```

### Check Existence

```csharp
bool exists = await _cache.ExistsAsync("user_456");
```

### Remove by Pattern (Redis only)

```csharp
// Removes all keys matching pattern: tenant_config_*
await _cache.RemoveByPatternAsync("tenant_config_*");
```

---

## ⚙️ Configuration Options

### Development (Local Redis)

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "Dev:"
  }
}
```

### Production (Azure Cache for Redis)

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "your-app.redis.cache.windows.net:6380,password=your-key,ssl=True,abortConnect=False",
    "InstanceName": "Prod:"
  }
}
```

### Disable Redis (Fallback to Memory Cache)

```json
{
  "Redis": {
    "Enabled": false
  }
}
```

---

## 🔍 Debugging Cache

### View All Keys in Redis

```bash
redis-cli KEYS "MicroservicesApp:*"
```

### Get Cached Value

```bash
redis-cli GET "MicroservicesApp:tenant_config_123"
```

### Delete Specific Key

```bash
redis-cli DEL "MicroservicesApp:tenant_config_123"
```

### Flush All Cache (⚠️ Development Only)

```bash
redis-cli FLUSHALL
```

### Monitor Cache Activity

```bash
redis-cli MONITOR
```

---

## 📊 Cache Performance Tips

### ✅ DO

- **Use descriptive cache keys:** `user_{userId}`, `tenant_config_{tenantId}`
- **Set appropriate TTL:** 5-30 minutes for frequently changing data
- **Cache expensive operations:** Database queries, API calls, complex calculations
- **Handle cache misses gracefully:** Always have fallback logic
- **Use cache for read-heavy data:** User profiles, tenant configs, settings

### ❌ DON'T

- **Don't cache sensitive data:** Passwords, credit cards, API keys
- **Don't cache rapidly changing data:** Real-time stock prices, live scores
- **Don't use cache as primary storage:** Always persist to database
- **Don't set TTL too long:** Stale data can cause issues
- **Don't cache large objects:** Keep cached objects < 1MB

---

## 🧪 Testing Cache

### Unit Test Example

```csharp
[Fact]
public async Task GetAsync_ShouldReturnCachedValue()
{
    // Arrange
    var cache = new RedisCacheService(_distributedCache, _logger);
    var user = new User { Id = "123", Name = "John" };

    // Act
    await cache.SetAsync("user_123", user, TimeSpan.FromMinutes(5));
    var result = await cache.GetAsync<User>("user_123");

    // Assert
    Assert.NotNull(result);
    Assert.Equal("John", result.Name);
}
```

### Integration Test Example

```csharp
[Fact]
public async Task TenantConfig_ShouldBeCached()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("x-tenant-id", "tenant-123");

    // Act - First request (cache miss)
    var response1 = await client.GetAsync("/api/tenant/config");
    var time1 = DateTime.UtcNow;

    // Act - Second request (cache hit)
    var response2 = await client.GetAsync("/api/tenant/config");
    var time2 = DateTime.UtcNow;

    // Assert
    Assert.True(response1.IsSuccessStatusCode);
    Assert.True(response2.IsSuccessStatusCode);
    Assert.True((time2 - time1).TotalMilliseconds < 10); // Cache should be much faster
}
```

---

## 🚨 Troubleshooting

### Issue: "Connection to Redis failed"

**Solution:**

1. Check Redis is running: `redis-cli ping`
2. Verify connection string in appsettings.json
3. Check firewall rules
4. Temporarily disable Redis: `"Redis:Enabled": false`

### Issue: "Cache always misses"

**Solution:**

1. Check cache keys are consistent
2. Verify TTL is not too short
3. Monitor Redis: `redis-cli MONITOR`
4. Check logs for cache errors

### Issue: "Out of memory in Redis"

**Solution:**

1. Check Redis memory: `redis-cli INFO memory`
2. Reduce TTL on cached items
3. Remove old keys: `redis-cli FLUSHDB`
4. Increase Redis max memory
5. Enable eviction policy: `maxmemory-policy allkeys-lru`

### Issue: "SignalR not working across instances"

**Solution:**

1. Verify Redis backplane is configured
2. Check Redis connection in Notification Service
3. Verify `Redis:Enabled = true`
4. Test Redis connection: `redis-cli ping`
5. Check logs for SignalR backplane errors

---

## 📈 Monitoring Cache

### Key Metrics

```bash
# Cache hit rate
redis-cli INFO stats | grep keyspace_hits
redis-cli INFO stats | grep keyspace_misses

# Memory usage
redis-cli INFO memory | grep used_memory_human

# Connected clients
redis-cli INFO clients | grep connected_clients

# Operations per second
redis-cli INFO stats | grep instantaneous_ops_per_sec
```

### Expected Performance

| Metric            | Target | Action if Below               |
| ----------------- | ------ | ----------------------------- |
| Cache hit rate    | >90%   | Review cache keys and TTL     |
| Response time     | <5ms   | Check Redis server load       |
| Memory usage      | <80%   | Increase memory or reduce TTL |
| Connection errors | <1%    | Check network and firewall    |

---

## 🔄 Cache Invalidation Patterns

### Pattern 1: Time-Based (TTL)

```csharp
// Cache for 30 minutes
await _cache.SetAsync("data", value, TimeSpan.FromMinutes(30));
```

**Use when:** Data changes predictably or infrequently

### Pattern 2: Event-Based

```csharp
// When data changes
public async Task UpdateUserAsync(User user)
{
    await _database.UpdateAsync(user);

    // Invalidate cache
    await _cache.RemoveAsync($"user_{user.Id}");
}
```

**Use when:** Data changes need immediate reflection

### Pattern 3: Pattern-Based

```csharp
// Clear all tenant configs
await _cache.RemoveByPatternAsync("tenant_config_*");
```

**Use when:** Multiple related items need invalidation

---

## 🎯 Common Use Cases

### Tenant Configuration Caching

```csharp
var cacheKey = $"tenant_config_{tenantId}";
var tenant = await _cache.GetAsync<TenantInfo>(cacheKey);

if (tenant == null)
{
    tenant = await _tenantService.GetConfigAsync(tenantId);
    await _cache.SetAsync(cacheKey, tenant, TimeSpan.FromMinutes(30));
}
```

**Benefits:** 80% reduction in Tenant Service API calls

### User Session Caching

```csharp
var cacheKey = $"user_session_{sessionId}";
var session = await _cache.GetAsync<UserSession>(cacheKey);

if (session == null)
{
    session = await _database.GetSessionAsync(sessionId);
    await _cache.SetAsync(cacheKey, session, TimeSpan.FromMinutes(15));
}
```

**Benefits:** Faster authentication, reduced database load

### API Response Caching

```csharp
var cacheKey = $"api_response_{endpoint}_{parameters}";
var response = await _cache.GetAsync<ApiResponse>(cacheKey);

if (response == null)
{
    response = await _externalApi.CallAsync(endpoint, parameters);
    await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));
}
```

**Benefits:** Reduced external API costs, faster responses

---

## 📚 Additional Resources

- **Redis Documentation:** https://redis.io/documentation
- **StackExchange.Redis:** https://stackexchange.github.io/StackExchange.Redis/
- **Azure Cache for Redis:** https://docs.microsoft.com/azure/azure-cache-for-redis/
- **Migration Plan:** `Doc/REDIS_CACHE_MIGRATION_PLAN.md`
- **Migration Summary:** `Doc/REDIS_CACHE_MIGRATION_SUMMARY.md`

---

## 🆘 Need Help?

1. Check logs in `Logs/` directory
2. Review this guide
3. Check migration documentation
4. Test with Redis disabled (`"Redis:Enabled": false`)
5. Contact the development team

---

**Last Updated:** November 10, 2025  
**Version:** 1.0  
**Status:** ✅ Production Ready
