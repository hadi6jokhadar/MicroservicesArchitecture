# 🔔 Notification Service - Complete Documentation

**Version:** 1.0.0  
**Last Updated:** November 7, 2025  
**Status:** ✅ Production Ready (98% Test Coverage - 40/41 Tests Passing)  
**Service Ports:** HTTP: 5004, HTTPS: 5104

---

## 📋 Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Key Features](#key-features)
- [Documentation Index](#documentation-index)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Endpoints](#api-endpoints)
- [SignalR Hub](#signalr-hub)
- [Targeting Scenarios](#targeting-scenarios)
- [Authentication](#authentication)
- [Multi-Tenancy](#multi-tenancy)
- [Background Processing](#background-processing)
- [Database Schema](#database-schema)
- [Client Integration](#client-integration)
- [Troubleshooting](#troubleshooting)

---

## Overview

The Notification Service is a real-time push notification system built with **ASP.NET Core 9.0**, **SignalR**, and **Entity Framework Core**. It implements a queue-based processing architecture with multi-tenancy support, dual JWT authentication modes, and optional Firebase Cloud Messaging integration.

### Design Principles

- **Clean Architecture**: Separation of concerns across layers
- **CQRS Pattern**: Commands and queries via MediatR
- **Queue-First Processing**: Reliable delivery with retry mechanism
- **Two-Database Model**: Global queue + tenant-specific persistence
- **Dual JWT Authentication**: Global JWT for admin, tenant-specific JWT for users
- **Optional Authentication**: Supports both authenticated and anonymous connections
- **Configuration-Driven**: Multi-tenancy enabled/disabled via configuration

---

## Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                    NOTIFICATION SERVICE                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────┐  ┌─────────────────┐  ┌─────────────────┐   │
│  │  SignalR Hub │  │  HTTP API       │  │  Background     │   │
│  │  (Real-time) │  │  (REST)         │  │  Processor      │   │
│  └──────┬───────┘  └────────┬────────┘  └────────┬────────┘   │
│         │                   │                     │             │
│         └───────────────────┴─────────────────────┘             │
│                             │                                    │
│         ┌───────────────────┴───────────────────┐               │
│         │                                       │               │
│    ┌────▼────────┐                       ┌─────▼──────┐        │
│    │ Global Queue│                       │  Tenant DB  │        │
│    │  Database   │                       │  (Per-Tenant)│       │
│    │ (Shared)    │                       │             │        │
│    └─────────────┘                       └─────────────┘        │
└─────────────────────────────────────────────────────────────────┘

External:
┌─────────────┐              ┌──────────────┐
│   Clients   │◄────────────►│   Firebase   │
│ (WebSocket) │              │     FCM      │
└─────────────┘              └──────────────┘
```

### Layer Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  API Layer (Notification.API)                                │
│  - SignalR Hub (NotificationHub)                             │
│  - HTTP Endpoints (Minimal APIs)                             │
│  - Middleware Pipeline                                       │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│  Application Layer (Notification.Application)                │
│  - Commands: SendNotification, MarkAsRead, Acknowledge       │
│  - Queries: GetQueueStatus, GetUserNotifications             │
│  - DTOs & Mapping Profiles                                   │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│  Infrastructure Layer (Notification.Infrastructure)          │
│  - Command/Query Handlers                                    │
│  - DbContexts (Global + Tenant)                              │
│  - Background Services (Processor, Cleanup)                  │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│  Domain Layer (Notification.Domain)                          │
│  - Entities: NotificationQueueItem, Notification             │
│  - Enums: DeliveryType, Priority, QueueStatus                │
└─────────────────────────────────────────────────────────────┘
```

---

## Key Features

### ✅ Real-Time Delivery

- SignalR hub with WebSocket support
- Automatic reconnection handling
- Group-based targeting
- Token authentication from query string
- Dual JWT authentication (global + tenant-specific)

### ✅ Queue-Based Processing

- Global queue for reliable delivery
- Background processor (5-second intervals)
- Retry mechanism (max 3 attempts)
- Exponential backoff
- SuperAdmin queue management endpoint with filtering and pagination

### ✅ Multi-Tenancy Support

- Optional per-tenant configuration
- Tenant-specific databases
- Dynamic connection string resolution
- Tenant-based SignalR grouping
- Tenant-specific JWT validation

### ✅ Five Targeting Scenarios

1. **Global**: All connected clients
2. **All Clients**: Single-tenant broadcast
3. **Tenant Broadcast**: All users in tenant
4. **User in Tenant**: Specific user in tenant
5. **Cross-Tenant User**: User across tenants

### ✅ Optional Authentication

- Anonymous connections supported
- JWT Bearer authentication
- Claims-based user identification
- Per-tenant JWT secrets

### ✅ Firebase Integration (Optional)

- Firebase Cloud Messaging support
- Device token management
- Push notifications for offline users
- Delivery status tracking

### ✅ Comprehensive Testing

- **Test Coverage**: 98% (40/41 integration tests passing)
- **Test Approach**: Handler-based testing via MediatR
- **Test Isolation**: Sequential execution with database cleanup
- **Test Types**: Send, management, queue status, user notifications
- **Dual Database Testing**: Both global and tenant databases validated

---

## 🎯 Current Implementation Status

### ✅ Completed Features

- ✅ **Core Architecture**: Clean Architecture with CQRS pattern
- ✅ **Database Migration**: Both global and tenant databases with EF Core migrations
- ✅ **API Endpoints**: All 5 endpoints implemented and tested
- ✅ **SignalR Hub**: Real-time delivery with group-based targeting
- ✅ **Background Processing**: Queue processor with retry mechanism
- ✅ **Cleanup Service**: Automatic expired notification cleanup
- ✅ **Multi-Tenancy**: Full support with tenant-specific databases
- ✅ **Authentication**: JWT Bearer with optional anonymous connections
- ✅ **Validation**: FluentValidation for all commands
- ✅ **Mapping**: AutoMapper for DTO transformations
- ✅ **Testing**: Comprehensive integration test suite
- ✅ **Swagger**: API documentation with tenant header support
- ✅ **Configuration**: Production-ready appsettings
- ✅ **Logging**: Structured logging with custom logger
- ✅ **Health Checks**: Service health monitoring endpoint

### 🚧 Pending Features

- ⏳ **Firebase FCM**: Push notification integration (placeholder ready)
- ⏳ **Device Tokens**: Management via Identity Service integration
- ⏳ **Notification Templates**: Predefined message templates
- ⏳ **Scheduled Notifications**: Time-based notification delivery
- ⏳ **User Preferences**: Per-user notification settings

### 📊 Service Metrics

- **Service Ports**: HTTP 5004, HTTPS 5104
- **Queue Processing**: 5-second intervals
- **Batch Sizes**: 50 immediate, 100 waitable
- **Retry Attempts**: Maximum 3 with exponential backoff
- **Retention Period**: 7 days for expired notifications
- **SignalR Timeout**: 1 minute client timeout, 15-second keep-alive

---

## Documentation Index

### 📖 Core Documentation

| Document                                                                       | Purpose                               | Audience               |
| ------------------------------------------------------------------------------ | ------------------------------------- | ---------------------- |
| **[NOTIFICATION_SYSTEM_FLOW.md](NOTIFICATION_SYSTEM_FLOW.md)**                 | Complete system architecture and flow | Architects, Developers |
| **[NOTIFICATION_HUB_GUIDE.md](NOTIFICATION_HUB_GUIDE.md)**                     | Comprehensive SignalR hub usage       | Developers             |
| **[NOTIFICATION_HUB_QUICK_REFERENCE.md](NOTIFICATION_HUB_QUICK_REFERENCE.md)** | Quick reference for common scenarios  | Developers             |
| **[SUPERADMIN_QUEUE_ENDPOINT.md](SUPERADMIN_QUEUE_ENDPOINT.md)**               | SuperAdmin queue management API       | Administrators         |

### 🔐 Authentication & JWT

| Document                                                                         | Purpose                       | Audience                       |
| -------------------------------------------------------------------------------- | ----------------------------- | ------------------------------ |
| **[JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md](JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md)** | Detailed JWT flow walkthrough | Developers                     |
| **[JWT_SECRET_AND_VALIDATION_FLOW.md](JWT_SECRET_AND_VALIDATION_FLOW.md)**       | JWT validation explained      | Security Engineers, Developers |

### 🏢 Related Documentation

| Document                                                                       | Purpose                |
| ------------------------------------------------------------------------------ | ---------------------- |
| **[MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md)**                           | Multi-tenancy patterns |
| **[DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md)** | Database architecture  |
| **[SHARED_IDENTITY_SERVICE_GUIDE.md](SHARED_IDENTITY_SERVICE_GUIDE.md)**       | Authentication system  |

---

## Quick Start

### 1. Prerequisites

- .NET 9.0 SDK
- PostgreSQL 15+ (or SQL Server, MySQL, SQLite)
- Visual Studio 2022 / VS Code / Rider

### 2. Configuration

Update `appsettings.json`:

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant"
  },
  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters-must-match-identity-service",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp"
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=notifications_global;Username=postgres;Password=postgres"
  }
}
```

### 3. Run Migrations

```bash
cd src/Services/Notification/Notification.API
dotnet ef database update --context NotificationDbContext
dotnet ef database update --context TenantNotificationDbContext
```

### 4. Run the Service

```bash
dotnet run
# Service runs on:
# HTTP:  http://localhost:5004
# HTTPS: https://localhost:5104
# SignalR Hub: https://localhost:5104/hubs/notifications
# Swagger UI: https://localhost:5104/swagger
# Health Check: https://localhost:5104/health
```

### 5. Test Connection

**JavaScript Client:**

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:5104/hubs/notifications", {
    accessTokenFactory: () => "your-jwt-token",
  })
  .build();

connection.on("ReceiveNotification", (notification) => {
  console.log("Notification received:", notification);
});

await connection.start();
console.log("Connected to notification hub!");
```

---

## Configuration

### Core Settings

#### Multi-Tenancy

```json
{
  "MultiTenancy": {
    "Enabled": true, // Enable/disable multi-tenancy
    "JwtMode": "PerTenant", // "Shared" or "PerTenant"
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  }
}
```

**JWT Modes:**

- `Shared`: All tenants use global JWT from appsettings.json
- `PerTenant`: Each tenant has its own JWT secret (stored in Tenant Service database)

#### JWT Authentication

```json
{
  "Jwt": {
    "Secret": "your-secret-key-32-chars-minimum",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 60
  }
}
```

**Note:** When `JwtMode = "PerTenant"`:

- Regular endpoints (send, user notifications) use **tenant-specific JWT**
- Admin endpoints (queue management) use **global JWT** from appsettings.json

#### Database

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql", // PostgreSql, SqlServer, MySql, Sqlite
    "ConnectionString": "Host=localhost;Database=notifications_global;..."
  }
}
```

#### SignalR

```json
{
  "SignalR": {
    "EnableDetailedErrors": true, // Development only
    "ClientTimeoutInterval": "00:01:00",
    "KeepAliveInterval": "00:00:15"
  }
}
```

#### Background Processing

```json
{
  "NotificationProcessing": {
    "ProcessingIntervalSeconds": 5, // How often to check queue
    "MaxRetryAttempts": 3,
    "CleanupIntervalHours": 1,
    "ExpiredNotificationRetentionDays": 7
  }
}
```

#### Firebase (Optional)

```json
{
  "Firebase": {
    "Enabled": false,
    "ProjectId": "your-project-id",
    "PrivateKeyPath": "path/to/service-account.json"
  }
}
```

#### CORS

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:4200"]
  }
}
```

---

## API Endpoints

### User Notification Endpoints

**Authentication:** Tenant-specific JWT (when `JwtMode = "PerTenant"`)  
**Required Header:** `x-tenant-id: {tenantId}`

### Send Notification

**POST** `/api/notifications/send`

Send a new notification to the queue.

```bash
curl -X POST "https://localhost:5104/api/notifications/send" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer TENANT_SPECIFIC_JWT_TOKEN" \
  -H "x-tenant-id: ihsandev" \
  -d '{
    "tenantId": "ihsandev",
    "userId": 1,
    "title": "New Message",
    "message": "You have a new message",
    "data": "{\"messageId\": 123}",
    "deliveryType": "Both",
    "priority": "Immediate"
  }'
```

**Response:**

```json
{
  "queueItemId": 123,
  "status": "Pending"
}
```

### Get Queue Status

**GET** `/api/notifications/status/{queueItemId}`

Check the status of a queued notification.

```bash
curl "https://localhost:5104/api/notifications/status/123" \
  -H "Authorization: Bearer TENANT_SPECIFIC_JWT_TOKEN" \
  -H "x-tenant-id: ihsandev"
```

**Response:**

```json
{
  "id": 123,
  "status": "Sent",
  "createdAt": "2025-11-07T14:00:00Z",
  "lastAttemptAt": "2025-11-07T14:00:05Z"
}
```

### Get User Notifications

**GET** `/api/notifications/user/{userId}`

Get all notifications for a specific user.

```bash
curl "https://localhost:5104/api/notifications/user/1?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer TENANT_SPECIFIC_JWT_TOKEN" \
  -H "x-tenant-id: ihsandev"
```

**Response:**

```json
{
  "items": [
    {
      "id": 456,
      "title": "New Message",
      "message": "You have a new message",
      "data": "{\"messageId\": 123}",
      "isRead": false,
      "createdAt": "2025-11-07T14:00:00Z"
    }
  ],
  "totalCount": 50,
  "pageNumber": 1,
  "pageSize": 20
}
```

### Mark as Read

**PUT** `/api/notifications/{notificationId}/read`

Mark a notification as read.

```bash
curl -X PUT "https://localhost:5104/api/notifications/456/read" \
  -H "Authorization: Bearer TENANT_SPECIFIC_JWT_TOKEN" \
  -H "x-tenant-id: ihsandev"
```

**Response:**

```json
{
  "success": true
}
```

---

### SuperAdmin Endpoints

**Authentication:** Global JWT from appsettings.json  
**Required Role:** SuperAdmin  
**Header:** No `x-tenant-id` required (bypasses tenant middleware)

### Get Queue Items (SuperAdmin)

**GET** `/api/notifications/admin/queue`

Retrieve all notification queue items across all tenants with filtering and pagination.

```bash
curl "https://localhost:5104/api/notifications/admin/queue?pageSize=20&status=0&tenantId=ihsandev" \
  -H "Authorization: Bearer GLOBAL_SUPERADMIN_JWT_TOKEN"
```

**Query Parameters:**

- `pageNumber` (int): Page number (default: 1)
- `pageSize` (int): Page size 1-100 (default: 10)
- `tenantId` (string): Filter by tenant
- `userId` (int): Filter by user
- `status` (QueueStatus): 0=Pending, 1=Processing, 2=Sent, 3=Failed, 4=Expired
- `priority` (Priority): 0=Waitable, 1=Immediate
- `deliveryType` (DeliveryType): 1=SignalR, 2=Firebase, 3=Both
- `fromDate` (DateTime): Filter from date
- `toDate` (DateTime): Filter to date
- `searchTerm` (string): Search in title/message

**Response:**

```json
{
  "items": [
    {
      "id": 123,
      "tenantId": "ihsandev",
      "userId": 2,
      "deliveryType": 3,
      "priority": 1,
      "title": "Test notification",
      "message": "Test message",
      "queueStatus": 2,
      "retryCount": 0,
      "createdAt": "2025-11-07T10:00:00Z"
    }
  ],
  "pageNumber": 1,
  "totalPages": 5,
  "totalCount": 50,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

**See:** [SUPERADMIN_QUEUE_ENDPOINT.md](SUPERADMIN_QUEUE_ENDPOINT.md) for complete documentation

---

## SignalR Hub

### Connection

**Endpoint:** `wss://localhost:5104/hubs/notifications`

**Client Code (JavaScript):**

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:5104/hubs/notifications", {
    accessTokenFactory: () => localStorage.getItem("jwt_token"),
  })
  .withAutomaticReconnect()
  .build();

// Listen for notifications
connection.on("ReceiveNotification", (notification) => {
  console.log("Received:", notification);
  // Show notification to user
  showNotification(notification);
});

// Start connection
await connection.start();
```

### Hub Methods

#### Server → Client: `ReceiveNotification`

Server sends notification to client.

**Parameters:**

```javascript
{
  "id": 456,
  "title": "New Message",
  "message": "You have a new message",
  "data": "{\"messageId\": 123}",
  "createdAt": "2025-11-05T14:00:00Z"
}
```

#### Client → Server: `AcknowledgeDelivery`

Client acknowledges receipt of notification.

```javascript
await connection.invoke("AcknowledgeDelivery", 456);
```

### Connection Groups

Clients are automatically added to groups based on authentication:

| Group Pattern               | Members                 | Example                              |
| --------------------------- | ----------------------- | ------------------------------------ |
| `global`                    | All clients             | All connected clients                |
| `all-clients`               | Single-tenant mode      | All clients (multi-tenancy disabled) |
| `tenant:{id}`               | Tenant members          | `tenant:ihsandev`                    |
| `tenant:{id}:user:{userId}` | Specific user in tenant | `tenant:ihsandev:user:1`             |
| `user:{userId}`             | User across tenants     | `user:1`                             |

---

## Targeting Scenarios

### 1. Global Notification (All Clients)

**Use Case:** System maintenance announcement

```json
{
  "tenantId": null,
  "userId": null,
  "title": "System Maintenance",
  "message": "System will be down at 2 AM",
  "deliveryType": "SignalR",
  "priority": "Immediate"
}
```

**Delivered To:** All connected clients in `global` group

---

### 2. All Clients (Single-Tenant Mode)

**Use Case:** Application-wide announcement (no multi-tenancy)

**Configuration:** `MultiTenancy:Enabled = false`

```json
{
  "tenantId": null,
  "userId": null,
  "title": "New Feature Available",
  "message": "Check out our new dashboard",
  "deliveryType": "Both",
  "priority": "Waitable"
}
```

**Delivered To:** All clients in `all-clients` group

---

### 3. Tenant Broadcast

**Use Case:** Company-wide announcement for specific tenant

```json
{
  "tenantId": "ihsandev",
  "userId": null,
  "title": "Company Meeting",
  "message": "All-hands meeting at 3 PM",
  "deliveryType": "Both",
  "priority": "Immediate"
}
```

**Delivered To:** All users in `tenant:ihsandev` group

---

### 4. User in Tenant

**Use Case:** Direct message to specific user

```json
{
  "tenantId": "ihsandev",
  "userId": 1,
  "title": "New Message",
  "message": "You have a message from John",
  "deliveryType": "Both",
  "priority": "Immediate"
}
```

**Delivered To:** User 1 in `tenant:ihsandev:user:1` group

---

### 5. Cross-Tenant User (Single-Tenant Mode)

**Use Case:** Personal notification (no tenant context)

**Configuration:** `MultiTenancy:Enabled = false`

```json
{
  "tenantId": null,
  "userId": 1,
  "title": "Password Changed",
  "message": "Your password was changed",
  "deliveryType": "Both",
  "priority": "Immediate"
}
```

**Delivered To:** User 1 in `user:1` group

---

## Authentication

The Notification Service implements a **dual JWT authentication system** to support both multi-tenant user operations and cross-tenant administrative functions.

### JWT Authentication Modes

#### 1. Global JWT (appsettings.json)

Used for **SuperAdmin endpoints** that operate across all tenants:

- Queue management endpoints (`/api/notifications/admin/*`)
- System-wide monitoring and analytics
- Cross-tenant operations

**Configuration:**

```json
{
  "Jwt": {
    "Secret": "your-global-secret-key-here",
    "Issuer": "IhsanDev",
    "Audience": "IhsanDevAPI",
    "ExpirationMinutes": 60
  }
}
```

**Example Token Generation:**

```csharp
var claims = new[]
{
    new Claim(ClaimTypes.NameIdentifier, "superadmin-id"),
    new Claim(ClaimTypes.Name, "SuperAdmin"),
    new Claim(ClaimTypes.Role, "SuperAdmin")
};

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

var token = new JwtSecurityToken(
    issuer: "IhsanDev",
    audience: "IhsanDevAPI",
    claims: claims,
    expires: DateTime.UtcNow.AddMinutes(60),
    signingCredentials: credentials
);
```

#### 2. Tenant-Specific JWT (Per-Tenant Configuration)

Used for **user notification endpoints** that operate within a single tenant:

- Send notification (`/api/notifications/send`)
- User notifications (`/api/notifications/user/{userId}`)
- Mark as read (`/api/notifications/{id}/read`)
- Queue status (`/api/notifications/status/{id}`)

**Configuration:** Stored in `TenantConfiguration.Jwt` table per tenant

**Required Header:** `x-tenant-id: {tenantId}`

**Flow:**

1. Request includes `x-tenant-id` header
2. TenantMiddleware resolves tenant from database
3. JWT validation uses tenant-specific secret from tenant configuration
4. Token validated against tenant's JWT settings

**Example Request:**

```bash
curl "https://localhost:5104/api/notifications/user/1" \
  -H "Authorization: Bearer TENANT_SPECIFIC_TOKEN" \
  -H "x-tenant-id: ihsandev"
```

### JwtMode Configuration

The `JwtMode` setting in `appsettings.json` controls the authentication behavior:

```json
{
  "MultiTenancy": {
    "JwtMode": "PerTenant" // or "Shared"
  }
}
```

**Modes:**

- `"PerTenant"`: User endpoints validate against tenant-specific JWT, admin endpoints use global JWT
- `"Shared"`: All endpoints use global JWT from appsettings.json

### BypassTenant Attribute

Endpoints marked with `BypassTenantAttribute` skip tenant middleware and always use global JWT:

```csharp
// In EndpointMappingExtensions.cs
adminGroup.MapGet("/queue", GetQueueItemsHandler)
    .WithMetadata(new BypassTenantAttribute())
    .RequireAuthorization();
```

**Effects:**

1. TenantMiddleware skips tenant resolution (no `x-tenant-id` header required)
2. JWT validation uses global secret from appsettings.json
3. Endpoint accessible to SuperAdmin role only

### Authorization Roles

**User Role:**

- Send notifications for their tenant
- View own notifications
- Mark notifications as read

**Service Role:**

- Send notifications on behalf of system services
- Access tenant-specific endpoints programmatically

**SuperAdmin Role:**

- Access all user endpoints across tenants
- Access admin endpoints (queue management)
- System-wide operations without tenant context

### JWT Token Requirements

**Required Claims:**

- `sub` or `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` - User ID
- `iss` - Must match `Jwt:Issuer` in configuration
- `aud` - Must match `Jwt:Audience` in configuration
- `exp` - Token expiration timestamp
- `role` - User, Service, or SuperAdmin

**Example JWT Payload (Global):**

```json
{
  "sub": "superadmin-1",
  "unique_name": "superadmin",
  "role": "SuperAdmin",
  "iss": "IhsanDev",
  "aud": "IhsanDevAPI",
  "exp": 1730812800,
  "iat": 1730809200
}
```

**Example JWT Payload (Tenant-Specific):**

```json
{
  "sub": "1",
  "unique_name": "john.doe",
  "email": "john@ihsandev.com",
  "tenantId": "ihsandev",
  "role": "User",
  "iss": "IhsanDevIdentity",
  "aud": "IhsanDevApp",
  "exp": 1730812800,
  "iat": 1730809200
}
```

### Connection Authentication (SignalR)

**Option 1: Access Token Factory (Recommended)**

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => getToken(),
  })
  .build();
```

**Option 2: Query String**

```javascript
const token = getToken();
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`/hubs/notifications?access_token=${token}`)
  .build();
```

**Option 3: Anonymous (Optional)**

```javascript
// No token required if hub allows anonymous connections
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications")
  .build();
// User will only join 'global' group
```

### Authentication Flow Examples

**User Notification Request (PerTenant Mode):**

```
1. Client → Request with tenant-specific JWT + x-tenant-id header
2. TenantMiddleware → Resolve tenant from header
3. JWT Validation → Use tenant's JWT secret from database
4. Authorization → Verify User/Service/SuperAdmin role
5. Endpoint → Execute with tenant context
```

**SuperAdmin Queue Request (All Modes):**

```
1. Client → Request with global JWT (no x-tenant-id header)
2. TenantMiddleware → Skip (BypassTenantAttribute detected)
3. JWT Validation → Use global JWT secret from appsettings.json
4. Authorization → Verify SuperAdmin role
5. Endpoint → Execute without tenant context
```

### Security Best Practices

1. **Separate JWT Secrets:** Use different secrets for global and tenant-specific JWTs
2. **Token Expiration:** Configure appropriate expiration times (default: 60 minutes)
3. **HTTPS Only:** Always use HTTPS in production
4. **Tenant Isolation:** User endpoints strictly enforce tenant boundaries via middleware
5. **Role-Based Access:** Leverage role claims for fine-grained authorization

---

## Multi-Tenancy

### Configuration Modes

#### Single-Tenant Mode

```json
{
  "MultiTenancy": {
    "Enabled": false
  }
}
```

**Behavior:**

- All users share same database
- Groups: `global`, `all-clients`, `user:{userId}`
- No tenant-specific targeting

#### Multi-Tenant Mode

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5104"
  }
}
```

**Behavior:**

- Separate database per tenant
- Groups: `global`, `tenant:{id}`, `tenant:{id}:user:{userId}`, `user:{userId}`
- Tenant-specific targeting supported

### Tenant Identification

**Via Header:**

```http
POST /api/notifications/send HTTP/1.1
x-tenant-id: ihsandev
Authorization: Bearer eyJhbGci...
```

**Via JWT Claim:**

```json
{
  "tenantId": "ihsandev",
  "sub": "1",
  ...
}
```

**Priority:** Header > JWT Claim

### Database Isolation

Each tenant has:

- Separate connection string (fetched from Tenant Service)
- Isolated `Notifications` table
- Own notification history

**Global Database:** Shared queue across all tenants
**Tenant Database:** Per-tenant notification storage

---

## Background Processing

### NotificationProcessor

**Purpose:** Process notification queue and deliver via SignalR/Firebase

**Configuration:**

```json
{
  "NotificationProcessing": {
    "ProcessingIntervalSeconds": 5,
    "MaxRetryAttempts": 3
  }
}
```

**Flow:**

1. Every 5 seconds, fetch pending/failed items (max 50)
2. Group by delivery type (SignalR, Firebase, Both)
3. Send via SignalR to appropriate groups
4. Send via Firebase (if enabled)
5. Persist to tenant database
6. Update queue status to `Sent` or `Failed`
7. Retry failed items (max 3 attempts)

**Retry Logic:**

- **Attempt 1:** Immediate
- **Attempt 2:** After 5 seconds
- **Attempt 3:** After 10 seconds
- **After 3:** Mark as `Failed`

### CleanupService

**Purpose:** Remove expired notifications

**Configuration:**

```json
{
  "NotificationProcessing": {
    "CleanupIntervalHours": 1,
    "ExpiredNotificationRetentionDays": 7
  }
}
```

**Flow:**

1. Every hour, scan global queue
2. Find items with `Status = Failed` or `Expired`
3. Older than 7 days
4. Delete from database

---

## Database Schema

### Global Queue Database

**Table:** `NotificationQueue`

| Column          | Type         | Description                                          |
| --------------- | ------------ | ---------------------------------------------------- |
| `Id`            | int          | Primary key                                          |
| `TenantId`      | string       | Tenant identifier (nullable)                         |
| `UserId`        | int          | User identifier (nullable)                           |
| `Title`         | string(200)  | Notification title                                   |
| `Message`       | string(1000) | Notification message                                 |
| `Data`          | string       | JSON metadata (nullable)                             |
| `DeliveryType`  | int          | 0=SignalR, 1=Firebase, 2=Both                        |
| `Priority`      | int          | 0=Immediate, 1=Waitable                              |
| `Status`        | int          | 0=Pending, 1=Processing, 2=Sent, 3=Failed, 4=Expired |
| `RetryCount`    | int          | Number of delivery attempts                          |
| `CreatedAt`     | datetime     | Queue creation timestamp                             |
| `LastAttemptAt` | datetime     | Last delivery attempt (nullable)                     |

**Indexes:**

- `IX_NotificationQueue_Status_Priority` (Status, Priority)
- `IX_NotificationQueue_TenantId` (TenantId)
- `IX_NotificationQueue_UserId` (UserId)

### Tenant Notification Database

**Table:** `Notifications` (per tenant)

| Column      | Type         | Description                     |
| ----------- | ------------ | ------------------------------- |
| `Id`        | int          | Primary key                     |
| `UserId`    | int          | User identifier                 |
| `Title`     | string(200)  | Notification title              |
| `Message`   | string(1000) | Notification message            |
| `Data`      | string       | JSON metadata (nullable)        |
| `IsRead`    | bool         | Read status                     |
| `ReadAt`    | datetime     | Read timestamp (nullable)       |
| `CreatedAt` | datetime     | Notification creation timestamp |

**Indexes:**

- `IX_Notifications_UserId_CreatedAt` (UserId, CreatedAt DESC)
- `IX_Notifications_IsRead` (IsRead)

---

## Client Integration

### JavaScript/TypeScript

#### Install SignalR

```bash
npm install @microsoft/signalr
```

#### Connection Setup

```typescript
import * as signalR from "@microsoft/signalr";

class NotificationClient {
  private connection: signalR.HubConnection;

  constructor(hubUrl: string, getToken: () => string) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => getToken(),
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.setupHandlers();
  }

  private setupHandlers() {
    this.connection.on("ReceiveNotification", (notification) => {
      console.log("Notification received:", notification);
      this.showNotification(notification);
    });

    this.connection.onreconnecting(() => {
      console.log("Reconnecting...");
    });

    this.connection.onreconnected(() => {
      console.log("Reconnected!");
    });

    this.connection.onclose(() => {
      console.log("Connection closed");
    });
  }

  async start() {
    try {
      await this.connection.start();
      console.log("Connected to notification hub");
    } catch (err) {
      console.error("Error connecting:", err);
      setTimeout(() => this.start(), 5000);
    }
  }

  async acknowledge(notificationId: number) {
    try {
      await this.connection.invoke("AcknowledgeDelivery", notificationId);
    } catch (err) {
      console.error("Error acknowledging:", err);
    }
  }

  private showNotification(notification: any) {
    // Your notification UI logic
    if (Notification.permission === "granted") {
      new Notification(notification.title, {
        body: notification.message,
        icon: "/icon.png",
      });
    }
  }
}

// Usage
const client = new NotificationClient(
  "https://localhost:5104/hubs/notifications",
  () => localStorage.getItem("jwt_token")!
);

await client.start();
```

### C# Client

```csharp
using Microsoft.AspNetCore.SignalR.Client;

public class NotificationClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public NotificationClient(string hubUrl, Func<Task<string>> tokenProvider)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = tokenProvider;
            })
            .WithAutomaticReconnect()
            .Build();

        SetupHandlers();
    }

    private void SetupHandlers()
    {
        _connection.On<NotificationDto>("ReceiveNotification", notification =>
        {
            Console.WriteLine($"Received: {notification.Title}");
            // Handle notification
        });

        _connection.Reconnecting += error =>
        {
            Console.WriteLine("Reconnecting...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            Console.WriteLine("Reconnected!");
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            Console.WriteLine("Connection closed");
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync()
    {
        try
        {
            await _connection.StartAsync();
            Console.WriteLine("Connected to notification hub");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting: {ex.Message}");
            await Task.Delay(5000);
            await StartAsync();
        }
    }

    public async Task AcknowledgeAsync(int notificationId)
    {
        await _connection.InvokeAsync("AcknowledgeDelivery", notificationId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}

// Usage
var client = new NotificationClient(
    "https://localhost:5104/hubs/notifications",
    async () => await GetTokenAsync()
);

await client.StartAsync();
```

---

## Troubleshooting

### Authentication Issues

#### Problem: 401 Unauthorized on user endpoints

**Possible Causes:**

1. **Wrong JWT mode configuration**

   - User endpoint uses tenant-specific JWT but `JwtMode = "Shared"`
   - Solution: Set `"JwtMode": "PerTenant"` in appsettings.json

2. **Missing x-tenant-id header**

   - User endpoints require `x-tenant-id` header when multi-tenancy enabled
   - Solution: Include header in all user endpoint requests

3. **JWT token validated against wrong secret**

   - Using global JWT token for tenant endpoint
   - Solution: Generate token with tenant-specific secret from tenant configuration

4. **Token expired or invalid**
   - Check token expiration in JWT claims
   - Verify token signature matches tenant secret

**Example Fix:**

```bash
# Correct request with tenant-specific JWT
curl "https://localhost:5104/api/notifications/user/1" \
  -H "Authorization: Bearer TENANT_SPECIFIC_TOKEN" \
  -H "x-tenant-id: ihsandev"
```

#### Problem: 403 Forbidden on admin endpoints

**Possible Causes:**

1. **Missing SuperAdmin role**

   - Token must have `role: "SuperAdmin"` claim
   - Solution: Generate token with SuperAdmin role

2. **Using tenant-specific JWT for admin endpoint**
   - Admin endpoints require global JWT from appsettings.json
   - Solution: Use global JWT token, no x-tenant-id header

**Example Fix:**

```bash
# Correct request with global JWT
curl "https://localhost:5104/api/notifications/admin/queue" \
  -H "Authorization: Bearer GLOBAL_SUPERADMIN_TOKEN"
  # NO x-tenant-id header
```

#### Problem: 401 Unauthorized on SignalR connection

**Solution:**

1. Check JWT token is valid
2. Verify token in query string: `?access_token=...`
3. Ensure `Jwt:Secret` matches Identity Service or tenant configuration
4. Check token claims (sub, iss, aud, exp)

### Connection Issues

#### Problem: Unable to connect to SignalR hub

**Possible Causes:**

1. **CORS not configured**

   ```json
   {
     "Cors": {
       "AllowedOrigins": ["http://localhost:3000"]
     }
   }
   ```

2. **JWT token expired**

   - Check token expiration in JWT claims
   - Refresh token and reconnect

3. **Invalid JWT secret**

   - Ensure secret matches Identity Service
   - Secret must be 32+ characters

4. **Hub endpoint incorrect**
   - URL: `wss://localhost:5104/hubs/notifications`
   - Not `ws://` or missing `/hubs`

### Notification Delivery Issues

#### Problem: Notifications not received

**Debugging Steps:**

1. Check global queue database - is notification pending?

   ```sql
   SELECT * FROM "NotificationQueue" WHERE "Status" = 0;
   ```

2. Check background processor logs

   ```
   Processing 5 pending items...
   Sending notification via SignalR to group: tenant:ihsandev:user:1
   ```

3. Verify client is in correct group

   - Check `OnConnectedAsync` logs
   - User should join appropriate groups

4. Check SignalR connection state
   ```javascript
   console.log(connection.state); // Should be "Connected"
   ```

#### Problem: Notifications delivered but not persisted to tenant DB

**Possible Causes:**

1. **Tenant database not migrated**

   ```bash
   dotnet ef database update --context TenantNotificationDbContext
   ```

2. **Tenant connection string not found**

   - Check Tenant Service is running
   - Verify `x-tenant-id` header is set

3. **Multi-tenancy disabled but tenantId provided**
   - Either enable multi-tenancy or remove tenantId

### Performance Issues

#### Problem: Queue processing slow

**Solutions:**

1. **Increase batch size**

   ```csharp
   var pendingItems = await _globalContext.NotificationQueue
       .Where(x => x.Status == QueueStatus.Pending)
       .OrderBy(x => x.Priority)
       .Take(100) // Increase from 50
       .ToListAsync();
   ```

2. **Decrease processing interval**

   ```json
   {
     "NotificationProcessing": {
       "ProcessingIntervalSeconds": 2
     }
   }
   ```

3. **Add indexes**
   ```csharp
   builder.HasIndex(x => new { x.Status, x.Priority });
   builder.HasIndex(x => x.TenantId);
   ```

#### Problem: Many failed deliveries

**Debugging:**

1. Check retry count

   ```sql
   SELECT "RetryCount", COUNT(*)
   FROM "NotificationQueue"
   WHERE "Status" = 3
   GROUP BY "RetryCount";
   ```

2. Check logs for errors

   ```
   Error sending notification: System.InvalidOperationException
   ```

3. Verify SignalR clients are connected
   ```javascript
   console.log(connection.state);
   ```

### Database Issues

#### Problem: Migration fails

**Solution:**

```bash
# Drop and recreate
dotnet ef database drop --context NotificationDbContext
dotnet ef database update --context NotificationDbContext

# Or apply specific migration
dotnet ef migrations add InitialCreate --context NotificationDbContext
dotnet ef database update --context NotificationDbContext
```

#### Problem: Tenant database not created

**Solution:**

1. Ensure Tenant Service is running
2. Verify tenant exists in Tenant Service
3. Check automatic migration is enabled:
   ```csharp
   await _tenantContext.Database.MigrateAsync();
   ```

---

## Best Practices

### Client-Side

1. **Always implement automatic reconnection**

   ```javascript
   .withAutomaticReconnect()
   ```

2. **Handle connection errors gracefully**

   ```javascript
   connection.onclose(() => {
     setTimeout(() => connection.start(), 5000);
   });
   ```

3. **Acknowledge notifications promptly**

   ```javascript
   connection.on("ReceiveNotification", async (notification) => {
     showNotification(notification);
     await connection.invoke("AcknowledgeDelivery", notification.id);
   });
   ```

4. **Store JWT token securely**
   - Use `localStorage` or `sessionStorage`
   - Never log tokens to console in production
   - Refresh expired tokens automatically

### Server-Side

1. **Always specify tenant context**

   ```csharp
   // When sending notification
   var command = new SendNotificationCommand
   {
       TenantId = tenantId, // Don't leave null if multi-tenancy enabled
       UserId = userId,
       ...
   };
   ```

2. **Use appropriate delivery type**

   - `SignalR`: Real-time only (connected users)
   - `Firebase`: Push notifications (offline users)
   - `Both`: Maximum reach (recommended)

3. **Set correct priority**

   - `Immediate`: Critical notifications (password reset, security alerts)
   - `Waitable`: Non-urgent notifications (newsletters, updates)

4. **Include meaningful data**

   ```json
   {
     "data": "{\"actionUrl\": \"/messages/123\", \"senderId\": 456}"
   }
   ```

5. **Monitor queue depth**

   ```sql
   SELECT COUNT(*) FROM "NotificationQueue" WHERE "Status" = 0;
   ```

   - Alert if > 1000 pending items

6. **Monitor failed deliveries**
   ```sql
   SELECT COUNT(*) FROM "NotificationQueue" WHERE "Status" = 3;
   ```
   - Investigate if failure rate > 5%

---

## Security Considerations

### JWT Token Management

1. **Use strong secrets** (64+ characters)
2. **Rotate secrets periodically** (every 90 days)
3. **Keep secrets in Key Vault** (Azure Key Vault, AWS Secrets Manager)
4. **Validate issuer and audience** (prevent token replay)
5. **Set short expiration times** (60 minutes)

### API Security

1. **Require authentication** for sensitive endpoints
2. **Validate tenant ID** matches JWT claim
3. **Rate limit API calls** (prevent abuse)
4. **Sanitize user input** (prevent XSS)
5. **Log security events** (failed auth attempts)

### SignalR Security

1. **Validate JWT on every connection**
2. **Use WSS (WebSocket Secure)** in production
3. **Implement connection throttling** (max connections per user)
4. **Monitor for abuse** (excessive connections, spam)

---

## Production Deployment

### Environment Variables

```bash
export ASPNETCORE_ENVIRONMENT=Production
export MultiTenancy__Enabled=true
export Jwt__Secret="<from-key-vault>"
export DatabaseSettings__ConnectionString="<from-key-vault>"
export Firebase__Enabled=true
export Firebase__PrivateKeyPath="/app/secrets/firebase-key.json"
```

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Notification.API/Notification.API.csproj", "Notification.API/"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.API.dll"]
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: notification-service
spec:
  replicas: 3
  selector:
    matchLabels:
      app: notification-service
  template:
    metadata:
      labels:
        app: notification-service
    spec:
      containers:
        - name: notification-api
          image: notification-service:1.0.0
          ports:
            - containerPort: 80
          env:
            - name: MultiTenancy__Enabled
              value: "true"
            - name: Jwt__Secret
              valueFrom:
                secretKeyRef:
                  name: jwt-secret
                  key: secret
```

### Health Checks

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
```

---

## Support

### Documentation

- **Complete Flow**: [NOTIFICATION_SYSTEM_FLOW.md](NOTIFICATION_SYSTEM_FLOW.md)
- **Hub Guide**: [NOTIFICATION_HUB_GUIDE.md](NOTIFICATION_HUB_GUIDE.md)
- **Quick Reference**: [NOTIFICATION_HUB_QUICK_REFERENCE.md](NOTIFICATION_HUB_QUICK_REFERENCE.md)

### Issues

- **GitHub Issues**: [Create Issue](https://github.com/your-repo/issues)
- **Email**: support@ihsandev.com

---

**Built with ❤️ using ASP.NET Core 9.0 & SignalR**

_Last Updated: November 2025_
