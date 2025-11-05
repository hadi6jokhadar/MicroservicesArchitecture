# Notification System Flow

## Overview

The notification system implements a **two-database architecture** with **background processing** and **real-time delivery** via SignalR and Firebase Cloud Messaging. It follows Clean Architecture principles with proper separation of concerns across Domain, Application, Infrastructure, and API layers.

---

## System Architecture

### Database Architecture

#### 1. Global Database (NotificationDbContext)
- **Table**: `NotificationQueue`
- **Entity**: `NotificationQueueItem`
- **Purpose**: Central queue for managing notification delivery workflow
- **Scope**: Shared across all tenants
- **Location**: Global database server

#### 2. Tenant Databases (TenantNotificationDbContext)
- **Table**: `Notifications`
- **Entity**: `Notification`
- **Purpose**: Persistent storage of notifications per tenant
- **Scope**: Separate database per tenant (multi-tenancy support)
- **Location**: Tenant-specific database servers

---

## Complete Notification Flow

### Phase 1: Notification Request (API Entry Point)

```
Client → API Endpoint → Handler → MediatR Command → Handler → Service → Global DB
```

**Step-by-Step:**

1. **Client sends POST request** to `/api/notifications/send`
   ```json
   {
     "tenantId": "tenant-123",
     "userId": 5,
     "title": "New Message",
     "message": "You have a new message from John",
     "data": "{\"messageId\": 42, \"senderId\": 10}",
     "deliveryType": "Both",
     "priority": "Immediate"
   }
   ```

2. **NotificationApiHandlers.SendNotificationHandler** receives request
   - Validates input via FluentValidation
   - Creates `SendNotificationCommand`
   - Sends to MediatR pipeline

3. **SendNotificationCommandHandler** processes command
   - Delegates to `INotificationService.SendNotificationAsync()`

4. **NotificationService.SendNotificationAsync()** executes:
   - Parses `DeliveryType` enum:
     - `SignalR` - Real-time WebSocket delivery
     - `Firebase` - Push notification via FCM
     - `Both` - Dual delivery for reliability
   - Parses `Priority` enum:
     - `Immediate` - Process ASAP (high priority)
     - `Waitable` - Can be delayed (low priority)
   - Creates `NotificationQueueItem` entity:
     ```csharp
     {
       Id: auto-generated (int),
       TenantId: "tenant-123",
       UserId: 5,
       Title: "New Message",
       Message: "You have a new message from John",
       Data: "{...}",
       DeliveryType: Both,
       Priority: Immediate,
       QueueStatus: Pending,
       ExpiresAt: UtcNow + 24 hours,
       CreatedAt: UtcNow,
       UpdatedAt: UtcNow,
       RetryCount: 0
     }
     ```
   - **Saves to Global DB** (`NotificationQueue` table)
   - Returns `SendNotificationResponse`:
     ```csharp
     {
       QueueItemId: 123,
       Status: "Queued",
       QueuedAt: DateTime.UtcNow,
       Priority: "Immediate",
       DeliveryType: "Both"
     }
     ```

**Result**: Notification queued in global database, ready for background processing.

---

### Phase 2: Background Processing (NotificationProcessor)

```
Background Service (every 5s) → Query Pending → Process → Persist → Deliver → Update Status
```

**NotificationProcessor** is a `BackgroundService` that runs continuously:

#### Processing Loop

1. **Timer triggers every 5 seconds** (configurable via `NotificationProcessing:ProcessingIntervalSeconds`)

2. **Queries global database** for pending notifications:
   ```sql
   SELECT * FROM NotificationQueue
   WHERE QueueStatus = 'Pending' 
     AND ExpiresAt > UtcNow
   ORDER BY Priority ASC,  -- Immediate (1) before Waitable (0)
            CreatedAt ASC  -- FIFO within same priority
   LIMIT 50  -- Batch processing
   ```

3. **For each notification in batch**:

   **Step 1: Mark as Processing**
   ```csharp
   queueItem.QueueStatus = QueueStatus.Processing;
   queueItem.UpdatedAt = DateTime.UtcNow;
   await globalDbContext.SaveChangesAsync();
   ```

   **Step 2: Persist to Tenant Database** ✅
   ```csharp
   // Set tenant context dynamically
   tenantContext.SetTenant(new TenantInfo { 
       TenantId = queueItem.TenantId 
   });
   
   // Create notification entity
   var notification = new Notification
   {
       UserId = queueItem.UserId,
       Title = queueItem.Title,
       Message = queueItem.Message,
       Data = queueItem.Data,
       IsRead = false,
       QueueItemId = queueItem.Id,
       CreatedAt = DateTime.UtcNow
   };
   
   // Save to tenant-specific database
   tenantDbContext.Notifications.Add(notification);
   await tenantDbContext.SaveChangesAsync();
   
   // Link back to queue item
   queueItem.NotificationId = notification.Id;
   ```

   **Step 3: Send via SignalR** ✅
   ```csharp
   // Prepare payload
   var payload = new
   {
       queueItemId = queueItem.Id,
       notificationId = queueItem.NotificationId,
       title = queueItem.Title,
       message = queueItem.Message,
       data = queueItem.Data,
       createdAt = queueItem.CreatedAt,
       priority = queueItem.Priority.ToString()
   };
   
   // Send to specific user or broadcast to tenant
   if (queueItem.UserId.HasValue)
   {
       // Specific user
       var groupName = $"tenant:{tenantId}:user:{userId}";
       await hubContext.Clients.Group(groupName)
           .SendAsync("ReceiveNotification", payload);
   }
   else
   {
       // Broadcast to all users in tenant
       var groupName = $"tenant:{tenantId}";
       await hubContext.Clients.Group(groupName)
           .SendAsync("ReceiveNotification", payload);
   }
   ```

   **Step 4: Send via Firebase** 🚧 (TODO)
   ```csharp
   // TODO: Implement Firebase Cloud Messaging
   // 1. Get device tokens for user from Identity Service
   // 2. Send push notification via Firebase Admin SDK
   // 3. Handle delivery status and token invalidation
   ```

   **Step 5: Mark as Sent**
   ```csharp
   queueItem.QueueStatus = QueueStatus.Sent;
   queueItem.ProcessedAt = DateTime.UtcNow;
   queueItem.UpdatedAt = DateTime.UtcNow;
   await globalDbContext.SaveChangesAsync();
   ```

4. **Error Handling**:
   ```csharp
   catch (Exception ex)
   {
       queueItem.RetryCount++;
       
       if (queueItem.RetryCount >= 3)
       {
           // Failed permanently
           queueItem.QueueStatus = QueueStatus.Failed;
           queueItem.Error = ex.Message;
       }
       else
       {
           // Retry later
           queueItem.QueueStatus = QueueStatus.Pending;
       }
       
       queueItem.UpdatedAt = DateTime.UtcNow;
       await globalDbContext.SaveChangesAsync();
   }
   ```

---

### Phase 3: Real-Time Delivery (SignalR Hub)

```
Client Connects → Authentication → Join Groups → Receive Notifications → Acknowledge
```

#### Connection Flow

**1. Client Connects to SignalR Hub**

```javascript
// JavaScript/TypeScript client
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
        accessTokenFactory: () => jwtToken  // Required!
    })
    .build();

// Add tenant header
connection.headers = { "x-tenant-id": "tenant-123" };

await connection.start();
```

**2. Server: OnConnectedAsync() Executes**

```csharp
[Authorize]  // Required authentication
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Extract JWT claims
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (userId == null)
        {
            Context.Abort();  // Reject unauthenticated connections
            return;
        }
        
        // Extract tenant from header
        var tenantId = Context.GetHttpContext()
            ?.Request.Headers["x-tenant-id"].FirstOrDefault();
        
        // Add to SignalR groups
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            // Tenant-specific user group
            await Groups.AddToGroupAsync(
                Context.ConnectionId, 
                $"tenant:{tenantId}:user:{userId}"
            );
            
            // Tenant broadcast group
            await Groups.AddToGroupAsync(
                Context.ConnectionId, 
                $"tenant:{tenantId}"
            );
        }
        else
        {
            // Global user group (no tenant)
            await Groups.AddToGroupAsync(
                Context.ConnectionId, 
                $"user:{userId}"
            );
        }
        
        await base.OnConnectedAsync();
    }
}
```

**3. Client Receives Notifications**

```javascript
// Subscribe to notification events
connection.on("ReceiveNotification", (notification) => {
    console.log("New notification:", notification);
    
    // Display notification to user
    showNotification(notification.title, notification.message);
    
    // Acknowledge receipt
    connection.invoke("AcknowledgeDelivery", notification.queueItemId)
        .catch(err => console.error("Ack failed:", err));
});
```

**4. Server Delivers to Groups**

```csharp
// Background processor sends to hub
await hubContext.Clients
    .Group($"tenant:{tenantId}:user:{userId}")
    .SendAsync("ReceiveNotification", payload);
```

#### SignalR Group Strategy

| Group Pattern | Purpose | Example |
|--------------|---------|---------|
| `tenant:{tenantId}` | Broadcast to all users in tenant | `tenant:acme-corp` |
| `tenant:{tenantId}:user:{userId}` | Specific user in tenant | `tenant:acme-corp:user:5` |
| `user:{userId}` | Global user (no tenant) | `user:5` |

---

### Phase 4: Client Acknowledgment

```
Client Receives → Acknowledges via SignalR → Command → Service → Update Global DB
```

**Step-by-Step:**

1. **Client acknowledges delivery**:
   ```javascript
   await connection.invoke("AcknowledgeDelivery", queueItemId);
   ```

2. **NotificationHub.AcknowledgeDelivery(int queueItemId)** executes:
   ```csharp
   public async Task AcknowledgeDelivery(int queueItemId)
   {
       var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
       
       var command = new AcknowledgeNotificationCommand
       {
           QueueItemId = queueItemId,
           ConnectionId = Context.ConnectionId,
           ReceivedAt = DateTime.UtcNow
       };
       
       var success = await _mediator.Send(command);
   }
   ```

3. **AcknowledgeNotificationCommandHandler** processes:
   - Delegates to `INotificationService.AcknowledgeDeliveryAsync()`

4. **NotificationService.AcknowledgeDeliveryAsync()** executes:
   ```csharp
   var queueItem = await globalDbContext.NotificationQueue
       .FirstOrDefaultAsync(q => q.Id == queueItemId);
   
   queueItem.QueueStatus = QueueStatus.Sent;
   queueItem.UpdatedAt = DateTime.UtcNow;
   
   await globalDbContext.SaveChangesAsync();
   ```

**Result**: Queue item confirmed as successfully delivered.

---

## Additional API Operations

### 1. Get Queue Status

**Endpoint**: `GET /api/notifications/status/{id}`

**Purpose**: Check status of a queued notification

**Response**:
```json
{
  "queueItemId": 123,
  "status": "Sent",
  "retryCount": 0,
  "createdAt": "2025-11-05T10:30:00Z",
  "processedAt": "2025-11-05T10:30:05Z",
  "error": null
}
```

---

### 2. Get User Notifications

**Endpoint**: `GET /api/notifications/user/{userId}`

**Purpose**: Retrieve notification history for a user (from tenant DB)

**Response**:
```json
[
  {
    "id": 456,
    "userId": 5,
    "title": "New Message",
    "message": "You have a new message from John",
    "isRead": false,
    "createdAt": "2025-11-05T10:30:00Z",
    "readAt": null
  }
]
```

**Notes**:
- Returns last 50 notifications
- Ordered by most recent first
- Reads from tenant-specific database

---

### 3. Mark as Read

**Endpoint**: `PUT /api/notifications/{id}/read`

**Purpose**: Mark a notification as read in tenant database

**Behavior**:
```csharp
notification.IsRead = true;
notification.ReadAt = DateTime.UtcNow;
notification.UpdatedAt = DateTime.UtcNow;
await tenantDbContext.SaveChangesAsync();
```

---

## Security

### Authentication Requirements

✅ **All endpoints require JWT authentication**
- API endpoints: `[RequireAuthorization]` on group
- SignalR hub: `[Authorize]` attribute + `.RequireAuthorization()` on endpoint
- JWT token validation with per-tenant support

### JWT Token Flow

**HTTP API Requests**:
```http
Authorization: Bearer <jwt-token>
x-tenant-id: tenant-123
```

**SignalR WebSocket Connections**:
```javascript
// Option 1: Query string (for WebSocket upgrade)
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications?access_token=" + jwtToken)
    .build();

// Option 2: Access token factory (recommended)
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
        accessTokenFactory: () => getJwtToken()
    })
    .build();
```

**Server Configuration**:
```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        
        if (!string.IsNullOrEmpty(accessToken) && 
            path.StartsWithSegments("/hubs/notifications"))
        {
            context.Token = accessToken;
        }
        
        return Task.CompletedTask;
    }
};
```

### Tenant Isolation

- SignalR groups: `tenant:{tenantId}:user:{userId}`
- Tenant-specific databases via `TenantNotificationDbContext`
- Dynamic tenant context in background processor
- Tenant header validation in hub connection

---

## Key Design Patterns

### 1. Two-Database Pattern

**Global Database (Workflow)**:
- ✅ Transient queue for delivery management
- ✅ Cross-tenant visibility for processing
- ✅ Retry logic and error tracking
- ✅ Status monitoring

**Tenant Databases (History)**:
- ✅ Persistent notification storage
- ✅ User notification history
- ✅ Read/unread tracking
- ✅ Multi-tenancy data isolation

### 2. Background Processing

- ✅ Decouples API response from delivery
- ✅ Handles retries automatically (3 attempts)
- ✅ Batched processing (50 notifications at a time)
- ✅ Configurable processing interval (default 5 seconds)

### 3. Priority Queue

- ✅ `Immediate` (priority=1) processed before `Waitable` (priority=0)
- ✅ FIFO within same priority level
- ✅ Expired notifications automatically skipped

### 4. Multi-Tenancy Support

- ✅ Tenant-specific SignalR groups
- ✅ Tenant-specific databases for history
- ✅ Optional non-tenant notifications (global users)
- ✅ Dynamic tenant context switching

### 5. Delivery Channels

**SignalR** ✅:
- Real-time WebSocket delivery
- For web and mobile apps with active connections
- Immediate notification display

**Firebase** 🚧 (TODO):
- Push notifications for mobile apps
- Works when app is in background/closed
- Device token management required

**Both** ✅:
- Dual delivery for reliability
- SignalR for active users + Firebase for offline users

### 6. Error Handling

- ✅ Automatic retries (up to 3 attempts)
- ✅ Exponential backoff via processing interval
- ✅ Error logging with full context
- ✅ Failed notifications marked with error message
- ✅ Expiration mechanism (24 hours default)

---

## Status Flow Diagram

```
┌──────────┐
│ Pending  │ ◄─── Initial state when notification queued
└────┬─────┘
     │
     ▼
┌──────────┐
│Processing│ ◄─── Background service picks up notification
└────┬─────┘
     │
     ├─────► Success ────► ┌──────┐
     │                      │ Sent │ ◄─── Delivered successfully
     │                      └──────┘
     │
     ├─────► Retry ────────► Back to Pending (if RetryCount < 3)
     │
     ├─────► Max Retries ──► ┌────────┐
     │                        │ Failed │ ◄─── Permanent failure
     │                        └────────┘
     │
     └─────► Timeout ───────► ┌─────────┐
                               │ Expired │ ◄─── Past ExpiresAt
                               └─────────┘
```

---

## Configuration

### appsettings.json

```json
{
  "NotificationProcessing": {
    "ProcessingIntervalSeconds": 5
  },
  "SignalR": {
    "EnableDetailedErrors": false,
    "ClientTimeoutInterval": "00:01:00",
    "KeepAliveInterval": "00:00:15"
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=NotificationQueue;..."
  },
  "Jwt": {
    "Secret": "your-secret-key",
    "Issuer": "your-issuer",
    "Audience": "your-audience"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:4200"]
  }
}
```

---

## Implementation Status

### ✅ Fully Implemented

- [x] Queue management in global database
- [x] Tenant-specific persistence
- [x] SignalR real-time delivery
- [x] User and tenant groups
- [x] JWT authentication enforcement
- [x] Background processing with retry logic
- [x] Priority queue ordering
- [x] Expiration handling
- [x] Error tracking and logging
- [x] Multi-tenancy support
- [x] Clean Architecture structure
- [x] CQRS with Commands (no Queries folder)
- [x] Integer IDs throughout system

### 🚧 TODO (Future Enhancements)

- [ ] Firebase Cloud Messaging integration
- [ ] Device token management (integrate with Identity Service)
- [ ] Delivery receipts and read tracking
- [ ] Rich notifications (images, actions, deep links)
- [ ] Notification templates
- [ ] Scheduled notifications
- [ ] Notification preferences per user
- [ ] Analytics and reporting

---

## Client Integration Examples

### JavaScript/TypeScript (Web)

```typescript
import * as signalR from "@microsoft/signalr";

class NotificationService {
    private connection: signalR.HubConnection;

    async connect(token: string, tenantId?: string) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/notifications", {
                accessTokenFactory: () => token
            })
            .withAutomaticReconnect()
            .build();

        // Add tenant header if provided
        if (tenantId) {
            this.connection.headers = { "x-tenant-id": tenantId };
        }

        // Subscribe to notifications
        this.connection.on("ReceiveNotification", (notification) => {
            this.handleNotification(notification);
            this.acknowledgeDelivery(notification.queueItemId);
        });

        await this.connection.start();
        console.log("Connected to notification hub");
    }

    private handleNotification(notification: any) {
        // Display notification to user
        if (Notification.permission === "granted") {
            new Notification(notification.title, {
                body: notification.message,
                icon: "/notification-icon.png"
            });
        }
    }

    private async acknowledgeDelivery(queueItemId: number) {
        try {
            await this.connection.invoke("AcknowledgeDelivery", queueItemId);
        } catch (err) {
            console.error("Failed to acknowledge notification:", err);
        }
    }

    async disconnect() {
        await this.connection.stop();
    }
}
```

### C# (Xamarin/MAUI)

```csharp
public class NotificationService
{
    private HubConnection _connection;

    public async Task ConnectAsync(string token, string tenantId = null)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl("https://api.example.com/hubs/notifications", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token);
                
                if (!string.IsNullOrEmpty(tenantId))
                {
                    options.Headers.Add("x-tenant-id", tenantId);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<NotificationPayload>("ReceiveNotification", notification =>
        {
            HandleNotification(notification);
            _ = AcknowledgeDeliveryAsync(notification.QueueItemId);
        });

        await _connection.StartAsync();
    }

    private void HandleNotification(NotificationPayload notification)
    {
        // Display local notification
        Device.BeginInvokeOnMainThread(() =>
        {
            // Show notification UI
        });
    }

    private async Task AcknowledgeDeliveryAsync(int queueItemId)
    {
        try
        {
            await _connection.InvokeAsync("AcknowledgeDelivery", queueItemId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to acknowledge: {ex.Message}");
        }
    }
}
```

---

## Troubleshooting

### Common Issues

**1. SignalR Connection Fails**
- ✅ Ensure JWT token is valid and not expired
- ✅ Check CORS configuration allows SignalR origin
- ✅ Verify `.RequireAuthorization()` is set on hub endpoint
- ✅ Check client uses `accessTokenFactory` or query string token

**2. Notifications Not Being Delivered**
- ✅ Check `NotificationProcessor` background service is running
- ✅ Verify queue items are in `Pending` status (not `Failed` or `Expired`)
- ✅ Check logs for processing errors
- ✅ Ensure client is connected to correct SignalR group

**3. Tenant Isolation Not Working**
- ✅ Verify `x-tenant-id` header is sent with requests
- ✅ Check tenant context is set correctly in background processor
- ✅ Ensure `TenantNotificationDbContext` resolves correct connection string

**4. Performance Issues**
- ✅ Adjust `ProcessingIntervalSeconds` (increase for lower load)
- ✅ Reduce batch size in processor (currently 50)
- ✅ Add database indexes on `QueueStatus`, `ExpiresAt`, `Priority`, `CreatedAt`
- ✅ Scale horizontally with multiple processor instances

---

## Monitoring and Logging

### Key Metrics to Track

- Queue depth (pending notifications)
- Processing latency (queue time to delivery)
- Delivery success rate
- Retry rate
- Failed notification count
- SignalR connection count
- Active user groups

### Log Levels

```csharp
// Information: Normal flow
_logger.LogInformation("Processing {Count} pending notifications", pendingItems.Count);

// Debug: Detailed tracking
_logger.LogDebug("SignalR notification sent to group: {GroupName}", groupName);

// Warning: Potential issues
_logger.LogWarning("Notification {QueueItemId} retry {RetryCount}/3", queueItemId, retryCount);

// Error: Failures requiring attention
_logger.LogError(ex, "Error processing notification {QueueItemId}", queueItemId);
```

---

## Summary

The notification system provides a **robust, scalable, and secure** solution for delivering real-time notifications in a multi-tenant environment. Key features include:

- ✅ **Two-database architecture** for workflow management and persistent storage
- ✅ **Background processing** with automatic retries and error handling
- ✅ **Real-time delivery** via SignalR with authentication enforcement
- ✅ **Multi-tenancy support** with proper data isolation
- ✅ **Priority queue** for immediate vs. waitable notifications
- ✅ **Clean Architecture** with proper separation of concerns
- ✅ **CQRS pattern** with Commands-only approach
- ✅ **Extensible design** ready for Firebase and other delivery channels

The system is production-ready for SignalR delivery, with Firebase integration planned for future releases.
