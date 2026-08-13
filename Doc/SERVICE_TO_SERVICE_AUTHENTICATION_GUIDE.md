# Service-to-Service Communication Guide

## Overview

This guide explains how services in the microservices architecture communicate with each other securely without requiring user JWT tokens. We use a **shared secret authentication** mechanism that allows internal services to call each other's APIs.

---

## 🔐 Authentication Mechanism

### How It Works

1. Each service has a **shared secret key** configured in `appsettings.json`
2. When making service-to-service calls, the calling service includes:
   - `X-Service-Secret` header with the shared secret
   - `X-Service-Name` header with the calling service name
3. The `ServiceAuthenticationMiddleware` validates the secret
4. If valid, the request is authenticated with a "Service" role
5. Endpoints can authorize both "User" and "Service" roles

---

## ⚙️ Configuration

### 1. Shared Secret Configuration

Add to `appsettings.json` in **ALL services** (Identity, Notification, Tenant):

```json
{
  "ServiceCommunication": {
    "Enabled": true,
    "ServiceName": "IdentityService",
    "SharedSecret": "CHANGE_ME_JWT_SECRET-service-secret-key",
    "AllowedServices": [
      "IdentityService",
      "NotificationService",
      "TenantService"
    ]
  }
}
```

**Important:**

- The `SharedSecret` **MUST be the same** across all services
- `ServiceName` identifies the calling service (used in logs and validation)
- Use a strong secret key (64+ characters recommended)
- In production, store this in Azure Key Vault or similar secret manager
- `AllowedServices` is optional whitelist of service names

### 2. Service URLs Configuration

In services that need to call other services (e.g., Identity calling Notification):

```json
{
  "Services": {
    "NotificationService": {
      "BaseUrl": "https://localhost:5104",
      "Timeout": 30
    }
  }
}
```

---

## 🛠️ Implementation Details

### ServiceAuthenticationMiddleware

Located in: `Shared.Infrastructure/Middleware/ServiceAuthenticationMiddleware.cs`

**Features:**

- ✅ Validates `X-Service-Secret` header
- ✅ Creates service identity with "Service" role
- ✅ Optional service name whitelist validation
- ✅ Adds claims: `Role=Service`, `IsInternalService=true`, `ServiceName=<name>`
- ✅ Comprehensive logging

**Pipeline Order (IMPORTANT):**

```csharp
app.UseServiceAuthentication();  // MUST be BEFORE UseAuthentication()
app.UseAuthentication();
app.UseAuthorization();
```

### ⚠️ The `AllowedServices` whitelist is the #1 source of silent failures

Every multi-tenant service (Identity, FileManager, Category, Notification, Nasheed — anything calling `AddMultiTenancy()`) automatically calls Tenant Service's `/config/{tenantId}` on every cache miss via the shared `TenantServiceClient`. **This means Tenant Service's `AllowedServices` list must include every single multi-tenant service's `ServiceName` — not just the two or three you're actively thinking about.**

This was missed for both `CategoryService` and `NasheedService` (July 2026) — both were correctly sending `X-Service-Secret` and the right `X-Service-Name`, but Tenant's whitelist only listed `IdentityService`, `NotificationService`, `FileManagerService`. `ServiceAuthenticationMiddleware` doesn't reject the request outright when a name isn't whitelisted — it silently skips setting the `Service`/`SuperAdmin` claims and lets the request continue unauthenticated, so the *actual* failure surfaces later as a plain 401 from the endpoint's own role check, with a `[Warning] Service '<name>' is not in the allowed services list` line easy to miss in the logs. Worse, this can hide for a long time: the 30-minute tenant-config cache means the bug only surfaces on a cache miss (cold start, cache flush, first request for a tenant) — it can look like everything works fine for hours.

**When adding a new multi-tenant service, add its `ServiceName` to Tenant Service's `AllowedServices` in both `appsettings.json` and `appsettings.Development.json` — this is easy to forget because nothing fails until the cache goes cold.**

### ⚠️ A placeholder or too-short `SharedSecret` now crashes the service at startup — it is no longer a silent 401

`ServiceAuthenticationMiddleware`'s constructor calls `JwtAuthenticationExtensions.ValidateSecretStrength(secret, "ServiceCommunication:SharedSecret")` before the app finishes starting. This throws `InvalidOperationException` — a hard startup crash, not a runtime 401 — if the configured secret is missing, is one of the known committed placeholders (`CHANGE_ME_JWT_SECRET`, `CHANGE_ME_SHARED_SECRET`, etc.), or is under 32 bytes. This means a service whose base `appsettings.json` only has the placeholder `SharedSecret: "CHANGE_ME_..."`, with no real value supplied via `appsettings.Development.json` (or environment variables), now **fails loudly at startup** instead of silently accepting service calls with a weak secret.

**This is a distinct failure mode from the `AllowedServices` gap above** — a startup crash is easy to spot (the service never comes up), whereas the whitelist gap is a silent per-request 401 that can hide for hours behind the 30-minute cache. What `ValidateSecretStrength` does **not** catch: two services each configured with a different *real*, strong secret (e.g. a typo when copying the value into one service's `appsettings.Development.json`). That case passes the strength check on both sides but still fails service-to-service auth at runtime — `ServiceAuthenticationMiddleware` logs `Invalid service secret from IP: ..., Path: ...` and, exactly like the whitelist gap, silently skips the `Service`/`SuperAdmin` claims rather than rejecting the request outright, surfacing later as a plain 401 from the endpoint's own role check.

Found for `NasheedService` (July 2026, before `ValidateSecretStrength` existed): its `AllowedServices` gap in Tenant Service was fixed first, but tenant resolution still failed — `Nasheed.API/appsettings.Development.json` had no `ServiceCommunication` section at all, so the placeholder secret from the base `appsettings.json` was genuinely in effect at runtime. Fixed by adding the real shared secret to `appsettings.Development.json`; a `ValidateSecretStrength`-style check was added afterward specifically to turn this class of gap into a startup failure. **When scaffolding a new service, verify both halves — the whitelist entry on the receiving side AND a real, matching secret override on the calling side.** A missing/placeholder secret now fails fast at startup; a real-but-mismatched secret between two services still fails silently at request time, same as the whitelist gap.

### Automatic Service Authentication for Tenant Service Client

When using multi-tenancy, the `TenantServiceClient` is **automatically configured** with service authentication headers by the `AddMultiTenancy()` extension method.

**Location:** `Shared.Infrastructure/Extensions/MultiTenancyExtensions.cs`

**What it does:**

- Reads `ServiceCommunication:SharedSecret` from configuration
- Reads `ServiceCommunication:ServiceName` from configuration (or falls back to `ApplicationName`)
- Automatically adds `X-Service-Secret` and `X-Service-Name` headers to all Tenant Service API calls
- Bypasses SSL certificate validation in local Development (`ASPNETCORE_ENVIRONMENT == "Development"`) so self-signed dev certs work
- Wraps every call in a resilience pipeline (`.AddStandardResilienceHandler()`) tuned to fail fast, since this client is invoked by `TenantConfigurationProvider` on **every** tenant-scoped request across every multi-tenant service whenever the 30-minute Redis cache misses
- No manual HttpClient configuration needed for tenant config fetching

**Code:**

```csharp
services.AddHttpClient("TenantServiceClient", client =>
{
    client.BaseAddress = new Uri(tenantServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    // Add service authentication headers automatically
    var serviceSecret = configuration["ServiceCommunication:SharedSecret"];
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);

        var serviceName = configuration["ServiceCommunication:ServiceName"]
            ?? configuration["ApplicationName"]
            ?? "UnknownService";
        client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
    }
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    // In development, bypass SSL certificate validation for self-signed certificates
    if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return handler;
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.Delay = TimeSpan.FromMilliseconds(100);
    options.Retry.BackoffType = DelayBackoffType.Exponential;

    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(4);
});
```

**This means:** When you enable multi-tenancy with `AddMultiTenancy()`, the Tenant Service client is automatically ready for service-to-service authentication — and tuned to fail fast (≈4s worst case) rather than hold a caller's request thread open on a slow/unhealthy Tenant Service.

---

## 📡 Service Configurations

### Identity Service

**Calls:** Notification Service

**Configuration:**

```json
{
  "ServiceCommunication": {
    "Enabled": true,
    "ServiceName": "IdentityService",
    "SharedSecret": "CHANGE_ME_JWT_SECRET-service-secret-key",
    "AllowedServices": ["NotificationService", "TenantService"]
  },
  "Services": {
    "NotificationService": {
      "BaseUrl": "https://localhost:5104",
      "Timeout": 30
    }
  }
}
```

**HttpClient Setup (Program.cs) — actual current pattern:**

Identity.API no longer configures `HttpClient` inline. It calls the shared extension methods from `SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md` instead, which already wire up the base URL, timeout, service-auth headers, dev SSL bypass, correlation-ID forwarding, and resilience pipeline:

```csharp
// Register Notification service client for service-to-service communication
builder.Services.AddNotificationServiceClient(
    builder.Configuration,
    "IdentityService",
    builder.Environment.IsDevelopment());

// Register FileManager service client for service-to-service communication
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "IdentityService",
    builder.Environment.IsDevelopment());
```

See `SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md` for the full extension-method reference (resilience settings, correlation-ID propagation, configuration priority). The verbose inline `AddHttpClient(...)` pattern below is kept only as a historical "before" reference — do not copy it for new code.

```csharp
// Historical / deprecated — do not use for new code, see AddNotificationServiceClient above
builder.Services.AddHttpClient("NotificationService", client =>
{
    var baseUrl = builder.Configuration["Services:NotificationService:BaseUrl"]
        ?? "https://localhost:5104";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    var timeout = builder.Configuration.GetValue<int>("Services:NotificationService:Timeout", 30);
    client.Timeout = TimeSpan.FromSeconds(timeout);

    // Add service authentication headers
    var serviceSecret = builder.Configuration["ServiceCommunication:SharedSecret"];
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
        client.DefaultRequestHeaders.Add("X-Service-Name", "IdentityService");
    }
});
```

### Notification Service

**Called by:** Identity Service, Tenant Service, Nasheed Service

**Configuration:**

```json
{
  "ServiceCommunication": {
    "Enabled": true,
    "ServiceName": "NotificationService",
    "SharedSecret": "CHANGE_ME_JWT_SECRET-service-secret-key",
    "AllowedServices": ["IdentityService", "TenantService", "NasheedService"]
  }
}
```

Nasheed added August 2026 — `NasheedIngestionWorker` broadcasts ingestion job-status changes via `SendTenantBroadcastAsync` so the admin frontend can update live (see `Doc/NOTIFICATION_SERVICE_README.md` "Known Service-to-Service Consumers" and `src/Apps/Nasheed/Doc/INGESTION_PIPELINE.md`).

**Endpoint Authorization:**

```csharp
var notificationGroup = app.MapGroup("/api/notifications")
    .WithTags("Notifications")
    .RequireAuthorization(policy => policy.RequireRole("User", "Service"))  // ← Allow Service role
    .WithOpenApi();
```

### Tenant Service

**Called by:** Every multi-tenant service via `AddMultiTenancy()`'s `TenantServiceClient` — Identity, Notification, FileManager, Category, Nasheed, Backup, PolySnap

**Configuration:**

```json
{
  "ServiceCommunication": {
    "Enabled": true,
    "ServiceName": "TenantService",
    "SharedSecret": "CHANGE_ME_SHARED_SECRET",
    "AllowedServices": [
      "IdentityService",
      "NotificationService",
      "FileManagerService",
      "CategoryService",
      "NasheedService",
      "BackupService",
      "PolySnapService"
    ]
  }
}
```

**This is the actual, current `AllowedServices` list from `Tenant.API/appsettings.json`.** This is exactly the list the "whitelist gap" warning above is about — every one of these seven entries corresponds to a real multi-tenant service that calls `/config/{tenantId}` on every cache miss; missing even one silently breaks that service the next time its cache goes cold. When adding an eighth service, add it here (and to `appsettings.Development.json`) immediately, not after a bug report.

**Endpoint Authorization:**

```csharp
// Accessible ONLY by services with service authentication, or a SuperAdmin token
// (SuperAdmin is included so the Angular admin dashboard's tenant-edit/config UI can call it too)
publicGroup.MapGet("/config/{tenantId}", TenantApiHandlers.GetTenantConfigHandler)
    .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
    .WithName("GetTenantConfig");
```

**Important:** The `/api/v1/tenant/config/{tenantId}` endpoint requires either the `Service` role (via `X-Service-Secret`/`X-Service-Name`) or the `SuperAdmin` role (via a normal JWT) — it is **not** anonymous and **not** `Service`-only. Any service calling this endpoint must include service authentication headers; any admin UI calling it must send a SuperAdmin JWT.

---

## 🔌 Usage Examples

### Example 1: Identity Service Sending Notification

**Service:** `NotificationServiceClient.cs`

```csharp
public class NotificationServiceClient : INotificationServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public async Task<bool> SendNotificationAsync(
        string tenantId,
        int userId,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("NotificationService");

        var payload = new
        {
            tenantId = tenantId,
            userId = userId,
            title = title,
            message = message,
            data = data,
            deliveryType = "Both",
            priority = "Immediate"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/send");
        request.Headers.Add("x-tenant-id", tenantId);
        request.Content = JsonContent.Create(payload);

        // Service authentication headers already added by HttpClient configuration
        var response = await client.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode;
    }
}
```

**Usage in Handler:**

```csharp
public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDtoIncludesToken>
{
    private readonly IUserService _userService;
    private readonly INotificationServiceClient _notificationClient;
    private readonly ITenantContext _tenantContext;

    public async Task<UserDtoIncludesToken> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // Login logic
        var user = await _userService.LoginAsync(request, cancellationToken);

        var tenantId = _tenantContext.CurrentTenant?.TenantId;

        if (!string.IsNullOrEmpty(tenantId))
        {
            // Send welcome notification (fire-and-forget)
            _ = _notificationClient.SendNotificationAsync(
                tenantId: tenantId,
                userId: user.Id,
                title: "Welcome Back!",
                message: $"You logged in at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                data: "{\"event\":\"login\"}",
                cancellationToken: cancellationToken
            );
        }

        return user;
    }
}
```

### Example 2: Service-to-Service Request Flow

**Request from Identity Service to Notification Service:**

```http
POST https://localhost:5104/api/notifications/send
Content-Type: application/json
x-tenant-id: acme-corp
X-Service-Secret: CHANGE_ME_JWT_SECRET-service-secret-key
X-Service-Name: IdentityService

{
  "tenantId": "acme-corp",
  "userId": 42,
  "title": "Password Changed",
  "message": "Your password was successfully changed",
  "deliveryType": "Both",
  "priority": "Immediate"
}
```

**Processing:**

1. ✅ Request hits `ServiceAuthenticationMiddleware`
2. ✅ Middleware validates `X-Service-Secret` matches configuration
3. ✅ Middleware creates service identity with claims:
   - `Role = "Service"`
   - `IsInternalService = "true"`
   - `ServiceName = "IdentityService"`
4. ✅ Request proceeds to `UseAuthentication()` (already authenticated)
5. ✅ Request proceeds to `UseAuthorization()`
6. ✅ Endpoint requires role "User" OR "Service" → ✅ Authorized
7. ✅ Handler executes successfully

---

## 🎯 Endpoint Authorization Patterns

### Pattern 1: Allow Both Users and Services

```csharp
notificationGroup.MapPost("/send", NotificationApiHandlers.SendNotificationHandler)
    .RequireAuthorization(policy => policy.RequireRole("User", "Service"))
    .WithName("SendNotification");
```

**Use When:**

- Endpoint should be accessible by both authenticated users AND other services
- Example: Send notification endpoint

### Pattern 2: Allow Anonymous (Including Services)

```csharp
publicGroup.MapGet("/feature-flags", TenantApiHandlers.GetTenantFeatureFlagsHandler)
    .AllowAnonymous()
    .WithName("GetTenantFeatureFlags");
```

**Use When:**

- Endpoint must be accessible without any authentication
- Example: Tenant Service's `/feature-flags` endpoint — returns tenant-specific or default feature flags and is deliberately safe to call from any app with no auth. (**Not** `/config/{tenantId}` — that endpoint requires `Service` or `SuperAdmin`, see Pattern 3.)

### Pattern 3: Service Only (or Service + SuperAdmin)

```csharp
internalGroup.MapPost("/internal/operation", InternalHandlers.OperationHandler)
    .RequireAuthorization(policy => policy.RequireRole("Service"))
    .WithName("InternalOperation");

// A variant that also allows an admin UI to call the same endpoint with a normal JWT:
publicGroup.MapGet("/config/{tenantId}", TenantApiHandlers.GetTenantConfigHandler)
    .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
    .WithName("GetTenantConfig");
```

**Use When:**

- Endpoint should ONLY be accessible by services (and optionally SuperAdmin), not ordinary users
- Example: Internal administrative operations; Tenant Service's `/config/{tenantId}` (Service **or** SuperAdmin)

---

## 🔍 Debugging & Logging

### Service Authentication Logs

**Successful Authentication:**

```
[Debug] Authenticated service request from: IdentityService, IP: 127.0.0.1, Path: /api/notifications/send
```

**Invalid Secret:**

```
[Warning] Invalid service secret from IP: 127.0.0.1, Path: /api/notifications/send
```

**Service Not Whitelisted:**

```
[Warning] Service 'UnknownService' is not in the allowed services list. IP: 127.0.0.1, Path: /api/notifications/send
```

**Secret Present but `X-Service-Name` Missing:**

```
[Warning] Service secret presented with no X-Service-Name header. IP: 127.0.0.1, Path: /api/notifications/send
```

A valid `X-Service-Secret` with no `X-Service-Name` header (or an empty one) is rejected outright by this check — it no longer falls through to the whitelist check with a blank service name. Like the other two warnings above, the request still proceeds unauthenticated (no `Service` claims) rather than short-circuiting with a 401 directly from the middleware; the 401 comes from whichever endpoint's own role check runs next.

### Checking Service Authentication

In a handler, you can check if the request is from a service:

```csharp
var isServiceCall = context.User.Claims
    .Any(c => c.Type == "IsInternalService" && c.Value == "true");

var serviceName = context.User.Claims
    .FirstOrDefault(c => c.Type == "ServiceName")?.Value;

if (isServiceCall)
{
    _logger.LogInformation("Request from service: {ServiceName}", serviceName);
}
```

---

## 🚨 Security Considerations

### 1. Secret Management

**Development:**

```json
{
  "ServiceCommunication": {
    "SharedSecret": "dev-secret-key-do-not-use-in-production"
  }
}
```

**Production:**

```csharp
// Use Azure Key Vault or environment variables
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());

// Then in appsettings.json reference:
{
  "ServiceCommunication": {
    "SharedSecret": "${SERVICE_SHARED_SECRET}" // Loaded from Key Vault
  }
}
```

### 2. Network Security

- ✅ Use HTTPS for all service-to-service communication
- ✅ Deploy services in a private network (Azure VNet, AWS VPC)
- ✅ Use network security groups to restrict traffic
- ✅ Consider service mesh (Istio, Linkerd) for advanced scenarios

### 3. Secret Rotation

**Steps to rotate the shared secret:**

1. Update secret in all service configurations
2. Deploy services with new secret
3. Verify all service-to-service calls work
4. Monitor logs for authentication failures

**Gradual Rotation (Zero Downtime):**

```json
{
  "ServiceCommunication": {
    "SharedSecret": "new-secret",
    "FallbackSecrets": ["old-secret"] // Accept both during transition
  }
}
```

### 4. Audit Logging

Log all service-to-service calls:

```csharp
_logger.LogInformation(
    "Service call: {ServiceName} → {Endpoint}, Status: {Status}",
    serviceName,
    context.Request.Path,
    statusCode);
```

---

## 📊 Service Communication Matrix

| From Service | To Service   | Endpoint                               | Purpose                                              |
| ------------ | ------------ | --------------------------------------- | ------------------------------------------------------ |
| Identity     | Notification | `POST /api/notifications/send`         | Send user notifications                               |
| Identity     | FileManager  | `GET /api/filemanager/internal/...`    | Profile picture enrichment                            |
| Identity     | Tenant       | `GET /api/v1/tenant/config/{tenantId}` | Fetch tenant configuration (via `AddMultiTenancy()`)  |
| FileManager  | Tenant       | `GET /api/v1/tenant/config/{tenantId}` | Fetch tenant configuration (via `AddMultiTenancy()`)  |
| FileManager  | Tenant       | (typed client)                          | Background jobs (`TempFileCleanupJob`)                |
| Category     | FileManager  | `GET /api/filemanager/internal/...`    | Icon/image enrichment                                 |
| Category     | Tenant       | `GET /api/v1/tenant/config/{tenantId}` | Fetch tenant configuration (via `AddMultiTenancy()`)  |
| Notification | Identity     | (typed client)                          | Background jobs (`NotificationProcessor`)             |
| Notification | Tenant       | `GET /api/v1/tenant/config/{tenantId}` | Fetch tenant configuration (via `AddMultiTenancy()`)  |
| Nasheed      | FileManager  | `GET /api/filemanager/internal/...`    | File enrichment                                       |
| Nasheed      | Tenant       | `GET /api/v1/tenant/config/{tenantId}` | Fetch tenant configuration (via `AddMultiTenancy()`)  |
| Nasheed      | Notification | `POST /api/v1/notifications/send`      | Broadcast ingestion job-status changes (real-time UI updates) |
| Tenant       | Notification | `POST /api/notifications/send`         | Send admin notifications                              |

**Every multi-tenant service → Tenant row above shares the same `TenantServiceClient` code path** (`MultiTenancyExtensions.cs`) — see the whitelist warning above. Routes are versioned `/api/v1/...` per the API Versioning Standard; internal-only endpoints (`/api/filemanager/internal/...`) stay unversioned by design.

---

## ✅ Checklist for Adding Service Communication

### For Calling Service (e.g., Identity)

- [ ] Add `ServiceCommunication` configuration to appsettings.json
- [ ] Add target service URL to `Services` section
- [ ] Configure HttpClient with service headers in Program.cs
- [ ] Add `using IhsanDev.Shared.Infrastructure.Middleware;`
- [ ] Add `app.UseServiceAuthentication()` before `UseAuthentication()`
- [ ] Create service client class (e.g., `NotificationServiceClient`)
- [ ] Register service client in DI container
- [ ] **If this is a new multi-tenant service (`AddMultiTenancy()`): add its `ServiceName` to Tenant Service's `AllowedServices` in both `appsettings.json` and `appsettings.Development.json`.** This is the step that's easy to forget — nothing fails at startup, and the 30-min tenant-config cache can hide the gap for a long time until it goes cold.

### For Called Service (e.g., Notification)

- [ ] Add `ServiceCommunication` configuration to appsettings.json
- [ ] Add `using IhsanDev.Shared.Infrastructure.Middleware;`
- [ ] Add `app.UseServiceAuthentication()` before `UseAuthentication()`
- [ ] Update endpoint authorization to allow "Service" role
- [ ] Test with service authentication headers

---

## 🧪 Testing Service Communication

### Manual Testing with cURL

```bash
curl -X POST https://localhost:5104/api/notifications/send \
  -H "Content-Type: application/json" \
  -H "x-tenant-id: acme-corp" \
  -H "X-Service-Secret: CHANGE_ME_JWT_SECRET-service-secret-key" \
  -H "X-Service-Name: TestService" \
  -d '{
    "tenantId": "acme-corp",
    "userId": 1,
    "title": "Test Notification",
    "message": "Testing service-to-service communication",
    "deliveryType": "Both",
    "priority": "Immediate"
  }'
```

### Integration Test Example

```csharp
[Fact]
public async Task SendNotification_WithServiceAuth_ShouldSucceed()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Service-Secret", "test-secret");
    client.DefaultRequestHeaders.Add("X-Service-Name", "TestService");
    client.DefaultRequestHeaders.Add("x-tenant-id", "test-tenant");

    var payload = new
    {
        tenantId = "test-tenant",
        userId = 1,
        title = "Test",
        message = "Test message"
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/notifications/send", payload);

    // Assert
    response.IsSuccessStatusCode.Should().BeTrue();
}
```

---

## 🎓 Best Practices

1. **Always use the HttpClient factory** - Prevents socket exhaustion
2. **Use fire-and-forget for non-critical operations** - Don't block user requests
3. **Implement retry logic** - Services may be temporarily unavailable
4. **Log all service calls** - Essential for debugging
5. **Use circuit breaker pattern** - Prevent cascade failures
6. **Set appropriate timeouts** - Default 30 seconds for service calls
7. **Handle failures gracefully** - Don't crash if notification fails
8. **Monitor service health** - Use health check endpoints

---

## 🔄 Migration from JWT to Service Auth

If you previously used JWT tokens for service calls:

**Before:**

```csharp
client.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceAccountToken}");
```

**After:**

```csharp
client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
client.DefaultRequestHeaders.Add("X-Service-Name", "IdentityService");
```

**Benefits:**

- ✅ No need to generate/manage service account tokens
- ✅ No token expiration issues
- ✅ Simpler configuration
- ✅ Better performance (no token validation overhead)

---

## 📚 Related Documentation

- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Multi-tenancy patterns
- [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md) - Notification service details
- [SHARED_IDENTITY_SERVICE_GUIDE.md](SHARED_IDENTITY_SERVICE_GUIDE.md) - JWT authentication flow
- [LOAD_TESTING_GUIDE.md](LOAD_TESTING_GUIDE.md) - How the missing-`AllowedServices` bug for Category/Nasheed was found via load testing

---

**Last Updated:** August 2026  
**Version:** 1.4.0 (Corrected against actual source: Tenant Service's real 7-entry `AllowedServices` list; `/config/{tenantId}`'s real `RequireRole("Service", "SuperAdmin")` policy made consistent everywhere it's referenced, replacing the previous 3 contradicting descriptions; `TenantServiceClient` code snippet now includes the real dev-SSL-bypass handler and `AddStandardResilienceHandler` values; corrected the missing-`SharedSecret` pitfall to describe the real `ValidateSecretStrength` startup-crash behavior instead of a silent 401; added the third rejection log line (`X-Service-Name` missing); Identity Service's HttpClient snippet now shows the real `AddNotificationServiceClient`/`AddFileManagerServiceClient` extension-method pattern instead of inline `AddHttpClient`)

**Version:** 1.3.0 (Added Nasheed → Notification consumer for real-time ingestion progress broadcasts; whitelisted `NasheedService` in Notification's `AllowedServices`)

**Version:** 1.2.0 (Added missing-`SharedSecret`-override pitfall found for Nasheed; Service Communication Matrix corrected to match actual code; `AllowedServices` whitelist pitfall added; fixed dead link to non-existent JWT_SECRET_AND_VALIDATION_FLOW.md)
