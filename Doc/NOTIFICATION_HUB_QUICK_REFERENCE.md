# Notification Hub - Quick Reference

## Connection Options

### Authenticated User (with JWT)

**PerTenant Mode (with tenant):**

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications?tenantId=tenant-123", {
    accessTokenFactory: () => tenantJwtToken, // Tenant-specific JWT
  })
  .build();
await connection.start();
```

**Shared Mode or Global Connection:**

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => globalJwtToken, // Global JWT
  })
  .build();
await connection.start();
```

### Anonymous User (without JWT)

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications?tenantId=tenant-123") // Tenant in query string
  .build();
await connection.start();
```

**Note:**

- In PerTenant mode, include `tenantId` in query string for tenant-specific JWT validation
- Token validation uses tenant-specific secret when tenantId provided, otherwise uses global secret

---

## Sending Notifications

### 1. Global Notification (Everyone)

```json
POST /api/notifications/send
{
  "tenantId": null,
  "userId": null,
  "title": "System Announcement",
  "message": "Maintenance at 3 PM"
}
```

**Receives**: All clients (authenticated + anonymous, all tenants)

---

### 2. All Clients (Single-Tenant Mode)

```json
POST /api/notifications/send
{
  "tenantId": null,
  "userId": null,
  "title": "Company News"
}
```

**Config**: `"MultiTenancy:Enabled": false`  
**Receives**: All clients in single-tenant app

---

### 3. Tenant Broadcast

```json
POST /api/notifications/send
{
  "tenantId": "acme-corp",
  "userId": null,
  "title": "Team Meeting"
}
```

**Config**: `"MultiTenancy:Enabled": true`  
**Receives**: All clients in tenant "acme-corp" (authenticated + anonymous)

---

### 4. User in Tenant

```json
POST /api/notifications/send
{
  "tenantId": "acme-corp",
  "userId": 5,
  "title": "Task Assigned"
}
```

**Config**: `"MultiTenancy:Enabled": true`  
**Required**: Both `tenantId` and `userId` must be provided  
**Receives**: User 5 with JWT in tenant "acme-corp"  
**Validation**: Returns 400 error if `userId` provided without `tenantId` when multi-tenancy enabled

---

### 5. User (Single-Tenant)

```json
POST /api/notifications/send
{
  "tenantId": null,
  "userId": 5,
  "title": "Password Changed"
}
```

**Config**: `"MultiTenancy:Enabled": false`  
**Receives**: User 5 with JWT

---

## SignalR Groups

### Multi-Tenancy Mode (`MultiTenancy:Enabled = true`)

| Connection           | Groups                                  | Can Receive     |
| -------------------- | --------------------------------------- | --------------- |
| **Anon + No Tenant** | `global`                                | Global only     |
| **Anon + Tenant**    | `global`, `tenant:X`                    | Global + Tenant |
| **Auth + No Tenant** | `global`, `user:Y`                      | Global + User   |
| **Auth + Tenant**    | `global`, `tenant:X`, `tenant:X:user:Y` | All types       |

### Single-Tenant Mode (`MultiTenancy:Enabled = false`)

| Connection        | Groups                            | Can Receive          |
| ----------------- | --------------------------------- | -------------------- |
| **Anonymous**     | `global`, `all-clients`           | Global + All-clients |
| **Authenticated** | `global`, `all-clients`, `user:Y` | All types            |

---

## Configuration

```json
{
  "MultiTenancy": {
    "Enabled": true // true = multi-tenant, false = single-tenant
  }
}
```

---

## Client Examples

### Receive Notifications

```typescript
connection.on("ReceiveNotification", (notification) => {
  console.log("📩", notification.title, notification.message);

  // Acknowledge (if authenticated)
  connection.invoke("AcknowledgeDelivery", notification.queueItemId);
});
```

### Error Handling

```typescript
connection.onclose(() => console.log("Disconnected"));
connection.onreconnecting(() => console.log("Reconnecting..."));
connection.onreconnected(() => console.log("Reconnected"));
```

---

## Rules Summary

✅ **Anonymous users** can receive:

- Global notifications
- Tenant broadcasts (if tenant header provided)

❌ **Anonymous users** cannot receive:

- User-specific notifications (no userId available)

✅ **Authenticated users** can receive:

- All notification types

⚠️ **Multi-tenancy mode**:

- Tenant header (`x-tenant-id`) recommended for authenticated users
- Required for tenant-scoped notifications

⚠️ **Single-tenant mode**:

- Tenant header ignored
- All data in same database
