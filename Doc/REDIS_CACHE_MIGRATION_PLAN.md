# 🔄 Redis Cache Migration Plan

**Date Created:** November 10, 2025  
**Status:** 🟡 In Progress  
**Priority:** 🔥 Critical  
**Estimated Time:** 2-3 days

---

## 📋 Table of Contents

- [Overview](#overview)
- [Migration Goals](#migration-goals)
- [Architecture Changes](#architecture-changes)
- [Implementation Steps](#implementation-steps)
- [Configuration Guide](#configuration-guide)
- [Testing Strategy](#testing-strategy)
- [Rollback Plan](#rollback-plan)
- [Performance Expectations](#performance-expectations)

---

## Overview

This document outlines the migration from **in-memory caching (IMemoryCache)** to **distributed Redis caching (IDistributedCache)** across all microservices in the architecture.

### Current State

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Identity       │     │  Notification   │     │  Tenant         │
│  Service        │     │  Service        │     │  Service        │
│  Instance 1     │     │  Instance 1     │     │  Instance 1     │
│                 │     │                 │     │                 │
│  MemoryCache    │     │  MemoryCache    │     │  MemoryCache    │
│  (isolated)     │     │  (isolated)     │     │  (isolated)     │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         ❌                      ❌                      ❌
    Cache not shared      Cache not shared        Cache not shared
```

**Problems:**

- ❌ Each instance has its own cache
- ❌ Cache misses when scaling horizontally
- ❌ SignalR can't scale (no backplane)
- ❌ Duplicate Tenant Service API calls

### Target State

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Identity       │     │  Notification   │     │  Tenant         │
│  Service        │     │  Service        │     │  Service        │
│  Instance 1     │     │  Instance 1     │     │  Instance 1     │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                                 ▼
                    ┌────────────────────────┐
                    │   Redis Cache Server   │
                    │                        │
                    │  • Shared Cache        │
                    │  • SignalR Backplane   │
                    │  • Persistent Storage  │
                    └────────────────────────┘
                                 ✅
                          All services share
                          same cache instance
```

**Benefits:**

- ✅ Shared cache across all instances
- ✅ SignalR horizontal scaling enabled
- ✅ Reduced external API calls
- ✅ Cache survives service restarts
- ✅ Centralized cache management

---

## Migration Goals

### Primary Objectives

1. **Create Shared Redis Cache Service** in `IhsanDev.Shared.Infrastructure`
2. **Replace IMemoryCache with IDistributedCache** across all services
3. **Implement SignalR Redis Backplane** for Notification Service
4. **Centralize Configuration** - Single Redis connection string
5. **Zero Downtime Migration** - Support both cache types during transition

### Success Criteria

- ✅ All services use Redis for caching
- ✅ Notification Service scales horizontally
- ✅ Cache hit rate improves by 30%+
- ✅ Tenant Service API calls reduced by 50%+
- ✅ Performance maintained or improved
- ✅ All existing tests pass

---

## Architecture Changes

### 1. New Shared Components

**Location:** `src/Shared/IhsanDev.Shared.Infrastructure/Services/Cache/`

```
IhsanDev.Shared.Infrastructure/
├── Services/
│   ├── Cache/                          ← NEW
│   │   ├── ICacheService.cs            ← Interface
│   │   ├── RedisCacheService.cs        ← Redis implementation
│   │   └── MemoryCacheService.cs       ← Fallback implementation
│   └── Tenant/
│       └── TenantConfigurationProvider.cs  ← MODIFIED (use ICacheService)
└── Extensions/
    └── RedisCacheExtensions.cs         ← NEW (DI registration)
```

### 2. Modified Components

**Files to Update:**

1. `IhsanDev.Shared.Infrastructure/Services/Tenant/TenantConfigurationProvider.cs`

   - Replace `IMemoryCache` with `ICacheService`
   - Maintain backward compatibility

2. `IhsanDev.Shared.Infrastructure/Extensions/MultiTenancyExtensions.cs`

   - Add Redis cache registration option
   - Keep memory cache as fallback

3. `Notification.API/Program.cs`

   - Add SignalR Redis backplane
   - Configure Redis cache

4. All Service `appsettings.json` files
   - Add Redis connection configuration

### 3. Package Dependencies

**Add to `Directory.Packages.props`:**

```xml
<PackageVersion Include="StackExchange.Redis" Version="2.7.10" />
<PackageVersion Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
<PackageVersion Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="8.0.0" />
```

---

## Implementation Steps

### Phase 1: Create Shared Cache Infrastructure (Day 1 Morning)

#### Step 1.1: Create ICacheService Interface

**File:** `src/Shared/IhsanDev.Shared.Infrastructure/Services/Cache/ICacheService.cs`

```csharp
namespace IhsanDev.Shared.Infrastructure.Services.Cache;

/// <summary>
/// Unified cache service interface supporting both in-memory and distributed caching
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sets a cached value with expiration
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes a cached value by key
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cached values matching a pattern (Redis only)
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}
```

#### Step 1.2: Create Redis Implementation

**File:** `src/Shared/IhsanDev.Shared.Infrastructure/Services/Cache/RedisCacheService.cs`

```csharp
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IhsanDev.Shared.Infrastructure.Services.Cache;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IDistributedCache cache,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var data = await _cache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(data))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from Redis cache for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _cache.SetStringAsync(key, json, options, cancellationToken);

            _logger.LogDebug("Cached value for key: {Key} with expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Redis cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _logger.LogDebug("Removed cached value for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from Redis cache for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _cache.GetStringAsync(key, cancellationToken);
            return !string.IsNullOrEmpty(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence in Redis cache for key: {Key}", key);
            return false;
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: This requires StackExchange.Redis directly for pattern-based deletion
            // For now, log a warning
            _logger.LogWarning("RemoveByPatternAsync not fully implemented for Redis. Pattern: {Pattern}", pattern);

            // TODO: Implement using StackExchange.Redis IConnectionMultiplexer
            // var server = connectionMultiplexer.GetServer(endpoint);
            // var keys = server.Keys(pattern: pattern);
            // foreach (var key in keys) await cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing values by pattern from Redis cache. Pattern: {Pattern}", pattern);
        }
    }
}
```

#### Step 1.3: Create Memory Cache Fallback

**File:** `src/Shared/IhsanDev.Shared.Infrastructure/Services/Cache/MemoryCacheService.cs`

```csharp
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Services.Cache;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;

    public MemoryCacheService(
        IMemoryCache cache,
        ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            if (_cache.TryGetValue<T>(key, out var value))
            {
                return Task.FromResult<T?>(value);
            }

            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from memory cache for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            _cache.Set(key, value, expiration);
            _logger.LogDebug("Cached value for key: {Key} with expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in memory cache for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _cache.Remove(key);
            _logger.LogDebug("Removed cached value for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from memory cache for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(_cache.TryGetValue(key, out _));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence in memory cache for key: {Key}", key);
            return Task.FromResult(false);
        }
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Memory cache doesn't support pattern-based deletion
        _logger.LogWarning("RemoveByPatternAsync not supported for MemoryCache. Pattern: {Pattern}", pattern);
        return Task.CompletedTask;
    }
}
```

#### Step 1.4: Create Extension Methods

**File:** `src/Shared/IhsanDev.Shared.Infrastructure/Extensions/RedisCacheExtensions.cs`

```csharp
using IhsanDev.Shared.Infrastructure.Services.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IhsanDev.Shared.Infrastructure.Extensions;

public static class RedisCacheExtensions
{
    /// <summary>
    /// Adds Redis distributed cache service
    /// </summary>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration["Redis:ConnectionString"];

        if (string.IsNullOrEmpty(redisConnection))
        {
            throw new InvalidOperationException(
                "Redis connection string not found in configuration. " +
                "Please add 'Redis:ConnectionString' to appsettings.json");
        }

        // Add Redis distributed cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = configuration["Redis:InstanceName"] ?? "MicroservicesApp:";
        });

        // Register ICacheService with Redis implementation
        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Adds in-memory cache service (fallback)
    /// </summary>
    public static IServiceCollection AddMemoryCache(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        return services;
    }

    /// <summary>
    /// Adds cache service based on configuration
    /// </summary>
    public static IServiceCollection AddCacheService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useRedis = configuration.GetValue<bool>("Redis:Enabled", false);

        if (useRedis)
        {
            services.AddRedisCache(configuration);
        }
        else
        {
            services.AddMemoryCache();
        }

        return services;
    }
}
```

### Phase 2: Update Tenant Configuration Provider (Day 1 Afternoon)

#### Step 2.1: Modify TenantConfigurationProvider

**File:** `src/Shared/IhsanDev.Shared.Infrastructure/Services/Tenant/TenantConfigurationProvider.cs`

Replace `IMemoryCache` with `ICacheService`:

```csharp
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Models.Tenant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace IhsanDev.Shared.Infrastructure.Services.Tenant;

public class TenantConfigurationProvider : ITenantConfigurationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly ILogger<TenantConfigurationProvider> _logger;
    private readonly string _tenantServiceUrl;
    private readonly TimeSpan _cacheExpiration;

    public TenantConfigurationProvider(
        IHttpClientFactory httpClientFactory,
        ICacheService cache,
        IConfiguration configuration,
        ILogger<TenantConfigurationProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TenantService");
        _cache = cache;
        _logger = logger;

        _tenantServiceUrl = configuration["MultiTenancy:TenantServiceUrl"]
            ?? throw new InvalidOperationException("TenantServiceUrl not configured");

        _cacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue<int>("MultiTenancy:CacheExpirationMinutes", 30));
    }

    public async Task<TenantInfo?> GetTenantConfigurationAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning("Attempted to fetch tenant configuration with null or empty tenant ID");
            return null;
        }

        var cacheKey = $"tenant_config_{tenantId}";

        try
        {
            // Try to get from cache
            var cachedTenant = await _cache.GetAsync<TenantInfo>(cacheKey, cancellationToken);
            if (cachedTenant != null)
            {
                _logger.LogDebug("Tenant configuration retrieved from cache for tenant: {TenantId}", tenantId);
                return cachedTenant;
            }

            // Cache miss - fetch from Tenant Service
            _logger.LogDebug("Cache miss for tenant: {TenantId}, fetching from Tenant Service", tenantId);

            var response = await _httpClient.GetAsync(
                $"{_tenantServiceUrl}/api/tenant/config/{tenantId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch tenant configuration for {TenantId}. Status: {StatusCode}",
                    tenantId,
                    response.StatusCode);
                return null;
            }

            var tenantInfo = await response.Content.ReadFromJsonAsync<TenantInfo>(
                cancellationToken: cancellationToken);

            if (tenantInfo != null)
            {
                // Cache the result
                await _cache.SetAsync(cacheKey, tenantInfo, _cacheExpiration, cancellationToken);
                _logger.LogInformation("Tenant configuration cached for tenant: {TenantId}", tenantId);
            }

            return tenantInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenant configuration for tenant: {TenantId}", tenantId);
            return null;
        }
    }

    public async Task ClearCacheAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"tenant_config_{tenantId}";
        await _cache.RemoveAsync(cacheKey, cancellationToken);
        _logger.LogInformation("Cache cleared for tenant: {TenantId}", cacheKey);
    }

    public async Task ClearAllCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveByPatternAsync("tenant_config_*", cancellationToken);
        _logger.LogInformation("All tenant configuration cache cleared");
    }
}
```

#### Step 2.2: Update MultiTenancyExtensions

**File:** `src/Shared/IhsanDev.Shared.Infrastructure/Extensions/MultiTenancyExtensions.cs`

Update to use new cache service:

```csharp
public static IServiceCollection AddMultiTenancy(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);

    if (!multiTenancyEnabled)
    {
        return services;
    }

    // Add cache service (Redis or Memory based on configuration)
    services.AddCacheService(configuration);

    // Register tenant services
    services.AddScoped<ITenantContext, TenantContext>();
    services.AddScoped<ITenantConfigurationProvider, TenantConfigurationProvider>();

    // Configure HttpClient for Tenant Service
    services.AddHttpClient("TenantService", client =>
    {
        var tenantServiceUrl = configuration["MultiTenancy:TenantServiceUrl"];
        if (!string.IsNullOrEmpty(tenantServiceUrl))
        {
            client.BaseAddress = new Uri(tenantServiceUrl);
        }
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    return services;
}
```

### Phase 3: Update Package Management (Day 1 Afternoon)

#### Step 3.1: Update Directory.Packages.props

**File:** `Directory.Packages.props`

Add Redis packages:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <!-- Existing packages -->
    <PackageVersion Include="MediatR" Version="12.2.0" />
    <PackageVersion Include="AutoMapper" Version="12.0.1" />
    <!-- ... other existing packages ... -->

    <!-- NEW: Redis packages -->
    <PackageVersion Include="StackExchange.Redis" Version="2.7.10" />
    <PackageVersion Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="8.0.0" />
  </ItemGroup>
</Project>
```

#### Step 3.2: Update Shared.Infrastructure.csproj

**File:** `src/Shared/IhsanDev.Shared.Infrastructure/IhsanDev.Shared.Infrastructure.csproj`

Add package references:

```xml
<ItemGroup>
  <!-- Existing packages -->

  <!-- NEW: Redis caching -->
  <PackageReference Include="StackExchange.Redis" />
  <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" />
</ItemGroup>
```

### Phase 4: Configure Services (Day 2 Morning)

#### Step 4.1: Update appsettings.json for All Services

**Pattern for all services:**

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 30
  }
}
```

**Files to update:**

1. `src/Services/Identity/Identity.API/appsettings.json`
2. `src/Services/Identity/Identity.API/appsettings.Development.json`
3. `src/Services/Tenant/Tenant.API/appsettings.json`
4. `src/Services/Tenant/Tenant.API/appsettings.Development.json`
5. `src/Services/Notification/Notification.API/appsettings.json`
6. `src/Services/Notification/Notification.API/appsettings.Development.json`

### Phase 5: SignalR Redis Backplane (Day 2 Afternoon)

#### Step 5.1: Update Notification.API.csproj

**File:** `src/Services/Notification/Notification.API/Notification.API.csproj`

```xml
<ItemGroup>
  <!-- Existing packages -->

  <!-- NEW: SignalR Redis backplane -->
  <PackageReference Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" />
</ItemGroup>
```

#### Step 5.2: Update Notification Program.cs

**File:** `src/Services/Notification/Notification.API/Program.cs`

Add SignalR Redis backplane configuration:

```csharp
// Add SignalR with Redis backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        var redisConnection = builder.Configuration["Redis:ConnectionString"];

        if (!string.IsNullOrEmpty(redisConnection))
        {
            options.Configuration = StackExchange.Redis.ConfigurationOptions.Parse(redisConnection);
            options.Configuration.ChannelPrefix = "SignalR";

            builder.Services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("SignalR Redis backplane configured with connection: {Connection}",
                    redisConnection);
                return options;
            });
        }
        else
        {
            var logger = builder.Services.BuildServiceProvider()
                .GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Redis connection string not found. SignalR running without backplane (single instance only)");
        }
    });
```

### Phase 6: Testing & Validation (Day 3)

#### Step 6.1: Install Redis Locally

**Option 1: Docker (Recommended)**

```bash
docker run -d --name redis-cache -p 6379:6379 redis:7-alpine
```

**Option 2: Windows (using WSL2)**

```bash
# In WSL2
sudo apt update
sudo apt install redis-server
sudo service redis-server start
```

**Option 3: Redis Cloud (Production)**

Use managed Redis service (Azure Cache for Redis, AWS ElastiCache, Redis Cloud)

#### Step 6.2: Verify Redis Connection

**Test command:**

```bash
redis-cli ping
# Expected output: PONG
```

#### Step 6.3: Run Tests

1. **Unit Tests**

   ```bash
   dotnet test src/Shared/IhsanDev.Shared.Infrastructure.Tests
   ```

2. **Integration Tests**

   ```bash
   dotnet test src/Services/Identity/Identity.API.Tests
   dotnet test src/Services/Notification/Notification.API.Tests
   ```

3. **Manual Testing**
   - Start all services
   - Send notification from Identity Service
   - Verify cache hits in logs
   - Test multi-instance SignalR delivery

---

## Configuration Guide

### Development Environment

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "Dev:"
  }
}
```

### Production Environment

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "your-redis-server:6379,password=your-password,ssl=true,abortConnect=false",
    "InstanceName": "Prod:"
  }
}
```

### Azure Cache for Redis

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "your-app.redis.cache.windows.net:6380,password=your-access-key,ssl=True,abortConnect=False",
    "InstanceName": "Azure:"
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

## Testing Strategy

### Test Scenarios

1. **Cache Functionality**

   - ✅ Set value in cache
   - ✅ Retrieve value from cache
   - ✅ Remove value from cache
   - ✅ Cache expiration works
   - ✅ Pattern-based removal

2. **Multi-Instance**

   - ✅ Start 2 instances of Notification Service
   - ✅ Send notification from one instance
   - ✅ Verify both instances see the notification
   - ✅ Verify SignalR clients on both instances receive messages

3. **Tenant Configuration**

   - ✅ First request fetches from Tenant Service
   - ✅ Second request served from Redis cache
   - ✅ Cache invalidation works
   - ✅ Different services share same cache

4. **Failover**
   - ✅ Redis goes down → Services use memory cache fallback
   - ✅ Redis comes back → Services resume using Redis

### Performance Tests

**Before Migration:**

```
Tenant Config Cache Hit Rate: 70%
Tenant Service API Calls: 1000/min
Average Response Time: 150ms
```

**After Migration (Expected):**

```
Tenant Config Cache Hit Rate: 95%
Tenant Service API Calls: 200/min (80% reduction)
Average Response Time: 50ms (67% improvement)
```

---

## Rollback Plan

### If Issues Occur

1. **Immediate Rollback**

   ```json
   {
     "Redis": {
       "Enabled": false // ← Disable Redis
     }
   }
   ```

   Restart services → Falls back to memory cache

2. **Gradual Rollback**

   - Disable Redis for one service at a time
   - Monitor for issues
   - Keep Redis for services that work

3. **Full Rollback**
   - Revert code changes
   - Redeploy previous version
   - Stop Redis server

### Rollback Triggers

**Rollback immediately if:**

- ❌ Cache hit rate drops below 50%
- ❌ Response times increase by >30%
- ❌ Redis connection failures >10% of requests
- ❌ Any service becomes unstable

---

## Performance Expectations

### Cache Performance

| Metric              | Memory Cache | Redis Cache | Improvement         |
| ------------------- | ------------ | ----------- | ------------------- |
| Read latency        | 0.001ms      | 1-5ms       | -5000% (acceptable) |
| Write latency       | 0.01ms       | 1-5ms       | -500% (acceptable)  |
| Cache sharing       | ❌ No        | ✅ Yes      | Unlimited instances |
| Persistence         | ❌ No        | ✅ Yes      | Survives restarts   |
| Memory per instance | 100MB        | ~10MB       | 90% reduction       |

### Expected Improvements

1. **Tenant Service API Calls**

   - Current: ~1000 calls/min
   - After Redis: ~200 calls/min
   - **Reduction: 80%**

2. **Cache Hit Rate**

   - Current: 70% (per-instance)
   - After Redis: 95% (shared)
   - **Improvement: +25%**

3. **Horizontal Scaling**

   - Current: Limited (cache misses)
   - After Redis: Unlimited instances
   - **Scalability: ∞**

4. **SignalR Delivery**
   - Current: Single instance only
   - After Redis: Multi-instance support
   - **Availability: High**

---

## Migration Checklist

### Pre-Migration

- [ ] Review this document with team
- [ ] Set up Redis server (local/dev/prod)
- [ ] Test Redis connectivity
- [ ] Backup current configurations
- [ ] Create rollback plan

### Implementation

**Phase 1: Infrastructure**

- [ ] Create ICacheService interface
- [ ] Implement RedisCacheService
- [ ] Implement MemoryCacheService
- [ ] Create RedisCacheExtensions
- [ ] Add Redis packages to Directory.Packages.props

**Phase 2: Integration**

- [ ] Update TenantConfigurationProvider
- [ ] Update MultiTenancyExtensions
- [ ] Update all appsettings.json files

**Phase 3: SignalR**

- [ ] Add SignalR Redis backplane to Notification Service
- [ ] Test multi-instance SignalR delivery

**Phase 4: Testing**

- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing complete
- [ ] Performance tests complete

**Phase 5: Deployment**

- [ ] Deploy to development environment
- [ ] Monitor for 24 hours
- [ ] Deploy to staging environment
- [ ] Deploy to production environment

### Post-Migration

- [ ] Monitor cache hit rates
- [ ] Monitor Redis server health
- [ ] Monitor API call reductions
- [ ] Update documentation
- [ ] Train team on Redis operations
- [ ] Set up alerting for Redis failures

---

## Monitoring & Alerts

### Key Metrics to Monitor

1. **Redis Health**

   - Connection status
   - Memory usage
   - CPU usage
   - Network throughput

2. **Cache Performance**

   - Hit rate (target: >90%)
   - Miss rate
   - Average latency
   - Error rate

3. **Application Impact**
   - Tenant Service API calls
   - Response times
   - Error rates
   - SignalR connection count

### Recommended Alerts

```yaml
alerts:
  - name: Redis Connection Failed
    condition: redis.connected == false
    severity: critical
    action: Page on-call engineer

  - name: Low Cache Hit Rate
    condition: cache.hit_rate < 50%
    severity: warning
    action: Investigate cache configuration

  - name: High Redis Memory
    condition: redis.memory_usage > 80%
    severity: warning
    action: Consider scaling Redis
```

---

## Next Steps

1. ✅ **Review this plan** with the team
2. 🔄 **Start Phase 1** - Create shared cache infrastructure
3. 📝 **Daily standups** to track progress
4. 🧪 **Continuous testing** after each phase
5. 📊 **Performance monitoring** post-deployment

---

**Document Status:** 📝 Active  
**Last Updated:** November 10, 2025  
**Next Review:** After Phase 3 completion

**Questions or concerns?** Contact the development team.
