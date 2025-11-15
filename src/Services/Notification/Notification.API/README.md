# 🔔 Notification Service API

Real-time notification system built with ASP.NET Core 9.0, SignalR, and Entity Framework Core.

## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL 16+
- Visual Studio 2022 / VS Code / Rider

### Running the Service

```bash
cd src/Services/Notification/Notification.API
dotnet run
```

The service will start on:

- HTTP: `http://localhost:5004`
- HTTPS: `https://localhost:5104`

### Access Points

- **Swagger UI**: `https://localhost:5104/swagger`
- **SignalR Hub**: `https://localhost:5104/hubs/notifications`
- **Health Check**: `https://localhost:5104/health`

## 📋 Features

- ✅ **Real-Time Delivery**: SignalR WebSocket push notifications
- ✅ **Queue-Based Processing**: Reliable delivery with retry mechanism
- ✅ **Multi-Tenancy**: Optional per-tenant configuration and isolation
- ✅ **Dual Database**: Global queue + tenant-specific persistence
- ✅ **Background Processing**: Automatic queue processing every 5 seconds
- ✅ **Authentication**: JWT Bearer with optional anonymous connections
- ✅ **Flexible Targeting**: Global, tenant-wide, or user-specific notifications
- ✅ **Firebase Support**: Ready for Firebase Cloud Messaging integration
- ✅ **Comprehensive Tests**: 40/41 integration tests passing (98%)

## 🏗️ Architecture

### Dual Database Design

**Global Database (Shared):**

- **Table**: `NotificationQueue`
- **Purpose**: Central queue for all notification delivery workflow
- **Scope**: Cross-tenant, shared queue management

**Tenant Databases (Per-Tenant):**

- **Table**: `Notifications`
- **Purpose**: Persistent notification history per tenant
- **Scope**: Tenant-isolated, separate database per tenant

### Clean Architecture Layers

```
Notification.API          → HTTP/SignalR endpoints, middleware
Notification.Application  → Commands, queries, DTOs, validators
Notification.Infrastructure → Handlers, repositories, background services
Notification.Domain       → Entities, enums, interfaces
```

## 📡 API Endpoints

### Send Notification

```http
POST /api/notifications/send
Authorization: Bearer {token}
x-tenant-id: {tenantId}
Content-Type: application/json

{
  "tenantId": "tenant-123",
  "userId": 5,
  "title": "New Message",
  "message": "You have a new message",
  "data": "{\"messageId\": 42}",
  "deliveryType": "Both",
  "priority": "Immediate"
}
```

### Get User Notifications

```http
GET /api/notifications/user/{userId}?unreadOnly=true&skip=0&take=20
Authorization: Bearer {token}
x-tenant-id: {tenantId}
```

### Mark as Read

```http
PUT /api/notifications/{id}/read
Authorization: Bearer {token}
x-tenant-id: {tenantId}
```

### Get Queue Status

```http
GET /api/notifications/status/{queueItemId}
Authorization: Bearer {token}
x-tenant-id: {tenantId}
```

### Acknowledge Delivery

```http
POST /api/notifications/received
Authorization: Bearer {token}
x-tenant-id: {tenantId}
Content-Type: application/json

{
  "queueItemId": 123
}
```

## 🔌 SignalR Hub

### Connect to Hub

**JavaScript/TypeScript:**

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:5104/hubs/notifications", {
    accessTokenFactory: () => jwtToken,
  })
  .withAutomaticReconnect()
  .build();

// Add tenant header if multi-tenancy is enabled
connection.headers = { "x-tenant-id": "tenant-123" };

await connection.start();
console.log("Connected to notification hub");
```

### Receive Notifications

```javascript
connection.on("ReceiveNotification", (notification) => {
  console.log("New notification:", notification);

  // Display to user
  showToast(notification.title, notification.message);

  // Acknowledge receipt
  connection.invoke("AcknowledgeDelivery", notification.queueItemId);
});
```

### Hub Methods

- `AcknowledgeDelivery(int queueItemId)` - Confirm notification receipt

## ⚙️ Configuration

### appsettings.json

```json
{
  "Urls": "http://localhost:5004;https://localhost:5104",

  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  },

  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=notification_global;..."
  },

  "Jwt": {
    "Secret": "your-secret-key-minimum-32-characters",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp",
    "ExpiryInMinutes": 21600
  },

  "SignalR": {
    "EnableDetailedErrors": true,
    "ClientTimeoutInterval": "00:01:00",
    "KeepAliveInterval": "00:00:15"
  },

  "NotificationProcessing": {
    "WaitableBatchSize": 100,
    "ImmediateBatchSize": 50,
    "ProcessingIntervalSeconds": 5,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 30,
    "CleanupIntervalHours": 1,
    "ExpiredNotificationRetentionDays": 7
  },

  "Firebase": {
    "Enabled": false,
    "ProjectId": "",
    "ServiceAccountKeyPath": ""
  }
}
```

## 🎯 Targeting Scenarios

### 1. Global Notification (Everyone)

```json
{
  "tenantId": null,
  "userId": null,
  "title": "System Maintenance",
  "message": "Scheduled maintenance at 3 PM"
}
```

**Receives**: All connected clients

### 2. Tenant Broadcast

```json
{
  "tenantId": "acme-corp",
  "userId": null,
  "title": "Team Meeting",
  "message": "Team meeting in 30 minutes"
}
```

**Receives**: All users in tenant "acme-corp"

### 3. User-Specific in Tenant

```json
{
  "tenantId": "acme-corp",
  "userId": 5,
  "title": "Task Assigned",
  "message": "You've been assigned a new task"
}
```

**Receives**: User 5 in tenant "acme-corp" only

## 🔐 Authentication

### JWT Token Required

- User-specific notifications require JWT authentication
- Token must contain `sub` claim (user ID)
- Optional: `tenantId` claim for multi-tenancy

### Anonymous Connections

- Supported for global and tenant broadcasts
- Cannot receive user-specific notifications
- No authentication required

### Multi-Tenancy

- When enabled: `x-tenant-id` header required for all API calls
- When disabled: Single-tenant mode, no header needed
- Swagger and health endpoints always exempt from tenant validation

## 🔄 Background Processing

### Notification Processor

- **Interval**: 5 seconds
- **Batch Size**: 50 immediate, 100 waitable
- **Retry**: Max 3 attempts with exponential backoff
- **Processing Order**: Immediate priority first, then FIFO

### Cleanup Service

- **Interval**: Hourly
- **Retention**: 7 days for expired notifications
- **Scope**: Global queue cleanup

## 🗄️ Database Migrations

### Apply Global Database Migration

```bash
cd src/Services/Notification/Notification.Infrastructure
dotnet ef database update --context NotificationDbContext --startup-project ../Notification.API
```

### Apply Tenant Database Migration

```bash
dotnet ef database update --context TenantNotificationDbContext --startup-project ../Notification.API
```

### Create New Migration

```bash
# Global DB
dotnet ef migrations add MigrationName --context NotificationDbContext --output-dir Migrations/Global

# Tenant DB
dotnet ef migrations add MigrationName --context TenantNotificationDbContext --output-dir Migrations/Tenant
```

## 🧪 Testing

### Run All Tests

```bash
cd src/Services/Notification/Notification.API.Tests
dotnet test
```

### Test Coverage

- **Total Tests**: 41
- **Passing**: 40 (98%)
- **Coverage**: Send, management, queue status, user notifications

### Test Types

- ✅ Integration tests with real databases
- ✅ MediatR pipeline testing (handlers, validators)
- ✅ Dual-database architecture testing
- ✅ Multi-tenancy isolation testing

## 📊 Monitoring

### Health Check

```http
GET /health
```

Response:

```json
{
  "status": "healthy",
  "service": "notification"
}
```

### Logging

- Custom logger with file and console output
- Structured logging with Serilog
- Log path: `Logs/Identity/`

## 🔧 Troubleshooting

### Swagger Not Loading

**Issue**: Multi-tenancy enabled but Swagger requires tenant header

**Solution**: Swagger is now configured to load before tenant middleware. Access at `/swagger`

### SignalR Connection Failed

**Issue**: JWT token not provided or expired

**Solution**: Ensure valid JWT token in `accessTokenFactory` for authenticated connections

### Notifications Not Received

**Issue**: User not in correct SignalR group

**Solution**:

- Check JWT contains correct `sub` claim (user ID)
- Verify `x-tenant-id` header matches notification's `tenantId`
- Confirm connection is active

### Database Connection Error

**Issue**: Cannot connect to PostgreSQL

**Solution**:

- Verify PostgreSQL is running
- Check connection string in appsettings.json
- Ensure database exists or enable auto-migration

## 📚 Documentation

- **Main Guide**: `Doc/NOTIFICATION_SERVICE_README.md`
- **System Flow**: `Doc/NOTIFICATION_SYSTEM_FLOW.md`
- **Hub Guide**: `Doc/NOTIFICATION_HUB_GUIDE.md`
- **Quick Reference**: `Doc/NOTIFICATION_HUB_QUICK_REFERENCE.md`
- **JWT Flow**: `Doc/JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md`

## 🚧 Future Enhancements

- [ ] Firebase Cloud Messaging integration
- [ ] Device token management
- [ ] Notification templates
- [ ] Scheduled notifications
- [ ] Notification preferences per user
- [ ] Push notification analytics

## 📝 Version

- **Current Version**: 1.0.0
- **Last Updated**: November 2025
- **Status**: ✅ Production Ready

---

**Built with ❤️ using ASP.NET Core 9.0**
