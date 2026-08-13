# Device Token Management - Implementation Guide

## Overview

Device tokens have been extracted from the `BaseUser` entity into a separate `DeviceToken` entity following the Single Responsibility Principle and Clean Architecture patterns. This allows for better management of push notification tokens across multiple devices per user.

## Architecture Changes

### 1. Entity Structure

#### DeviceToken Entity (Shared.Kernel)

Located at: `src/Shared/IhsanDev.Shared.Kernel/Entities/DeviceToken.cs`

```csharp
public class DeviceToken : BaseEntity
{
    public int UserId { get; set; }                 // Foreign key to user
    public string Token { get; set; }               // FCM/APNs token (max 500 chars)
    public Platform Platform { get; set; }          // iOS, Android, Web
    public string? DeviceIdentifier { get; set; }   // Optional device ID
    public DateTime? LastVerifiedAt { get; set; }   // Last token verification
    public bool IsPrimary { get; set; }             // Primary device flag
}
```

#### Platform Enum (Shared.Kernel)

Located at: `src/Shared/IhsanDev.Shared.Kernel/Enums/Platform.cs`

```csharp
public enum Platform
{
    iOS = 0,
    Android = 1,
    Web = 2
}
```

### 2. Changes to BaseUser

**Removed:**

- `FirebaseToken` property (deprecated)

**Migration Impact:**

- Existing `FirebaseToken` data should be migrated to `DeviceToken` table
- Old column will be dropped in migration

### 3. Repository Pattern

#### Interface (Identity.Domain)

Located at: `src/Services/Identity/Identity.Domain/Repositories/IDeviceTokenRepository.cs`

Methods:

- `GetByIdAsync(int id)` - Get token by ID
- `GetByUserIdAsync(int userId)` - Get all tokens for user
- `GetByTokenAsync(string token)` - Find token by string
- `GetByUserIdAndPlatformAsync(userId, platform)` - Filter by platform
- `AddAsync(DeviceToken)` - Add new token
- `UpdateAsync(DeviceToken)` - Update existing
- `DeleteAsync(DeviceToken)` - Soft delete
- `DeleteByUserIdAsync(int userId)` - Delete all user tokens
- `ExistsAsync(string token)` - Check if token exists

#### Implementation (Identity.Infrastructure)

Located at: `src/Services/Identity/Identity.Infrastructure/Repositories/DeviceTokenRepository.cs`

Features:

- Soft delete using `IsArchived` flag
- Ordering by `IsPrimary` and `Created` date
- Automatic `LastModified` timestamp updates

## CQRS Implementation

### Commands (Identity.Application)

Located at: `src/Services/Identity/Identity.Application/Commands/DeviceToken/`

1. **AddDeviceTokenCommand**

   - Adds or updates device token
   - Ensures only one primary token per user/platform
   - Auto-verifies token on creation

2. **UpdateDeviceTokenCommand**

   - Updates token, device identifier, or primary status
   - Handles primary flag conflicts

3. **DeleteDeviceTokenCommand**

   - Soft deletes single token

4. **DeleteAllUserDeviceTokensCommand**
   - Soft deletes all tokens for a user

### Queries (Identity.Application)

1. **GetDeviceTokenByIdQuery**
2. **GetUserDeviceTokensQuery**
3. **GetUserDeviceTokensByPlatformQuery**
4. **GetDeviceTokenByTokenQuery**

### Validation

FluentValidation rules in: `src/Services/Identity/Identity.Application/Validators/DeviceToken/`

- UserId must be > 0
- Token required, max 500 characters
- Platform must be valid enum value
- DeviceIdentifier max 100 characters

## API Endpoints

All endpoints require `Service`, `User`, `Admin`, or `SuperAdmin` role authorization (a service calling with `X-Service-Secret`/`X-Service-Name` is authorized the same as a real user token). The group carries `[OptionalTenant]` — `x-tenant-id` is optional on every endpoint below.

Base URL: `/api/v1/device-tokens`

### Endpoints

| Method | Endpoint                                   | Description                                        |
| ------ | ------------------------------------------ | --------------------------------------------------- |
| POST   | `/`                                        | Add new device token                                |
| GET    | `/{id}`                                    | Get token by ID                                     |
| GET    | `/user/{userId}`                           | Get all user tokens                                 |
| GET    | `/user/{userId}/platform?platform={value}` | Get user tokens by platform                         |
| PUT    | `/{id}`                                    | Update token                                        |
| DELETE | `/{id}`                                    | Delete token                                        |
| DELETE | `/user/{userId}`                           | Delete all user tokens                              |
| POST   | `/batch`                                   | Get device tokens for multiple users in one call (service-to-service — used by `NotificationProcessor` to batch-fetch tokens for a tenant group) |
| DELETE | `/batch`                                   | Delete multiple device tokens in one call (service-to-service — used to clean up invalid/expired tokens after a failed Firebase send) |
| GET    | `/tenant`                                  | Get all device tokens for the current tenant (or global if no `x-tenant-id`) — used to broadcast a tenant-wide push notification |

### Request Examples

#### Add Device Token

```bash
POST /api/v1/device-tokens
Content-Type: application/json
Authorization: Bearer {token}

{
  "userId": 1,
  "token": "fcm_token_string_here",
  "platform": 1,  // 0=iOS, 1=Android, 2=Web
  "deviceIdentifier": "device-123",
  "isPrimary": true
}
```

**Response:** `201 Created`

```json
{
  "id": 1,
  "userId": 1,
  "token": "fcm_token_string_here",
  "platform": 1,
  "deviceIdentifier": "device-123",
  "lastVerifiedAt": "2025-11-13T10:00:00Z",
  "isPrimary": true,
  "created": "2025-11-13T10:00:00Z"
}
```

#### Get User Device Tokens

```bash
GET /api/v1/device-tokens/user/1
Authorization: Bearer {token}
```

**Response:** `200 OK`

```json
[
  {
    "id": 1,
    "userId": 1,
    "token": "fcm_token_android",
    "platform": 1,
    "deviceIdentifier": "android-device",
    "lastVerifiedAt": "2025-11-13T10:00:00Z",
    "isPrimary": true,
    "created": "2025-11-13T09:00:00Z"
  },
  {
    "id": 2,
    "userId": 1,
    "token": "fcm_token_ios",
    "platform": 0,
    "deviceIdentifier": "iphone-12",
    "lastVerifiedAt": "2025-11-13T08:00:00Z",
    "isPrimary": false,
    "created": "2025-11-12T15:00:00Z"
  }
]
```

#### Update Device Token

```bash
PUT /api/v1/device-tokens/1
Content-Type: application/json
Authorization: Bearer {token}

{
  "token": "new_fcm_token",
  "isPrimary": true
}
```

#### Delete Device Token

```bash
DELETE /api/v1/device-tokens/1
Authorization: Bearer {token}
```

**Response:** `204 No Content`

#### Delete All User Tokens

```bash
DELETE /api/v1/device-tokens/user/1
Authorization: Bearer {token}
```

**Response:** `204 No Content`

## Database Schema

### DeviceTokens Table

| Column           | Type         | Nullable | Description                 |
| ---------------- | ------------ | -------- | --------------------------- |
| Id               | int          | No       | Primary key                 |
| UserId           | int          | No       | Foreign key to Users        |
| Token            | varchar(500) | No       | Device token string         |
| Platform         | varchar(50)  | No       | Platform enum as string     |
| DeviceIdentifier | varchar(100) | Yes      | Optional device ID          |
| LastVerifiedAt   | datetime     | Yes      | Last verification timestamp |
| IsPrimary        | bit          | No       | Primary device flag         |
| IsArchived       | bit          | No       | Soft delete flag            |
| Status           | bit          | No       | Active status               |
| Created          | datetime     | No       | Creation timestamp          |
| CreatedBy        | varchar      | Yes      | Creator identifier          |
| LastModified     | datetime     | Yes      | Last modification           |
| LastModifiedBy   | varchar      | Yes      | Last modifier               |

### Indexes

```sql
-- Composite index for user queries
CREATE INDEX IX_DeviceTokens_UserId ON DeviceTokens(UserId);

-- Index for token lookups
CREATE INDEX IX_DeviceTokens_Token ON DeviceTokens(Token);

-- Composite index for platform filtering
CREATE INDEX IX_DeviceTokens_UserId_Platform ON DeviceTokens(UserId, Platform);
```

## Integration with Notification Service

This integration is **already built and in production** — not a recommended pattern to implement. This section describes what actually runs today.

### `IdentityServiceClient` (Notification.Infrastructure/Services/IdentityServiceClient.cs)

Notification Service talks to Identity's device-token endpoints via `IIdentityServiceClient`, registered through `AddIdentityServiceClient<IIdentityServiceClient, IdentityServiceClient>(...)` (see `SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md`). It wraps all four device-token read/write calls needed by the notification pipeline:

| Method | Calls | Notes |
|---|---|---|
| `GetUserDeviceTokensAsync(userId, tenantId)` | `GET /api/v1/device-tokens/user/{userId}` | Per-user fallback path; result cached 5 minutes in `IMemoryCache` keyed by `device_tokens_{userId}_{tenantId}` |
| `GetBatchDeviceTokensAsync(userIds, tenantId)` | `POST /api/v1/device-tokens/batch` | Bulk path used by `NotificationProcessor` to fetch tokens for an entire tenant batch in one call instead of N calls |
| `GetTenantDeviceTokensAsync(tenantId)` | `GET /api/v1/device-tokens/tenant` | Used for tenant-wide broadcasts |
| `DeleteBatchDeviceTokensAsync(tokenIds, tenantId)` | `DELETE /api/v1/device-tokens/batch` | Automatic cleanup — see "Automatic Invalid-Token Cleanup" below |

### `NotificationProcessor.SendViaFirebaseAsync` (Notification.API/BackgroundServices/NotificationProcessor.cs)

This is the actual dispatch logic, handling three distinct notification scopes:

1. **Global** (no `UserId`, no `TenantId`) — calls `ITenantServiceClient.GetAllActiveTenantIdsAsync()`, then loops every active tenant **in parallel** (`Task.WhenAll`), calling `GetTenantDeviceTokensAsync(tenantId)` for each and sending to every device found.
2. **Tenant** (`TenantId` set, no `UserId`) — calls `GetTenantDeviceTokensAsync(tenantId)` once and sends to every device for that tenant.
3. **User** (`UserId` + `TenantId` set) — checks the batch-fetched `deviceTokensCache` (populated once per tenant-group by `GetBatchDeviceTokensAsync` before the per-item loop starts) first; on a cache miss, falls back to the single-user `GetUserDeviceTokensAsync` call.

All three scopes funnel into `IFirebaseService.SendToMultipleDevicesAsync(tokens, title, message, data, ct)`.

### Automatic Invalid-Token Cleanup

`SendToMultipleDevicesAsync`'s result includes `InvalidTokenIds` — tokens Firebase itself rejected (uninstalled app, expired registration, etc.). `SendViaFirebaseAsync` matches those back to `DeviceTokenDto.Id` and calls `DeleteBatchDeviceTokensAsync` immediately, so a dead token is removed from Identity's `DeviceTokens` table the same processing cycle it fails in — no separate cleanup job, no manual admin action, and no notification is ever retried against a token already known to be dead.

### Batch Fetch — Why It Exists

Before a tenant group's per-item send loop runs, `ProcessTenantGroupAsync` collects every distinct `UserId` in the batch (excluding SignalR-only deliveries) and calls `GetBatchDeviceTokensAsync` **once** for the whole group, populating `deviceTokensCache`. This is what keeps a 500-notification batch for one tenant from making up to 500 individual `GET /user/{userId}` calls to Identity — it's one `POST /batch` call instead.

### Primary Device Logic

- Only one primary device per platform per user (enforced by `AddDeviceTokenCommand`/`UpdateDeviceTokenCommand` in Identity)
- The current dispatch logic above sends to **all** of a user's/tenant's devices by default — it does not yet special-case "critical → primary device only" (see "Future Enhancements" below for this as an open item)

## Migration Guide

### From Old FirebaseToken to New DeviceToken

If you have existing `FirebaseToken` data in the `Users` table:

```sql
-- Migration script to move existing tokens
INSERT INTO DeviceTokens (UserId, Token, Platform, IsPrimary, Created, IsArchived, Status)
SELECT
    Id as UserId,
    FirebaseToken as Token,
    'Android' as Platform,  -- Assume Android, adjust as needed
    1 as IsPrimary,
    Created,
    0 as IsArchived,
    1 as Status
FROM Users
WHERE FirebaseToken IS NOT NULL AND FirebaseToken != '';
```

**Note:** The EF Core migration will drop the `FirebaseToken` column. Run the above script BEFORE applying the migration if you need to preserve data.

## Testing

### Integration Tests Location

`src/Services/Identity/Identity.API.Tests/Endpoints/DeviceTokenEndpointsTests.cs`

### Test Coverage

- Add device token with validation
- Get token by ID (found/not found)
- Get all user tokens
- Get tokens by platform
- Update token (success/not found)
- Delete token
- Delete all user tokens
- Primary flag handling (ensure only one primary per platform)
- Token uniqueness validation

## Best Practices

1. **Token Security:**

   - Never expose device tokens in public APIs
   - Validate user ownership before returning tokens
   - Log token access for security auditing

2. **Token Management:**

   - Set primary device automatically on first token
   - Clean up expired tokens periodically
   - Verify tokens before sending notifications

3. **Multi-Device Support:**

   - Allow multiple devices per user
   - Support all platforms (iOS, Android, Web)
   - Handle platform-specific token formats

4. **Performance:**
   - Cache frequently accessed tokens
   - Use indexes for fast lookups
   - Batch token retrievals when possible

## Future Enhancements

- [ ] Token expiration and auto-cleanup
- [ ] Token verification with FCM/APNs
- [ ] Device metadata (model, OS version, app version)
- [ ] Push notification preferences per device
- [ ] Token rotation and security auditing
- [ ] Primary-device-only routing for critical notifications (currently sends to all devices — see "Primary Device Logic" above)
- [ ] WebSocket-based token updates

(Batch token operations — `POST /batch` and `DELETE /batch` — are already built; see the Endpoints table and "Integration with Notification Service" above.)

## Related Documentation

- [Notification Service README](NOTIFICATION_SERVICE_README.md)
- [Service-to-Service Authentication Guide](SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md)
- [Database-Per-Tenant Architecture](DATABASE_PER_TENANT_ARCHITECTURE.md)

---

**Last Updated:** August 2026  
**Version:** 1.1.0 (Corrected against actual source: base URL and all request examples fixed to `/api/v1/device-tokens`; added the 3 missing endpoints — `POST /batch`, `DELETE /batch`, `GET /tenant`; rewrote "Integration with Notification Service" to describe the real, already-built `IdentityServiceClient`/`NotificationProcessor.SendViaFirebaseAsync` integration — batch fetch, 3 broadcast scenarios, automatic invalid-token cleanup — instead of presenting it as a DIY/recommended pattern)  
**Status:** ✅ Production Ready
