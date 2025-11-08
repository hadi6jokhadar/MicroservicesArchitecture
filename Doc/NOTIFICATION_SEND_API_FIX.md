# Notification Send API - Tenant ID Configuration

**Date:** November 8, 2025  
**Issue:** Send API should use tenantId from request body, not header  
**Status:** ✅ Configured

---

## Configuration

The `/api/notifications/send` endpoint is configured to **bypass tenant middleware** because:

1. The `tenantId` is provided in the **request body** (not header)
2. The endpoint only writes to the **global queue database** (no tenant-specific DB needed)
3. This simplifies service-to-service communication (no need for x-tenant-id header)

### Endpoint Configuration

```csharp
// Send endpoint configuration
notificationGroup.MapPost("/send", NotificationApiHandlers.SendNotificationHandler)
    .WithName("SendNotification")
    .WithSummary("Send a notification")
    .WithDescription("Queue a notification for delivery to a user via SignalR or Firebase. TenantId should be provided in the request body.")
    .WithMetadata(new BypassTenantAttribute()) // ✅ Bypasses tenant middleware
    .Produces<SendNotificationResponse>(200)
    .ProducesValidationProblem();
```

### Why BypassTenantAttribute?

1. **TenantId comes from request body**, not header
2. **Only writes to global queue database** - no tenant-specific DB access needed during send
3. **Simplifies service-to-service calls** - no need to add x-tenant-id header
4. **Background processor handles tenant resolution** - when processing the queue, it reads tenantId from the queue item

---

## How It Works

### Flow Diagram

```
Client/Service Request
    ↓
POST /api/notifications/send
{
  "tenantId": "ihsandev",    ← TenantId in body
  "userId": 1,
  "title": "Test",
  "message": "Message"
}
    ↓
TenantMiddleware SKIPPED (BypassTenantAttribute)
    ↓
JWT Validation (global JWT from appsettings.json)
    ↓
Handler → NotificationService.SendNotificationAsync()
    ↓
Save to Global Queue Database
{
  QueueItemId: 123,
  TenantId: "ihsandev",     ← Stored in queue
  UserId: 1,
  Status: Pending
}
    ↓
Background Processor (later)
    ├─ Reads queue item
    ├─ Extracts tenantId from queue item
    ├─ Fetches tenant configuration
    ├─ Connects to tenant-specific database
    └─ Delivers notification
    ↓
Success! ✅
```

## Correct Endpoint Configuration

### Send Endpoint (NO x-tenant-id required)

| Endpoint                  | Method | TenantId Source  | Requires x-tenant-id |
| ------------------------- | ------ | ---------------- | -------------------- |
| `/api/notifications/send` | POST   | **Request body** | ❌ No                |

### Other User Endpoints (REQUIRE x-tenant-id)

These endpoints access tenant-specific databases and **require** the `x-tenant-id` header:

| Endpoint                           | Method | Purpose                | Requires x-tenant-id |
| ---------------------------------- | ------ | ---------------------- | -------------------- |
| `/api/notifications/status/{id}`   | GET    | Get queue status       | ✅ Yes               |
| `/api/notifications/user/{userId}` | GET    | Get user notifications | ✅ Yes               |
| `/api/notifications/{id}/read`     | PUT    | Mark as read           | ✅ Yes               |

### Admin Endpoints (NO x-tenant-id required)

| Endpoint                         | Method | Purpose             | Requires x-tenant-id |
| -------------------------------- | ------ | ------------------- | -------------------- |
| `/api/notifications/admin/queue` | GET    | Get all queue items | ❌ No                |

---

## How to Use the Send API

### Request Example (NO x-tenant-id header needed)

```bash
curl -X POST "https://localhost:5104/api/notifications/send" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "tenantId": "ihsandev",
    "userId": 1,
    "title": "Test Notification",
    "message": "This is a test message",
    "deliveryType": "Both",
    "priority": "Immediate"
  }'
```

### Required Headers

1. **Authorization**: Bearer token with valid JWT
2. **Content-Type**: application/json

**Note:** No `x-tenant-id` header required! The `tenantId` comes from the request body.

### Success Response

```json
{
  "queueItemId": 123,
  "status": "Pending"
}
```

---

## Authentication Flow

### Send Endpoint (Bypasses Tenant Middleware)

```
Client Request
    ↓
Contains: JWT token + tenantId in body
    ↓
TenantMiddleware (SKIPPED because of BypassTenantAttribute)
    ↓
JWT Validation (uses global JWT from appsettings.json)
    ↓
Handler executes - saves to global queue with tenantId from body ✅
    ↓
Success!
```

### Other User Endpoints (Require x-tenant-id header)

```
Client Request
    ↓
Contains: x-tenant-id header + JWT token
    ↓
TenantMiddleware (runs - NO BypassTenantAttribute)
    ├─ Extract tenant ID from header
    ├─ Fetch tenant configuration from Tenant Service
    └─ Set tenant context with database settings
    ↓
JWT Validation (uses tenant-specific JWT if JwtMode=PerTenant)
    ↓
Handler executes with full tenant context ✅
    ↓
Success!
```

### Admin Endpoints (Queue Management)

```
Client Request
    ↓
Contains: JWT token (NO x-tenant-id header)
    ↓
TenantMiddleware (SKIPPED because of BypassTenantAttribute)
    ↓
JWT Validation (always uses global JWT from appsettings.json)
    ↓
Authorization check (requires SuperAdmin role)
    ↓
Handler executes without tenant context ✅
    ↓
Success!
```

---

## Testing

### Test 1: Send Notification (NO x-tenant-id header)

```bash
curl -X POST "https://localhost:5104/api/notifications/send" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "tenantId": "ihsandev",
    "userId": 1,
    "title": "Test",
    "message": "Test message",
    "deliveryType": "Both",
    "priority": "Immediate"
  }'
```

**Expected:** ✅ 200 OK with queue item ID

### Test 2: Get User Notifications (x-tenant-id header required)

```bash
curl "https://localhost:5104/api/notifications/user/1" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "x-tenant-id: ihsandev"
```

**Expected:** ✅ 200 OK with notification list

### Test 3: Admin Queue Endpoint (SuperAdmin)

```bash
curl "https://localhost:5104/api/notifications/admin/queue?pageSize=10" \
  -H "Authorization: Bearer SUPERADMIN_TOKEN"
```

**Expected:** ✅ 200 OK with paginated queue items (no x-tenant-id needed)

---

## Related Documentation

- **Notification Service README**: [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md)
- **Notification System Flow**: [NOTIFICATION_SYSTEM_FLOW.md](NOTIFICATION_SYSTEM_FLOW.md)
- **Service Integration Guide**: [SERVICE_TO_NOTIFICATION_INTEGRATION_GUIDE.md](SERVICE_TO_NOTIFICATION_INTEGRATION_GUIDE.md)
- **Multi-Tenancy Guide**: [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md)

---

## Key Takeaways

1. ✅ **Send API does NOT require x-tenant-id header** - tenantId comes from request body
2. ✅ Send endpoint uses `BypassTenantAttribute` to skip tenant middleware
3. ✅ Send endpoint only writes to global queue database (no tenant DB needed)
4. ✅ Background processor handles tenant resolution when processing queue items
5. ✅ Other user endpoints (status, user notifications, mark as read) still require x-tenant-id header
6. ✅ This design simplifies service-to-service communication

---

**Status:** Configured correctly - send endpoint bypasses tenant middleware and uses tenantId from body
