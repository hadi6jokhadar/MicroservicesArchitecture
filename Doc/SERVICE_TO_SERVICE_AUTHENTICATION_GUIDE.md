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

### Automatic Service Authentication for Tenant Service Client

When using multi-tenancy, the `TenantServiceClient` is **automatically configured** with service authentication headers by the `AddMultiTenancy()` extension method.

**Location:** `Shared.Infrastructure/Extensions/MultiTenancyExtensions.cs`

**What it does:**

- Reads `ServiceCommunication:SharedSecret` from configuration
- Reads `ServiceCommunication:ServiceName` from configuration (or falls back to `ApplicationName`)
- Automatically adds `X-Service-Secret` and `X-Service-Name` headers to all Tenant Service API calls
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
});
```

**This means:** When you enable multi-tenancy with `AddMultiTenancy()`, the Tenant Service client is automatically ready for service-to-service authentication!

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

**HttpClient Setup (Program.cs):**

```csharp
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

**Called by:** Identity Service, Tenant Service

**Configuration:**

```json
{
  "ServiceCommunication": {
    "Enabled": true,
    "ServiceName": "NotificationService",
    "SharedSecret": "CHANGE_ME_JWT_SECRET-service-secret-key",
    "AllowedServices": ["IdentityService", "TenantService"]
  }
}
```

**Endpoint Authorization:**

```csharp
var notificationGroup = app.MapGroup("/api/notifications")
    .WithTags("Notifications")
    .RequireAuthorization(policy => policy.RequireRole("User", "Service"))  // ← Allow Service role
    .WithOpenApi();
```

### Tenant Service

**Called by:** Identity Service, Notification Service

**Configuration:**

```json
{
  "ServiceCommunication": {
    "Enabled": true,
    "ServiceName": "TenantService",
    "SharedSecret": "CHANGE_ME_JWT_SECRET-service-secret-key",
    "AllowedServices": ["IdentityService", "NotificationService"]
  }
}
```

**Endpoint Authorization:**

```csharp
// Tenant config endpoint is now restricted to Service role ONLY
publicGroup.MapGet("/config/{tenantId}", TenantApiHandlers.GetTenantConfigHandler)
    .RequireAuthorization(policy => policy.RequireRole("Service"))  // ← Service-only endpoint
    .WithName("GetTenantConfig");
```

**Important:** The `/api/tenant/config/{tenantId}` endpoint is now **service-only** and requires service authentication. Any service calling this endpoint must include service authentication headers.

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
publicGroup.MapGet("/config/{tenantId}", TenantApiHandlers.GetTenantConfigHandler)
    .AllowAnonymous()
    .WithName("GetTenantConfig");
```

**Use When:**

- Endpoint must be accessible without any authentication
- Example: Tenant config endpoint (used by middleware)

### Pattern 3: Service Only

```csharp
internalGroup.MapPost("/internal/operation", InternalHandlers.OperationHandler)
    .RequireAuthorization(policy => policy.RequireRole("Service"))
    .WithName("InternalOperation");
```

**Use When:**

- Endpoint should ONLY be accessible by services, not users
- Example: Internal administrative operations

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

| From Service | To Service   | Endpoint                            | Purpose                    |
| ------------ | ------------ | ----------------------------------- | -------------------------- |
| Identity     | Notification | `POST /api/notifications/send`      | Send user notifications    |
| Identity     | Tenant       | `GET /api/tenant/config/{tenantId}` | Fetch tenant configuration |
| Notification | Tenant       | `GET /api/tenant/config/{tenantId}` | Fetch tenant configuration |
| Tenant       | Notification | `POST /api/notifications/send`      | Send admin notifications   |

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
- [JWT_SECRET_AND_VALIDATION_FLOW.md](JWT_SECRET_AND_VALIDATION_FLOW.md) - JWT authentication

---

**Last Updated:** November 7, 2025  
**Version:** 1.0.0
