# Notification Hub Guide

## Overview

The **NotificationHub** supports flexible connection modes and notification targeting strategies based on authentication status and multi-tenancy configuration. This guide explains all supported scenarios and how to use them.

---

## Configuration

### Multi-Tenancy Setting

The hub behavior is controlled by the `MultiTenancy:Enabled` configuration in `appsettings.json`:

```json
{
  "MultiTenancy": {
    "Enabled": true // or false
  }
}
```

- **`true`**: Multi-tenant mode - supports tenant-specific databases and tenant-scoped notifications
- **`false`**: Single-tenant mode - all notifications and data in one database

---

## Connection Modes

### JWT Authentication for SignalR Hub

The hub supports **dual JWT authentication** based on the `JwtMode` configuration:

#### JwtMode: Shared

All connections validate JWT tokens using the **global JWT secret** from `appsettings.json`.

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => globalJwtToken,
  })
  .build();
```

#### JwtMode: PerTenant

Connections validate JWT tokens based on whether a `tenantId` is provided:

**With tenantId** - Validates using **tenant-specific JWT secret** from database:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications?tenantId=ihsandev", {
    accessTokenFactory: () => tenantSpecificJwtToken,
  })
  .build();
```

**Without tenantId** - Validates using **global JWT secret** from appsettings.json:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => globalJwtToken,
  })
  .build();
```

**Important Notes:**

- The `tenantId` parameter must be in the **query string** of the WebSocket URL
- Token validation parameters are set during the `OnMessageReceived` event
- Each request gets fresh validation parameters to prevent cross-request pollution
- The hub uses `OptionalTenantAttribute`, so tenant context is optional

---

### 1. Authenticated Connection (With JWT Token)

**Recommended for personalized notifications**

#### JavaScript/TypeScript Client

```javascript
// For tenant users (PerTenant mode)
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications?tenantId=tenant-123", {
    accessTokenFactory: () => getTenantJwtToken(), // Tenant-specific JWT
  })
  .withAutomaticReconnect()
  .build();

// For SuperAdmin or global users
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => getGlobalJwtToken(), // Global JWT
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```

#### What Happens

- Token validated (tenant-specific or global based on tenantId parameter)
- UserId extracted from JWT claims (`ClaimTypes.NameIdentifier`)
- User can receive:
  - ✅ Global notifications
  - ✅ User-specific notifications
  - ✅ Tenant-wide notifications (if tenantId provided)

---

### 2. Anonymous Connection (Without JWT Token)

**For public notifications or non-authenticated users**

#### JavaScript/TypeScript Client

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications?tenantId=tenant-123") // No token, but can specify tenant
  .withAutomaticReconnect()
  .build();

await connection.start();
```

#### What Happens

- No authentication required
- No userId available
- User can receive:
  - ✅ Global notifications
  - ✅ Tenant-wide notifications (if tenantId provided in URL)
  - ❌ User-specific notifications (no userId to target)

---

## SignalR Groups

The hub automatically assigns connections to appropriate groups based on authentication and tenant context:

### Multi-Tenancy Mode (`MultiTenancy:Enabled = true`)

| Connection Type               | Groups Joined                                                    | Description                                       |
| ----------------------------- | ---------------------------------------------------------------- | ------------------------------------------------- |
| **Anonymous + No Tenant**     | `global`                                                         | Only receives global broadcasts                   |
| **Anonymous + Tenant**        | `global`, `tenant:{tenantId}`                                    | Receives global + tenant broadcasts               |
| **Authenticated + No Tenant** | `global`, `user:{userId}`                                        | Receives global + cross-tenant user notifications |
| **Authenticated + Tenant**    | `global`, `tenant:{tenantId}`, `tenant:{tenantId}:user:{userId}` | Receives all notification types                   |

### Single-Tenant Mode (`MultiTenancy:Enabled = false`)

| Connection Type   | Groups Joined                            | Description                              |
| ----------------- | ---------------------------------------- | ---------------------------------------- |
| **Anonymous**     | `global`, `all-clients`                  | Receives global + all-clients broadcasts |
| **Authenticated** | `global`, `all-clients`, `user:{userId}` | Receives all notification types          |

---

## Notification Sending Scenarios

### Scenario 1: Global Notification (All Clients)

**Use Case**: System announcements, maintenance notices, emergency alerts

**Target**: Every connected client (authenticated and anonymous, all tenants)

#### API Request

```json
POST /api/notifications/send
{
  "tenantId": null,
  "userId": null,
  "title": "System Maintenance",
  "message": "Scheduled maintenance in 30 minutes",
  "deliveryType": "SignalR",
  "priority": "Immediate"
}
```

#### SignalR Group

- `global`

#### Who Receives

- ✅ All authenticated users
- ✅ All anonymous users
- ✅ All tenants (if multi-tenancy enabled)
- ✅ Single-tenant mode users

---

### Scenario 2: All Clients in Single-Tenant Mode

**Use Case**: Broadcast to everyone when `MultiTenancy:Enabled = false`

**Target**: All connected clients in single-tenant application

#### API Request

```json
POST /api/notifications/send
{
  "tenantId": null,
  "userId": null,
  "title": "New Feature Available",
  "message": "Check out our new dashboard",
  "deliveryType": "SignalR",
  "priority": "Waitable"
}
```

#### Configuration Required

```json
{
  "MultiTenancy": {
    "Enabled": false
  }
}
```

#### SignalR Group

- `all-clients`

#### Who Receives

- ✅ All authenticated users
- ✅ All anonymous users
- ✅ Same as global in single-tenant context

---

### Scenario 3: Tenant-Wide Notification

**Use Case**: Announcements for specific organization/tenant

**Target**: All users (authenticated and anonymous) connected to a specific tenant

#### API Request

```json
POST /api/notifications/send
{
  "tenantId": "acme-corp",
  "userId": null,
  "title": "Company Meeting",
  "message": "All-hands meeting at 3 PM",
  "deliveryType": "SignalR",
  "priority": "Immediate"
}
```

#### Configuration Required

```json
{
  "MultiTenancy": {
    "Enabled": true
  }
}
```

#### SignalR Group

- `tenant:acme-corp`

#### Who Receives

- ✅ All authenticated users in tenant "acme-corp"
- ✅ All anonymous users connected with `x-tenant-id: acme-corp`
- ❌ Users in other tenants
- ❌ Users without tenant context

---

### Scenario 4: User-Specific in Tenant

**Use Case**: Personal notifications in multi-tenant environment

**Target**: Specific authenticated user within a tenant

#### API Request

```json
POST /api/notifications/send
{
  "tenantId": "acme-corp",
  "userId": 5,
  "title": "New Task Assigned",
  "message": "You have been assigned to Project Alpha",
  "deliveryType": "Both",
  "priority": "Immediate"
}
```

#### Configuration Required

```json
{
  "MultiTenancy": {
    "Enabled": true
  }
}
```

#### SignalR Group

- `tenant:acme-corp:user:5`

#### Who Receives

- ✅ User with ID 5 connected to tenant "acme-corp" with valid JWT
- ❌ Other users
- ❌ Anonymous connections (no userId)
- ❌ Same user in different tenant

---

### Scenario 5: User-Specific (Single-Tenant Mode)

**Use Case**: Personal notifications in single-tenant application

**Target**: Specific authenticated user (no tenant context needed)

#### API Request

```json
POST /api/notifications/send
{
  "tenantId": null,
  "userId": 5,
  "title": "Password Changed",
  "message": "Your password was successfully updated",
  "deliveryType": "SignalR",
  "priority": "Immediate"
}
```

#### Configuration Required

```json
{
  "MultiTenancy": {
    "Enabled": false
  }
}
```

#### SignalR Group

- `user:5`

#### Who Receives

- ✅ User with ID 5 with valid JWT token
- ❌ Other users
- ❌ Anonymous connections

---

## Client Connection Examples

### JavaScript/TypeScript - Authenticated User in Tenant

```typescript
class NotificationClient {
  private connection: signalR.HubConnection;

  async connect(jwtToken: string, tenantId?: string) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/notifications", {
        accessTokenFactory: () => jwtToken,
      })
      .withAutomaticReconnect()
      .build();

    // Add tenant header if multi-tenancy is enabled
    if (tenantId) {
      this.connection.headers = { "x-tenant-id": tenantId };
    }

    // Subscribe to notifications
    this.connection.on("ReceiveNotification", (notification) => {
      this.handleNotification(notification);
      this.acknowledgeDelivery(notification.queueItemId);
    });

    await this.connection.start();
    console.log("✅ Connected as authenticated user");
  }

  private handleNotification(notification: any) {
    console.log("📩 New notification:", notification);

    // Display to user (toast, push notification, etc.)
    this.showNotification(notification.title, notification.message);
  }

  private async acknowledgeDelivery(queueItemId: number) {
    try {
      await this.connection.invoke("AcknowledgeDelivery", queueItemId);
      console.log("✅ Acknowledged:", queueItemId);
    } catch (err) {
      console.error("❌ Ack failed:", err);
    }
  }

  private showNotification(title: string, message: string) {
    if (Notification.permission === "granted") {
      new Notification(title, { body: message });
    }
  }

  async disconnect() {
    await this.connection.stop();
  }
}

// Usage - Authenticated user in tenant
const client = new NotificationClient();
await client.connect("eyJhbGciOiJIUzI1Ni...", "acme-corp");
```

---

### JavaScript/TypeScript - Anonymous User

```typescript
class AnonymousNotificationClient {
  private connection: signalR.HubConnection;

  async connect(tenantId?: string) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/notifications") // No token
      .withAutomaticReconnect()
      .build();

    // Optionally add tenant for tenant-wide broadcasts
    if (tenantId) {
      this.connection.headers = { "x-tenant-id": tenantId };
    }

    // Subscribe to notifications
    this.connection.on("ReceiveNotification", (notification) => {
      console.log("📩 Global/Tenant notification:", notification);
      this.displayPublicNotification(notification);
    });

    await this.connection.start();
    console.log("✅ Connected anonymously");
  }

  private displayPublicNotification(notification: any) {
    // Display public announcements
    const banner = document.createElement("div");
    banner.className = "notification-banner";
    banner.textContent = `${notification.title}: ${notification.message}`;
    document.body.appendChild(banner);
  }

  async disconnect() {
    await this.connection.stop();
  }
}

// Usage - Anonymous user (only global notifications)
const publicClient = new AnonymousNotificationClient();
await publicClient.connect();

// Usage - Anonymous user in tenant (global + tenant broadcasts)
const tenantPublicClient = new AnonymousNotificationClient();
await tenantPublicClient.connect("acme-corp");
```

---

### C# Client - Xamarin/MAUI

```csharp
public class NotificationClient
{
    private HubConnection _connection;
    private readonly bool _isMultiTenancyEnabled;

    public NotificationClient(bool isMultiTenancyEnabled = true)
    {
        _isMultiTenancyEnabled = isMultiTenancyEnabled;
    }

    // Authenticated connection
    public async Task ConnectAuthenticatedAsync(string jwtToken, string tenantId = null)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl("https://api.example.com/hubs/notifications", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(jwtToken);

                if (_isMultiTenancyEnabled && !string.IsNullOrEmpty(tenantId))
                {
                    options.Headers.Add("x-tenant-id", tenantId);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        ConfigureHandlers();
        await _connection.StartAsync();
        Debug.WriteLine("✅ Connected as authenticated user");
    }

    // Anonymous connection
    public async Task ConnectAnonymousAsync(string tenantId = null)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl("https://api.example.com/hubs/notifications", options =>
            {
                if (_isMultiTenancyEnabled && !string.IsNullOrEmpty(tenantId))
                {
                    options.Headers.Add("x-tenant-id", tenantId);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        ConfigureHandlers();
        await _connection.StartAsync();
        Debug.WriteLine("✅ Connected anonymously");
    }

    private void ConfigureHandlers()
    {
        _connection.On<NotificationPayload>("ReceiveNotification", notification =>
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                HandleNotification(notification);
            });
        });
    }

    private void HandleNotification(NotificationPayload notification)
    {
        // Display local notification
        DependencyService.Get<INotificationService>()
            .ShowNotification(notification.Title, notification.Message);

        // Acknowledge if authenticated
        _ = AcknowledgeDeliveryAsync(notification.QueueItemId);
    }

    private async Task AcknowledgeDeliveryAsync(int queueItemId)
    {
        try
        {
            await _connection.InvokeAsync("AcknowledgeDelivery", queueItemId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Ack failed: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        await _connection.StopAsync();
    }
}

// Usage examples
var client = new NotificationClient(isMultiTenancyEnabled: true);

// Authenticated user in tenant
await client.ConnectAuthenticatedAsync(jwtToken, "acme-corp");

// Anonymous user (global notifications only)
await client.ConnectAnonymousAsync();

// Anonymous user in tenant (global + tenant broadcasts)
await client.ConnectAnonymousAsync("acme-corp");
```

---

## Hub Methods (Server-Side)

### Available Hub Methods

These methods are available on the hub instance and can be called from background services or API controllers:

```csharp
public class NotificationHub : Hub
{
    // Send to ALL clients (global broadcast)
    Task SendGlobalNotification(object notification)

    // Send to all clients (single-tenant mode only)
    Task SendToAllClients(object notification)

    // Send to all clients in a tenant (multi-tenant mode)
    Task SendToTenant(string tenantId, object notification)

    // Send to specific user in tenant (multi-tenant mode)
    Task SendToUserInTenant(string tenantId, string userId, object notification)

    // Send to specific user (single-tenant mode)
    Task SendToUser(string userId, object notification)
}
```

### Using Hub Methods from Background Service

```csharp
public class NotificationProcessor : BackgroundService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Example: Send global notification
        await _hubContext.Clients.Group("global")
            .SendAsync("ReceiveNotification", notification);

        // Example: Send to tenant
        await _hubContext.Clients.Group($"tenant:{tenantId}")
            .SendAsync("ReceiveNotification", notification);

        // Example: Send to user in tenant
        await _hubContext.Clients.Group($"tenant:{tenantId}:user:{userId}")
            .SendAsync("ReceiveNotification", notification);

        // Example: Send to user (single-tenant)
        await _hubContext.Clients.Group($"user:{userId}")
            .SendAsync("ReceiveNotification", notification);
    }
}
```

---

## Connection Flow Diagrams

### Multi-Tenancy Mode (`MultiTenancy:Enabled = true`)

```
┌─────────────────────────────────────────────────────────────────┐
│                    CLIENT CONNECTION FLOW                        │
└─────────────────────────────────────────────────────────────────┘

Client → Hub Connection
    ↓
Has JWT Token?
├─ YES (Authenticated)
│   ↓
│   Has x-tenant-id header?
│   ├─ YES
│   │   ↓
│   │   Join Groups:
│   │   • global
│   │   • tenant:{tenantId}
│   │   • tenant:{tenantId}:user:{userId}
│   │   ↓
│   │   ✅ Can receive:
│   │      - Global notifications
│   │      - Tenant-wide notifications
│   │      - User-specific notifications
│   │
│   └─ NO
│       ↓
│       Join Groups:
│       • global
│       • user:{userId}
│       ↓
│       ✅ Can receive:
│          - Global notifications
│          - User-specific notifications (cross-tenant)
│
└─ NO (Anonymous)
    ↓
    Has x-tenant-id header?
    ├─ YES
    │   ↓
    │   Join Groups:
    │   • global
    │   • tenant:{tenantId}
    │   ↓
    │   ✅ Can receive:
    │      - Global notifications
    │      - Tenant-wide notifications
    │
    └─ NO
        ↓
        Join Groups:
        • global
        ↓
        ✅ Can receive:
           - Global notifications only
```

### Single-Tenant Mode (`MultiTenancy:Enabled = false`)

```
┌─────────────────────────────────────────────────────────────────┐
│                    CLIENT CONNECTION FLOW                        │
└─────────────────────────────────────────────────────────────────┘

Client → Hub Connection
    ↓
Has JWT Token?
├─ YES (Authenticated)
│   ↓
│   Join Groups:
│   • global
│   • all-clients
│   • user:{userId}
│   ↓
│   ✅ Can receive:
│      - Global notifications
│      - All-clients broadcasts
│      - User-specific notifications
│
└─ NO (Anonymous)
    ↓
    Join Groups:
    • global
    • all-clients
    ↓
    ✅ Can receive:
       - Global notifications
       - All-clients broadcasts
```

---

## Database Considerations

### Multi-Tenancy Mode (`MultiTenancy:Enabled = true`)

**Two-Database Architecture:**

1. **Global Database** (`NotificationDbContext`)

   - Table: `NotificationQueue`
   - Stores: Queue items for all tenants
   - Purpose: Delivery workflow management

2. **Tenant Databases** (`TenantNotificationDbContext`)
   - Table: `Notifications` (per tenant)
   - Stores: Notification history for each tenant
   - Purpose: Persistent storage, read/unread tracking

**Configuration:**

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=notifications_global;..."
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5003"
  }
}
```

### Single-Tenant Mode (`MultiTenancy:Enabled = false`)

**Single-Database Architecture:**

1. **Shared Database** (`NotificationDbContext` and `TenantNotificationDbContext`)
   - Tables: `NotificationQueue` and `Notifications`
   - Both stored in same database
   - Purpose: Simplified deployment for single-tenant apps

**Configuration:**

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=notifications;..."
  },
  "MultiTenancy": {
    "Enabled": false
  }
}
```

---

## Troubleshooting

### Issue: Anonymous Users Not Receiving Notifications

**Symptom**: Anonymous connections established but notifications not received

**Solution**:

1. Verify connection joins `global` group (check server logs)
2. Ensure notifications sent to correct group:
   - Global: `global` group
   - Tenant: `tenant:{tenantId}` group
3. Check `userId` and `tenantId` are null in queue item for global broadcasts

---

### Issue: Multi-Tenancy Header Required Error

**Symptom**: Warning logged about missing `x-tenant-id` header

**Solution**:

- For authenticated tenant users: Add `x-tenant-id` header to connection
  ```javascript
  connection.headers = { "x-tenant-id": "your-tenant-id" };
  ```
- For global-only notifications: This is expected behavior, connection will still work

---

### Issue: User-Specific Notifications Not Received

**Symptom**: Notifications sent but specific user doesn't receive them

**Checklist**:

1. ✅ User connected with valid JWT token?
2. ✅ UserId extracted from token claims (`ClaimTypes.NameIdentifier`)?
3. ✅ Correct group targeted:
   - Multi-tenancy: `tenant:{tenantId}:user:{userId}`
   - Single-tenant: `user:{userId}`
4. ✅ Notification queue item has correct `userId` and `tenantId`?

---

### Issue: Notifications Delivered to Wrong Tenant

**Symptom**: Users receiving notifications from other tenants

**Root Cause**: Tenant isolation broken

**Solution**:

1. Verify `x-tenant-id` header sent by client
2. Check background processor sets correct tenant context
3. Ensure groups use consistent tenant naming: `tenant:{tenantId}`
4. Review logs for tenant context switching errors

---

## Best Practices

### 1. Always Provide Tenant Context in Multi-Tenancy Mode

```javascript
// ✅ Good - Explicit tenant context
connection.headers = { "x-tenant-id": currentTenantId };

// ❌ Bad - Missing tenant context
connection.headers = {};
```

### 2. Use Appropriate Notification Targeting

```csharp
// ✅ Good - Clear targeting
new NotificationQueueItem
{
    TenantId = "acme-corp",    // Specific tenant
    UserId = 5,                 // Specific user
    Title = "Personal message"
}

// ❌ Bad - Ambiguous targeting (will fallback to global)
new NotificationQueueItem
{
    TenantId = null,
    UserId = 5,  // UserId without TenantId in multi-tenant mode
    Title = "Where does this go?"
}
```

### 3. Handle Connection Lifecycle

```typescript
// ✅ Good - Proper lifecycle management
connection.onclose(() => {
  console.log("Disconnected, attempting reconnect...");
});

connection.onreconnecting(() => {
  console.log("Reconnecting...");
});

connection.onreconnected(() => {
  console.log("Reconnected successfully");
});
```

### 4. Acknowledge Delivery (If Authenticated)

```typescript
// ✅ Good - Acknowledge notifications
connection.on("ReceiveNotification", async (notification) => {
  displayNotification(notification);

  // Acknowledge if authenticated
  if (isAuthenticated) {
    await connection.invoke("AcknowledgeDelivery", notification.queueItemId);
  }
});
```

---

## Summary

### Supported Connection Types

| Mode                                       | JWT Token | x-tenant-id | Groups Joined                           | Can Receive                     |
| ------------------------------------------ | --------- | ----------- | --------------------------------------- | ------------------------------- |
| **Multi-Tenant Authenticated**             | ✅ Yes    | ✅ Yes      | `global`, `tenant:X`, `tenant:X:user:Y` | All notifications               |
| **Multi-Tenant Authenticated (No Tenant)** | ✅ Yes    | ❌ No       | `global`, `user:Y`                      | Global + User-specific          |
| **Multi-Tenant Anonymous**                 | ❌ No     | ✅ Yes      | `global`, `tenant:X`                    | Global + Tenant broadcasts      |
| **Multi-Tenant Anonymous (No Tenant)**     | ❌ No     | ❌ No       | `global`                                | Global only                     |
| **Single-Tenant Authenticated**            | ✅ Yes    | N/A         | `global`, `all-clients`, `user:Y`       | All notifications               |
| **Single-Tenant Anonymous**                | ❌ No     | N/A         | `global`, `all-clients`                 | Global + All-clients broadcasts |

### Notification Targeting Options

| Scenario                        | `tenantId` | `userId` | Multi-Tenant | Single-Tenant | Target Group         |
| ------------------------------- | ---------- | -------- | ------------ | ------------- | -------------------- |
| **Global Broadcast**            | `null`     | `null`   | ✅           | ✅            | `global`             |
| **All Clients (Single-Tenant)** | `null`     | `null`   | ❌           | ✅            | `all-clients`        |
| **Tenant Broadcast**            | `"acme"`   | `null`   | ✅           | ❌            | `tenant:acme`        |
| **User in Tenant**              | `"acme"`   | `5`      | ✅           | ❌            | `tenant:acme:user:5` |
| **User (Single-Tenant)**        | `null`     | `5`      | ❌           | ✅            | `user:5`             |

The notification hub now provides maximum flexibility for all notification scenarios while maintaining security and proper tenant isolation.
