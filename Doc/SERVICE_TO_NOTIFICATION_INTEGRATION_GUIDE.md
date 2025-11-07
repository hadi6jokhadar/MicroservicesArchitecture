# Service-to-Notification Integration Guide

**Last Updated:** November 7, 2025  
**Version:** 1.0.0

This guide explains how to integrate notification sending capability into any microservice (Identity, Tenant, or custom services) to send notifications to users in specific tenants.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Step 1: Add Configuration](#step-1-add-configuration)
- [Step 2: Register HttpClient](#step-2-register-httpclient)
- [Step 3: Create Notification Service Client](#step-3-create-notification-service-client)
- [Step 4: Register the Service](#step-4-register-the-service)
- [Step 5: Use in Your Service](#step-5-use-in-your-service)
- [Authentication Options](#authentication-options)
- [Complete Examples](#complete-examples)
- [Troubleshooting](#troubleshooting)

---

## Overview

Any microservice in the architecture can send notifications to users by making HTTP requests to the **Notification Service**. This guide provides a reusable pattern for integrating notification capabilities.

### Key Components

- **Notification Service**: `https://localhost:5104` (default)
- **Endpoint**: `POST /api/notifications/send`
- **Required Headers**: `x-tenant-id`, `Authorization` (optional based on setup)
- **Multi-Tenancy**: Fully supported

---

## Prerequisites

Before starting, ensure:

1. ✅ **Notification Service is running** on port 5104 (HTTPS) or 5004 (HTTP)
2. ✅ **Multi-tenancy is configured** in both services
3. ✅ **Tenant Service is running** (for tenant database resolution)
4. ✅ **Database migrations applied** for Notification Service

---

## Step 1: Add Configuration

Add the Notification Service connection details to your service's `appsettings.json`.

### Example: `Identity.API/appsettings.json`

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

### Configuration Properties

| Property  | Description                     | Default                  |
| --------- | ------------------------------- | ------------------------ |
| `BaseUrl` | Notification Service base URL   | `https://localhost:5104` |
| `Timeout` | HTTP request timeout in seconds | `30`                     |

---

## Step 2: Register HttpClient

Register an `HttpClient` for the Notification Service in your `Program.cs`.

### Example: `Identity.API/Program.cs`

```csharp
// ============================================
// HttpClient for Notification Service
// ============================================
builder.Services.AddHttpClient("NotificationService", client =>
{
    var baseUrl = builder.Configuration["Services:NotificationService:BaseUrl"]
        ?? "https://localhost:5104";

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("Services:NotificationService:Timeout", 30)
    );
});
```

### Key Points

- ✅ Uses `IHttpClientFactory` for proper connection pooling
- ✅ Configurable base URL and timeout
- ✅ Reusable across your service

---

## Step 3: Create Notification Service Client

Create a client class to encapsulate notification sending logic.

### Interface: `INotificationServiceClient.cs`

Create in your `Infrastructure` layer (e.g., `Identity.Infrastructure/Services/`):

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Infrastructure.Services;

/// <summary>
/// Client for sending notifications via Notification Service
/// </summary>
public interface INotificationServiceClient
{
    /// <summary>
    /// Send a notification to a specific user in a tenant
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="title">Notification title (max 200 characters)</param>
    /// <param name="message">Notification message (max 1000 characters)</param>
    /// <param name="data">Optional JSON data payload</param>
    /// <param name="deliveryType">SignalR, Firebase, or Both (default: Both)</param>
    /// <param name="priority">Immediate or Waitable (default: Immediate)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if notification was queued successfully</returns>
    Task<bool> SendNotificationAsync(
        string tenantId,
        int userId,
        string title,
        string message,
        string? data = null,
        string deliveryType = "Both",
        string priority = "Immediate",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a tenant-wide notification (broadcast to all users in tenant)
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="data">Optional JSON data payload</param>
    /// <param name="deliveryType">SignalR, Firebase, or Both (default: Both)</param>
    /// <param name="priority">Immediate or Waitable (default: Immediate)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if notification was queued successfully</returns>
    Task<bool> SendTenantNotificationAsync(
        string tenantId,
        string title,
        string message,
        string? data = null,
        string deliveryType = "Both",
        string priority = "Immediate",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a global notification (to all connected clients)
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="data">Optional JSON data payload</param>
    /// <param name="deliveryType">SignalR, Firebase, or Both (default: Both)</param>
    /// <param name="priority">Immediate or Waitable (default: Immediate)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if notification was queued successfully</returns>
    Task<bool> SendGlobalNotificationAsync(
        string title,
        string message,
        string? data = null,
        string deliveryType = "Both",
        string priority = "Immediate",
        CancellationToken cancellationToken = default);
}
```

### Implementation: `NotificationServiceClient.cs`

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

/// <summary>
/// Client for sending notifications via Notification Service
/// </summary>
public class NotificationServiceClient : INotificationServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationServiceClient> _logger;

    public NotificationServiceClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NotificationServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendNotificationAsync(
        string tenantId,
        int userId,
        string title,
        string message,
        string? data = null,
        string deliveryType = "Both",
        string priority = "Immediate",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");

            // Create notification payload
            var payload = new
            {
                tenantId = tenantId,
                userId = userId,
                title = title,
                message = message,
                data = data,
                deliveryType = deliveryType,
                priority = priority
            };

            // Prepare request with tenant header
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/send")
            {
                Content = JsonContent.Create(payload)
            };

            // Add tenant header (REQUIRED for multi-tenancy)
            request.Headers.Add("x-tenant-id", tenantId);

            // Optional: Add authentication if Notification Service requires it
            // See "Authentication Options" section below
            // var token = await GetServiceAccountTokenAsync();
            // request.Headers.Add("Authorization", $"Bearer {token}");

            // Send request
            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Notification sent successfully to user {UserId} in tenant {TenantId}: {Title}",
                    userId, tenantId, title);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to send notification. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending notification to user {UserId} in tenant {TenantId}",
                userId, tenantId);
            return false;
        }
    }

    public async Task<bool> SendTenantNotificationAsync(
        string tenantId,
        string title,
        string message,
        string? data = null,
        string deliveryType = "Both",
        string priority = "Immediate",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");

            // Create notification payload (userId = null for tenant broadcast)
            var payload = new
            {
                tenantId = tenantId,
                userId = (int?)null,  // Null = broadcast to all users in tenant
                title = title,
                message = message,
                data = data,
                deliveryType = deliveryType,
                priority = priority
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/send")
            {
                Content = JsonContent.Create(payload)
            };

            request.Headers.Add("x-tenant-id", tenantId);

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Tenant notification sent successfully to tenant {TenantId}: {Title}",
                    tenantId, title);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to send tenant notification. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending tenant notification to tenant {TenantId}",
                tenantId);
            return false;
        }
    }

    public async Task<bool> SendGlobalNotificationAsync(
        string title,
        string message,
        string? data = null,
        string deliveryType = "Both",
        string priority = "Immediate",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");

            // Create notification payload (tenantId = null, userId = null for global)
            var payload = new
            {
                tenantId = (string?)null,  // Null = global notification
                userId = (int?)null,
                title = title,
                message = message,
                data = data,
                deliveryType = deliveryType,
                priority = priority
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/send")
            {
                Content = JsonContent.Create(payload)
            };

            // No tenant header needed for global notifications

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Global notification sent successfully: {Title}",
                    title);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to send global notification. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending global notification");
            return false;
        }
    }
}
```

---

## Step 4: Register the Service

Register the `INotificationServiceClient` in your dependency injection container.

### Example: `Identity.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Identity.Infrastructure.Services;

namespace Identity.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // ... existing services ...

        // Register Notification Service Client
        services.AddScoped<INotificationServiceClient, NotificationServiceClient>();

        return services;
    }
}
```

---

## Step 5: Use in Your Service

Inject and use `INotificationServiceClient` in your handlers, services, or controllers.

### Example 1: Send Notification After User Login

```csharp
using MediatR;
using Identity.Application.Commands.Auth;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Infrastructure.Services;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Identity.Application.Handlers;

public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDtoIncludesToken>
{
    private readonly IUserService _userService;
    private readonly INotificationServiceClient _notificationClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserService userService,
        INotificationServiceClient notificationClient,
        ITenantContext tenantContext,
        ILogger<LoginCommandHandler> logger)
    {
        _userService = userService;
        _notificationClient = notificationClient;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<UserDtoIncludesToken> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // Perform login
        var user = await _userService.LoginAsync(request, cancellationToken);

        // Get tenant ID from context
        var tenantId = _tenantContext.CurrentTenant?.TenantId;

        // Send welcome notification (fire-and-forget)
        if (!string.IsNullOrEmpty(tenantId))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationClient.SendNotificationAsync(
                        tenantId: tenantId,
                        userId: user.Id,
                        title: "Welcome Back!",
                        message: $"You logged in at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                        data: $"{{\"event\":\"login\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}",
                        cancellationToken: CancellationToken.None
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send login notification");
                }
            }, CancellationToken.None);
        }

        return user;
    }
}
```

### Example 2: Send Notification After Password Change

```csharp
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private readonly IUserService _userService;
    private readonly INotificationServiceClient _notificationClient;
    private readonly ITenantContext _tenantContext;

    public async Task<bool> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        // Change password
        var success = await _userService.ChangePasswordAsync(request, cancellationToken);

        if (success)
        {
            var tenantId = _tenantContext.CurrentTenant?.TenantId;

            if (!string.IsNullOrEmpty(tenantId))
            {
                // Send security notification
                await _notificationClient.SendNotificationAsync(
                    tenantId: tenantId,
                    userId: request.UserId,
                    title: "Password Changed",
                    message: "Your password was successfully changed. If you didn't make this change, please contact support immediately.",
                    data: $"{{\"event\":\"password_change\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}",
                    priority: "Immediate",
                    cancellationToken: cancellationToken
                );
            }
        }

        return success;
    }
}
```

### Example 3: Send Tenant-Wide Notification

```csharp
public class TenantMaintenanceService
{
    private readonly INotificationServiceClient _notificationClient;

    public async Task NotifyMaintenanceAsync(string tenantId, DateTime scheduledTime)
    {
        await _notificationClient.SendTenantNotificationAsync(
            tenantId: tenantId,
            title: "Scheduled Maintenance",
            message: $"System maintenance scheduled for {scheduledTime:yyyy-MM-dd HH:mm} UTC. Please save your work.",
            data: $"{{\"event\":\"maintenance\",\"scheduledTime\":\"{scheduledTime:O}\"}}",
            priority: "Immediate"
        );
    }
}
```

### Example 4: Send Global Notification

```csharp
public class SystemAnnouncementService
{
    private readonly INotificationServiceClient _notificationClient;

    public async Task AnnounceNewFeatureAsync()
    {
        await _notificationClient.SendGlobalNotificationAsync(
            title: "New Feature Available!",
            message: "Check out our new dashboard redesign. Click here to learn more.",
            data: "{\"event\":\"feature_announcement\",\"featureId\":\"dashboard-v2\"}",
            priority: "Waitable"
        );
    }
}
```

---

## Authentication Options

The Notification Service requires authentication by default. Choose one of the following options:

### Option 1: Modify Notification Endpoint (Allow Service Role)

Update `Notification.API/Extensions/EndpointMappingExtensions.cs`:

```csharp
var notificationGroup = app.MapGroup("/api/notifications")
    .WithTags("Notifications")
    .RequireAuthorization(policy => policy.RequireRole("User", "Service")) // Add "Service" role
    .WithOpenApi();
```

Then generate a service account JWT with role `"Service"` and include it in requests:

```csharp
// In NotificationServiceClient.cs
private async Task<string> GetServiceAccountTokenAsync()
{
    // Generate or retrieve service account JWT
    // This should be a long-lived token with role "Service"
    return await _tokenProvider.GetServiceTokenAsync();
}

// In SendNotificationAsync method
request.Headers.Add("Authorization", $"Bearer {await GetServiceAccountTokenAsync()}");
```

### Option 2: Create Internal Endpoint (No Auth)

Add an internal endpoint in `Notification.API/Extensions/EndpointMappingExtensions.cs`:

```csharp
// Internal endpoints (no authentication required)
var internalGroup = app.MapGroup("/internal/notifications")
    .WithTags("Internal")
    .ExcludeFromDescription(); // Hide from Swagger

internalGroup.MapPost("/send", NotificationApiHandlers.SendNotificationHandler)
    .AllowAnonymous()
    .WithName("InternalSendNotification");
```

Then update the client to use the internal endpoint:

```csharp
// In NotificationServiceClient.cs
var request = new HttpRequestMessage(HttpMethod.Post, "/internal/notifications/send")
{
    Content = JsonContent.Create(payload)
};
```

**Security Note:** Use IP whitelisting middleware to restrict access to internal network only.

### Option 3: Use System User JWT

Create a "system" user with role `"User"` and generate a long-lived JWT for service-to-service calls. Store the token in configuration or a secure vault.

---

## Complete Examples

### Notification Types

| Type                 | `tenantId`    | `userId` | Who Receives                        |
| -------------------- | ------------- | -------- | ----------------------------------- |
| **User-Specific**    | `"acme-corp"` | `42`     | User 42 in tenant acme-corp         |
| **Tenant Broadcast** | `"acme-corp"` | `null`   | All users in tenant acme-corp       |
| **Global**           | `null`        | `null`   | All connected clients (all tenants) |

### Full Request Example

```http
POST https://localhost:5104/api/notifications/send
Content-Type: application/json
x-tenant-id: acme-corp
Authorization: Bearer {service-account-jwt}

{
  "tenantId": "acme-corp",
  "userId": 42,
  "title": "Password Changed",
  "message": "Your password was successfully changed at 2025-11-07 14:30:00 UTC",
  "data": "{\"event\":\"password_change\",\"timestamp\":\"2025-11-07T14:30:00Z\"}",
  "deliveryType": "Both",
  "priority": "Immediate"
}
```

### Response

```json
{
  "queueItemId": 123,
  "status": "Queued",
  "queuedAt": "2025-11-07T14:30:00Z",
  "priority": "Immediate",
  "deliveryType": "Both"
}
```

---

## Troubleshooting

### Issue 1: Connection Refused

**Symptom**: `HttpRequestException: Connection refused`

**Solution**:

- Verify Notification Service is running
- Check the `BaseUrl` in configuration matches the actual service URL
- Ensure firewall allows connections to port 5104

### Issue 2: 401 Unauthorized

**Symptom**: HTTP 401 response

**Solution**:

- Check authentication option is properly configured
- Verify service account JWT is valid and not expired
- Ensure role is correct (`"User"` or `"Service"`)

### Issue 3: 400 Bad Request (Tenant Not Found)

**Symptom**: HTTP 400 response with "Tenant not found" error

**Solution**:

- Verify `x-tenant-id` header is included
- Check tenant exists in Tenant Service
- Ensure Tenant Service is running

### Issue 4: Notification Not Received by User

**Symptom**: Request succeeds but user doesn't receive notification

**Solution**:

- Check user is connected to SignalR hub
- Verify user has valid JWT token
- Ensure user is in correct tenant
- Check Notification Service background processor logs

### Issue 5: Timeout Errors

**Symptom**: Request times out after 30 seconds

**Solution**:

- Increase timeout in configuration
- Check Notification Service health
- Verify network connectivity

---

## Summary Checklist

Use this checklist when integrating notification sending into a new service:

- [ ] Add `Services:NotificationService` configuration to `appsettings.json`
- [ ] Register `HttpClient` for NotificationService in `Program.cs`
- [ ] Create `INotificationServiceClient` interface
- [ ] Create `NotificationServiceClient` implementation
- [ ] Register service in DI container
- [ ] Choose and implement authentication option
- [ ] Test sending notification to specific user
- [ ] Test sending notification to tenant
- [ ] Test sending global notification
- [ ] Add error handling and logging
- [ ] Document usage in your service README

---

## Related Documentation

- **Notification Service README**: [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md)
- **Notification System Flow**: [NOTIFICATION_SYSTEM_FLOW.md](NOTIFICATION_SYSTEM_FLOW.md)
- **Notification Hub Guide**: [NOTIFICATION_HUB_GUIDE.md](NOTIFICATION_HUB_GUIDE.md)
- **Multi-Tenancy Guide**: [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md)

---

**Last Updated:** November 7, 2025  
**Maintained by:** Development Team
