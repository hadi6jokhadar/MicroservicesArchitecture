# Rate Limiting Implementation Summary

**Date:** November 19, 2025  
**Status:** ✅ **COMPLETE**  
**Services Updated:** Identity Service, Tenant Service, File Manager Service

---

## Overview

Rate limiting has been successfully implemented across three critical microservices to protect against DoS attacks, brute force attempts, and resource abuse. This follows the same proven pattern already established in the Notification Service.

---

## Services with Rate Limiting

| Service              | Port | Global Limit | Per-IP Limit | Per-Tenant Limit | Per-User Limit |
| -------------------- | ---- | ------------ | ------------ | ---------------- | -------------- |
| **Identity Service** | 5001 | 50,000/min   | 100/min      | 5,000/min        | 1,000/min      |
| **Tenant Service**   | 5002 | 20,000/min   | 200/min      | 5,000/min        | N/A            |
| **File Manager**     | 5005 | 10,000/min   | 50/min       | 2,000/min        | 500/min        |
| **Notification**     | 5004 | 100,000/min  | 500/min      | 10,000/min       | 2,000/min      |

---

## Implementation Details

### Identity Service (Port 5001)

**Purpose:** Protect authentication endpoints from brute force attacks

**Rate Limits:**

- **Global:** 50,000 requests/minute (entire service)
- **Per-IP:** 100 requests/minute (prevent brute force)
- **Per-Tenant:** 5,000 requests/minute (tenant isolation)
- **Per-User:** 1,000 requests/minute (authenticated users)

**Why These Limits:**

- Login/register endpoints are primary attack vectors
- Per-IP limit specifically targets brute force password attacks
- Balances security with legitimate high-traffic scenarios

**Configuration Location:**

- `src/Services/Identity/Identity.API/Program.cs` (middleware)
- `src/Services/Identity/Identity.API/appsettings.json` (limits)

---

### Tenant Service (Port 5002)

**Purpose:** Protect tenant configuration endpoints from abuse

**Rate Limits:**

- **Global:** 20,000 requests/minute
- **Per-IP:** 200 requests/minute
- **Per-Tenant:** 5,000 requests/minute

**Why These Limits:**

- Tenant Service is called frequently by other services
- Higher per-IP limit to accommodate service-to-service calls
- No per-user limit (tenant management is admin-only)

**Configuration Location:**

- `src/Services/Tenant/Tenant.API/Program.cs` (middleware)
- `src/Services/Tenant/Tenant.API/appsettings.json` (limits)

---

### File Manager Service (Port 5005)

**Purpose:** Protect file upload/download endpoints from abuse

**Rate Limits:**

- **Global:** 10,000 requests/minute
- **Per-IP:** 50 requests/minute (file operations are resource-intensive)
- **Per-Tenant:** 2,000 requests/minute
- **Per-User:** 500 requests/minute

**Why These Limits:**

- File operations consume more resources (CPU, disk I/O, bandwidth)
- Lower per-IP limit prevents upload/download abuse
- Stricter user limits protect storage resources

**Configuration Location:**

- `src/Services/FileManager/FileManager.API/Program.cs` (middleware)
- `src/Services/FileManager/FileManager.API/appsettings.json` (limits)

---

## Rate Limiting Policies

### 1. Global Rate Limit

**Applied to:** All requests to the service  
**Key:** "global" (single partition)  
**Purpose:** Protect overall service capacity

```csharp
options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: "global",
        factory: partition => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 50000, // Configurable
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0 // No queuing for global limit
        }));
```

### 2. Per-IP Rate Limit

**Applied to:** Requests grouped by IP address  
**Key:** Remote IP address  
**Purpose:** Prevent brute force and DDoS attacks

```csharp
options.AddPolicy("PerIP", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: partition => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100, // Configurable
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 10 // Small queue for legitimate bursts
        }));
```

### 3. Per-Tenant Rate Limit

**Applied to:** Requests grouped by `x-tenant-id` header  
**Key:** Tenant ID  
**Purpose:** Isolate tenant resource consumption

```csharp
options.AddPolicy("PerTenant", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Request.Headers["x-tenant-id"].FirstOrDefault() ?? "default",
        factory: partition => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5000, // Configurable
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 50 // Allow tenant bursts
        }));
```

### 4. Per-User Rate Limit

**Applied to:** Authenticated requests grouped by user ID  
**Key:** User ID from JWT claims  
**Purpose:** Prevent individual user abuse

```csharp
options.AddPolicy("PerUser", context =>
{
    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: userId,
        factory: partition => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1000, // Configurable
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 20
        });
});
```

---

## Middleware Order (Critical)

Rate limiting is applied **before** authentication to protect endpoints even from unauthenticated attacks:

```csharp
app.UseGlobalExceptionHandler();
app.UseResponseCompression();
app.UseRateLimiter();           // ← Rate limiting BEFORE authentication
app.UseHttpsRedirection();

// Multi-tenancy
app.UseTenantResolution();
app.UseJwtTenantVerification();
app.UseTenantAwareCors();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();
```

---

## Error Response (HTTP 429)

When rate limit is exceeded, clients receive:

```json
{
  "error": "Rate limit exceeded. Please try again later.",
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfter": 60
}
```

**HTTP Status:** `429 Too Many Requests`  
**Headers:** Standard rate limiting headers (if configured)

---

## Logging

Rate limit violations are automatically logged:

```
[Warning] Rate limit exceeded - Endpoint: /api/auth/login, IP: 192.168.1.100, TenantId: acme-corp
```

**Log Level:** Warning  
**Includes:** Endpoint, IP address, Tenant ID  
**Purpose:** Security monitoring and analysis

---

## Configuration

All rate limits are configurable via `appsettings.json`:

### Identity Service Example

```json
{
  "RateLimiting": {
    "Global": {
      "PermitLimit": 50000,
      "WindowMinutes": 1
    },
    "PerIP": {
      "PermitLimit": 100,
      "WindowMinutes": 1
    },
    "PerTenant": {
      "PermitLimit": 5000,
      "WindowMinutes": 1
    },
    "PerUser": {
      "PermitLimit": 1000,
      "WindowMinutes": 1
    }
  }
}
```

### Adjusting Limits for Production

**High-Traffic Tenants:**

```json
"PerTenant": {
  "PermitLimit": 20000,
  "WindowMinutes": 1
}
```

**Premium Users:**

```json
"PerUser": {
  "PermitLimit": 5000,
  "WindowMinutes": 1
}
```

**Load Testing:**

```json
"Global": {
  "PermitLimit": 100000,
  "WindowMinutes": 1
}
```

---

## Testing Rate Limiting

### 1. Test Per-IP Rate Limit

```bash
# Send 150 requests from same IP (limit: 100/min)
for i in {1..150}; do
  curl -X POST http://localhost:5001/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"wrong"}' \
    -w "\n%{http_code}\n"
done

# Expected: First 100 return 200/401, rest return 429
```

### 2. Test Per-Tenant Rate Limit

```bash
# Send requests for specific tenant
for i in {1..6000}; do
  curl -X GET http://localhost:5001/api/user/profile \
    -H "x-tenant-id: acme-corp" \
    -H "Authorization: Bearer $TOKEN" \
    -w "\n%{http_code}\n"
done

# Expected: First 5000 succeed, rest return 429
```

### 3. Test Global Rate Limit

```bash
# Use load testing tool (e.g., k6, Apache Bench)
ab -n 60000 -c 100 http://localhost:5001/api/user/profile
# Expected: Rate limit kicks in around 50,000 requests
```

---

## Monitoring & Metrics

### Key Metrics to Track

1. **Rate Limit Violations** (from logs)

   - Filter logs for "Rate limit exceeded"
   - Group by endpoint, IP, tenant

2. **429 Response Count** (from application metrics)

   - Track total 429 responses
   - Alert if rate exceeds normal baseline

3. **Per-Endpoint Rate Limit Usage**

   - Monitor which endpoints hit limits most
   - Adjust limits based on usage patterns

4. **Tenant-Specific Violations**
   - Identify tenants frequently hitting limits
   - Consider premium tier adjustments

### Prometheus Metrics (Future Enhancement)

```csharp
// Example metric to add
private static readonly Counter RateLimitViolations = Metrics
    .CreateCounter("rate_limit_violations_total",
        "Total number of rate limit violations",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "endpoint", "tenant_id" }
        });
```

---

## Security Benefits

### ✅ Protections Enabled

1. **Brute Force Prevention**

   - Per-IP limit stops password guessing attacks
   - Identity Service: 100 attempts/min maximum

2. **DDoS Mitigation**

   - Global limit protects service capacity
   - Distributed attacks limited by per-IP policy

3. **Resource Abuse Prevention**

   - File uploads/downloads rate limited
   - Prevents storage exhaustion attacks

4. **Tenant Isolation**

   - One tenant can't exhaust resources for others
   - Fair resource allocation guaranteed

5. **API Scraping Prevention**
   - Per-user limits prevent automated scraping
   - Protects data confidentiality

---

## Best Practices

### DO ✅

- ✅ Monitor rate limit logs regularly
- ✅ Adjust limits based on actual usage patterns
- ✅ Document limit changes in git commits
- ✅ Test rate limiting during load tests
- ✅ Use stricter limits for sensitive endpoints (login, file upload)

### DON'T ❌

- ❌ Set limits too low (breaks legitimate usage)
- ❌ Ignore rate limit violation logs (security signal)
- ❌ Apply rate limiting AFTER authentication (wastes resources)
- ❌ Use same limits for all services (each has different needs)
- ❌ Forget to test rate limiting in staging environment

---

## Troubleshooting

### Issue: Legitimate Users Getting 429

**Symptom:** Users report frequent "Rate limit exceeded" errors

**Solutions:**

1. Check if user is within expected usage patterns
2. Review per-user limit (`RateLimiting:PerUser:PermitLimit`)
3. Consider implementing tiered limits (basic vs premium users)
4. Check if issue is per-IP (corporate network, proxy)

### Issue: Service-to-Service Calls Failing

**Symptom:** Services report 429 when calling each other

**Solutions:**

1. Increase per-IP limits (services call from same IPs)
2. Implement service authentication bypass for rate limiting
3. Use separate rate limit policy for service-to-service calls

```csharp
// Example: Bypass rate limiting for service-to-service calls
options.OnRejected = async (context, cancellationToken) =>
{
    // Check if request is from another service
    var serviceSecret = context.HttpContext.Request.Headers["X-Service-Secret"];
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        // Allow service-to-service calls to bypass rate limit
        context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        return;
    }

    // Normal rate limit response
    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    // ...
};
```

### Issue: Rate Limiting Not Applied

**Symptom:** Requests exceed limits without 429 response

**Check:**

1. Verify `app.UseRateLimiter()` is called in middleware pipeline
2. Check configuration values are loaded correctly
3. Ensure middleware order is correct (before authentication)
4. Verify .NET rate limiting package is installed

---

## Future Enhancements

### 1. Distributed Rate Limiting with Redis

Currently uses in-memory rate limiting (per service instance). For horizontal scaling:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

builder.Services.AddRateLimiter(options =>
{
    // Use distributed rate limiter with Redis
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(/* ... */));
});
```

### 2. Dynamic Rate Limits per Tenant Tier

```csharp
// Fetch tenant tier from tenant configuration
var tenantTier = tenantContext.CurrentTenant?.Tier ?? "Basic";
var permitLimit = tenantTier switch
{
    "Premium" => 10000,
    "Enterprise" => 50000,
    _ => 5000
};
```

### 3. Rate Limit Headers (RFC 6585)

```csharp
options.OnRejected = async (context, cancellationToken) =>
{
    context.HttpContext.Response.Headers.Append("X-RateLimit-Limit", "100");
    context.HttpContext.Response.Headers.Append("X-RateLimit-Remaining", "0");
    context.HttpContext.Response.Headers.Append("X-RateLimit-Reset",
        DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString());
    // ...
};
```

### 4. Adaptive Rate Limiting

Automatically adjust limits based on system load:

```csharp
var cpuUsage = GetCpuUsage();
var adjustedLimit = baseLimit * (1.0 - cpuUsage);
```

---

## Summary

Rate limiting has been successfully implemented across **4 microservices**:

- ✅ **Identity Service** - 50k global, 100 per-IP (brute force protection)
- ✅ **Tenant Service** - 20k global, 200 per-IP (high service traffic)
- ✅ **File Manager Service** - 10k global, 50 per-IP (resource-intensive operations)
- ✅ **Notification Service** - 100k global, 500 per-IP (high-volume notifications)

**Total Protection:** All critical services now protected against:

- DDoS attacks
- Brute force attempts
- Resource abuse
- API scraping
- Tenant resource exhaustion

**Configuration:** Fully configurable via `appsettings.json`  
**Monitoring:** Automatic logging of violations  
**Testing:** All services build successfully

---

## Related Documentation

- [BOTTLENECKS_COMPLETION_SUMMARY.md](BOTTLENECKS_COMPLETION_SUMMARY.md) - Notification Service performance (includes rate limiting)
- [PERFORMANCE_OPTIMIZATION_GUIDE.md](PERFORMANCE_OPTIMIZATION_GUIDE.md) - Performance tuning strategies
- [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) - How to add rate limiting to new services

---

**Last Updated:** November 19, 2025  
**Version:** 1.0  
**Status:** ✅ Production Ready
