# Firebase Push Notifications Implementation Guide

**Version:** 1.0.0  
**Last Updated:** November 13, 2025  
**Status:** ✅ Production Ready

---

## 📋 Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Configuration](#configuration)
- [Device Token Flow](#device-token-flow)
- [Implementation Details](#implementation-details)
- [Testing](#testing)
- [Error Handling](#error-handling)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Overview

The Notification Service now supports Firebase Cloud Messaging (FCM) for sending push notifications to mobile devices. The implementation integrates with the Identity Service to retrieve device tokens and automatically handles invalid or expired tokens by removing them from the database.

### Key Features

✅ **Firebase Cloud Messaging Integration** - Send push notifications via FCM  
✅ **Device Token Management** - Retrieve tokens from Identity Service  
✅ **Automatic Token Cleanup** - Remove invalid/expired tokens  
✅ **Multi-Device Support** - Send to multiple devices per user  
✅ **Platform Support** - iOS (APNs), Android (FCM), Web (FCM)  
✅ **Batch Processing** - Efficient multicast messaging  
✅ **Error Handling** - Comprehensive logging and retry logic

---

## Architecture

### System Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    NOTIFICATION PROCESSOR                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  1. Fetch queue item with DeliveryType = Firebase or Both       │
│  2. Get user's device tokens from Identity Service              │
│  3. Send FCM notification to each device token                  │
│  4. Check delivery status for each token                        │
│  5. Remove invalid/expired tokens from Identity Service         │
│  6. Mark notification as Sent or Failed                         │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
         │                                        │
         ▼                                        ▼
┌──────────────────┐                    ┌──────────────────┐
│  Identity Service│                    │  Firebase FCM    │
│  Device Tokens   │                    │  Cloud Service   │
└──────────────────┘                    └──────────────────┘
```

### Component Architecture

```
NotificationProcessor (Background Service)
    │
    ├─→ IFirebaseService (Send to FCM)
    │     └─→ FirebaseAdmin SDK
    │
    ├─→ IIdentityServiceClient (Get/Delete tokens)
    │     └─→ HttpClient → Identity Service API
    │
    └─→ NotificationDbContext (Update queue status)
```

---

## Configuration

### 1. Firebase Configuration (appsettings.json)

Update the Notification Service `appsettings.json`:

```json
{
  "Firebase": {
    "Enabled": true,
    "ProjectId": "CHANGE_ME_FIREBASE_PROJECT_ID",
    "ServiceAccountKeyPath": "Firebase-Ihsan-SDK-Key.json"
  }
}
```

**Configuration Properties:**

- `Enabled` (bool): Enable/disable Firebase integration
- `ProjectId` (string): Firebase project ID from Firebase Console
- `ServiceAccountKeyPath` (string): Path to Firebase service account JSON file

### 2. Service Account Key File

Place the Firebase service account key file in the Notification.API project root:

```
src/Services/Notification/Notification.API/
    ├── Firebase-Ihsan-SDK-Key.json   ← Service account key
    ├── appsettings.json
    └── Program.cs
```

**How to get the service account key:**

1. Go to Firebase Console: https://console.firebase.google.com/
2. Select your project
3. Go to Project Settings → Service Accounts
4. Click "Generate new private key"
5. Save the JSON file as `Firebase-Ihsan-SDK-Key.json`

### 3. Identity Service Configuration

Ensure the Identity Service base URL is configured:

```json
{
  "IdentityService": {
    "BaseUrl": "http://localhost:5001"
  }
}
```

### 4. Service-to-Service Authentication

Configure shared secret for service-to-service communication:

```json
{
  "ServiceCommunication": {
    "Enabled": true,
    "ServiceName": "NotificationService",
    "SharedSecret": "your-shared-secret-here",
    "AllowedServices": ["IdentityService", "TenantService"]
  }
}
```

**Important:** The same `SharedSecret` must be configured in the Identity Service.

---

## Device Token Flow

### 1. Device Token Registration (Client → Identity Service)

```http
POST https://localhost:5001/api/device-tokens
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "userId": 1,
  "token": "fcm_device_token_here",
  "platform": 1,  // 0=iOS, 1=Android, 2=Web
  "deviceIdentifier": "android-device-123",
  "isPrimary": true
}
```

### 2. Notification Queuing (Any Service → Notification Service)

```http
POST https://localhost:5104/api/notifications/send
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "tenantId": "ihsandev",
  "userId": 1,
  "title": "New Message",
  "message": "You have a new message",
  "data": "{\"messageId\": 123}",
  "deliveryType": "Both",  // SignalR, Firebase, or Both
  "priority": "Immediate"
}
```

### 3. Background Processing (Notification Service)

```csharp
// NotificationProcessor automatically processes queue
1. Fetch pending notifications with DeliveryType = Firebase or Both
2. For each notification with userId:
   a. Call Identity Service: GET /api/device-tokens/user/{userId}
   b. Send FCM notification to each device token
   c. Check Firebase response for each token
   d. If token invalid/expired:
      - Call Identity Service: DELETE /api/device-tokens/{tokenId}
   e. Mark notification as Sent or Failed
```

### 4. Firebase Delivery

```
Notification Service → Firebase FCM → Mobile Device
                     ↓
            (Invalid Token Detected)
                     ↓
Notification Service → Identity Service (Delete Token)
```

---

## Implementation Details

### 1. Firebase Service (`IFirebaseService`)

**Location:** `Notification.Infrastructure/Services/FirebaseService.cs`

**Key Methods:**

- `SendToDeviceAsync()` - Send to single device
- `SendToMultipleDevicesAsync()` - Send to multiple devices (batch)
- `IsEnabled` - Check if Firebase is enabled

**Features:**

- Automatic Firebase app initialization
- Token masking in logs (security)
- Error code handling (Unregistered, InvalidArgument)
- Multicast messaging for efficiency

**Example Usage:**

```csharp
var firebaseService = serviceProvider.GetRequiredService<IFirebaseService>();

if (firebaseService.IsEnabled)
{
    var data = new Dictionary<string, string>
    {
        { "notificationId", "123" },
        { "type", "message" }
    };

    var success = await firebaseService.SendToDeviceAsync(
        deviceToken: "fcm_token_here",
        title: "New Message",
        message: "You have a new message",
        data: data);

    if (!success)
    {
        // Token is invalid, remove from database
    }
}
```

### 2. Identity Service Client (`IIdentityServiceClient`)

**Location:** `Notification.Infrastructure/Services/IdentityServiceClient.cs`

**Key Methods:**

- `GetUserDeviceTokensAsync()` - Get all device tokens for user
- `DeleteDeviceTokenAsync()` - Delete invalid token

**Features:**

- Service-to-service authentication (X-Service-Secret header)
- Multi-tenancy support (x-tenant-id header)
- Comprehensive error logging
- Automatic retry on transient failures

**Example Usage:**

```csharp
var identityClient = serviceProvider.GetRequiredService<IIdentityServiceClient>();

// Get device tokens
var tokens = await identityClient.GetUserDeviceTokensAsync(
    userId: 1,
    tenantId: "ihsandev");

// Delete invalid token
var deleted = await identityClient.DeleteDeviceTokenAsync(
    tokenId: 123,
    tenantId: "ihsandev");
```

### 3. Notification Processor Integration

**Location:** `Notification.API/BackgroundServices/NotificationProcessor.cs`

**Updated Method:** `SendViaFirebaseAsync()`

**Flow:**

```csharp
1. Check if Firebase is enabled
2. Check if notification has userId
3. Get device tokens from Identity Service
4. Prepare FCM payload with custom data
5. Send to all devices using multicast
6. Process results:
   - Log success for each device
   - Collect invalid token IDs
7. Delete invalid tokens from Identity Service
8. Log summary (success count, failure count)
```

**Key Features:**

- Skips notifications without userId (can't send to devices without user context)
- Handles empty device token list gracefully
- Efficient batch processing with multicast
- Automatic cleanup of invalid tokens
- Detailed logging for troubleshooting

---

## Testing

### 1. Manual Testing Steps

#### Step 1: Register Device Token

```bash
# Login to get JWT token
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }'

# Register device token
curl -X POST "https://localhost:5001/api/device-tokens" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {jwt_token}" \
  -d '{
    "userId": 1,
    "token": "test_fcm_token_android",
    "platform": 1,
    "deviceIdentifier": "android-test-device",
    "isPrimary": true
  }'
```

#### Step 2: Send Notification

```bash
curl -X POST "https://localhost:5104/api/notifications/send" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {jwt_token}" \
  -d '{
    "tenantId": "ihsandev",
    "userId": 1,
    "title": "Test Notification",
    "message": "This is a test Firebase notification",
    "data": "{\"testKey\": \"testValue\"}",
    "deliveryType": "Both",
    "priority": "Immediate"
  }'
```

#### Step 3: Check Logs

Look for these log entries in Notification Service:

```
[INFO] Retrieved 1 device tokens for user 1 in tenant ihsandev
[INFO] Sending Firebase notification to 1 device(s) for user 1 - QueueItemId={Id}
[INFO] Firebase notification sent successfully. MessageId: {MessageId}, Token: test_fcm...vice
[INFO] Firebase notification completed - Success: 1, Failed: 0, QueueItemId={Id}
```

### 2. Integration Testing

**Test Invalid Token:**

```bash
# Register invalid token
curl -X POST "https://localhost:5001/api/device-tokens" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {jwt_token}" \
  -d '{
    "userId": 1,
    "token": "invalid_token_12345",
    "platform": 1,
    "isPrimary": true
  }'

# Send notification
curl -X POST "https://localhost:5104/api/notifications/send" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {jwt_token}" \
  -d '{
    "userId": 1,
    "title": "Test",
    "message": "Test message",
    "deliveryType": "Firebase",
    "priority": "Immediate"
  }'

# Check logs for automatic token deletion
# [WARN] Invalid or unregistered device token: invalid_...2345. Error: Unregistered
# [INFO] Removing 1 invalid/expired device tokens for user 1
# [INFO] Deleted invalid device token 123 for user 1
```

### 3. End-to-End Testing with Mobile App

1. **Register Device Token:**

   - Mobile app obtains FCM token
   - App sends token to Identity Service on login/startup

2. **Send Test Notification:**

   - Use Swagger UI or Postman
   - Send notification with `deliveryType: "Firebase"`

3. **Verify Delivery:**
   - Check device receives push notification
   - Check Firebase Console → Cloud Messaging → Reports

---

## Error Handling

### 1. Firebase Errors

| Error Code        | Description                              | Action                     |
| ----------------- | ---------------------------------------- | -------------------------- |
| `Unregistered`    | Device token is invalid or expired       | Delete token from database |
| `InvalidArgument` | Token format is invalid                  | Delete token from database |
| `Unavailable`     | Firebase service temporarily unavailable | Retry (handled by queue)   |
| `Internal`        | Firebase internal error                  | Retry (handled by queue)   |

### 2. Identity Service Errors

| Status Code | Description                          | Action                          |
| ----------- | ------------------------------------ | ------------------------------- |
| `404`       | Device token not found               | Skip deletion (already removed) |
| `401`       | Unauthorized (invalid shared secret) | Check service configuration     |
| `500`       | Identity Service error               | Log error, continue processing  |

### 3. Queue Processing Errors

All errors in Firebase/Identity Service calls are caught and logged. The notification will be retried up to 3 times with exponential backoff:

- **Attempt 1:** Immediate
- **Attempt 2:** After 30 seconds
- **Attempt 3:** After 60 seconds
- **After 3 attempts:** Marked as Failed

---

## Best Practices

### 1. Device Token Management

✅ **DO:**

- Register device token on app startup and login
- Update token when FCM token changes (onTokenRefresh)
- Delete token on logout
- Set one device as primary per user per platform

❌ **DON'T:**

- Store device tokens in client-side storage
- Share device tokens between users
- Forget to handle token refresh

### 2. Notification Delivery

✅ **DO:**

- Use `DeliveryType: "Both"` for maximum reach (SignalR + Firebase)
- Set appropriate priority (Immediate for critical, Waitable for non-urgent)
- Include meaningful data payload for app routing
- Test with real device tokens before production

❌ **DON'T:**

- Send Firebase notifications without userId
- Assume all users have device tokens
- Send too many notifications (respect user preferences)

### 3. Firebase Configuration

✅ **DO:**

- Keep service account key file secure (never commit to Git)
- Use environment variables in production
- Monitor Firebase Console for delivery metrics
- Set up Firebase Analytics for insights

❌ **DON'T:**

- Expose service account key in logs or responses
- Use same Firebase project for dev and production
- Ignore Firebase quota limits

### 4. Error Handling

✅ **DO:**

- Log all Firebase errors with context
- Implement automatic token cleanup
- Monitor invalid token rate
- Set up alerts for high failure rate

❌ **DON'T:**

- Ignore token validation errors
- Retry indefinitely on permanent failures
- Log sensitive token data

---

## Troubleshooting

### Problem: Firebase notifications not sending

**Possible Causes:**

1. **Firebase not enabled**

   ```json
   "Firebase": { "Enabled": false }
   ```

   **Solution:** Set `"Enabled": true` in appsettings.json

2. **Service account key file not found**

   ```
   Error: Failed to initialize Firebase. File not found: Firebase-Ihsan-SDK-Key.json
   ```

   **Solution:** Copy service account key file to Notification.API project root

3. **Invalid project ID**
   ```
   Error: Failed to initialize Firebase. Invalid project ID
   ```
   **Solution:** Verify `ProjectId` matches Firebase Console

### Problem: Device tokens not found

**Possible Causes:**

1. **Identity Service not running**

   ```
   [ERROR] HTTP error getting device tokens for user 1
   ```

   **Solution:** Start Identity Service on port 5001

2. **Invalid shared secret**

   ```
   [WARN] Failed to get device tokens for user 1. Status: 401
   ```

   **Solution:** Verify `SharedSecret` matches in both services

3. **No tokens registered**
   ```
   [INFO] Retrieved 0 device tokens for user 1
   ```
   **Solution:** Register device token via Identity Service API

### Problem: Tokens not being deleted

**Possible Causes:**

1. **Identity Service client not registered**

   ```
   [WARN] Identity Service client not registered
   ```

   **Solution:** Verify Program.cs has `AddHttpClient<IIdentityServiceClient, IdentityServiceClient>()`

2. **Service-to-service auth failing**
   ```
   [ERROR] Failed to delete device token 123. Status: 401
   ```
   **Solution:** Check `X-Service-Secret` header is correct

### Problem: High failure rate

**Check:**

1. **Firebase Console → Cloud Messaging → Reports**

   - Look for error patterns
   - Check token format

2. **Notification Service Logs**

   ```bash
   # Search for failures
   grep "Firebase notification failed" Logs/Notification/*.log
   ```

3. **Identity Service Logs**
   ```bash
   # Check token registrations
   grep "device-tokens" Logs/Identity/*.log
   ```

---

## Related Documentation

- [Device Token Management Guide](DEVICE_TOKEN_MANAGEMENT_GUIDE.md)
- [Notification Service README](NOTIFICATION_SERVICE_README.md)
- [Service-to-Service Authentication](SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md)
- [Firebase Cloud Messaging Docs](https://firebase.google.com/docs/cloud-messaging)

---

## Summary

Firebase push notifications are now fully integrated with the Notification Service. The implementation:

✅ Retrieves device tokens from Identity Service  
✅ Sends push notifications via Firebase Cloud Messaging  
✅ Automatically removes invalid/expired tokens  
✅ Supports multi-device and multi-platform delivery  
✅ Includes comprehensive error handling and logging  
✅ Integrates seamlessly with existing queue processing

**Next Steps:**

1. Test with real mobile devices
2. Monitor Firebase Console metrics
3. Implement notification preferences
4. Add notification templates
5. Set up Firebase Analytics

---

**Last Updated:** November 13, 2025  
**Version:** 1.0.0  
**Status:** ✅ Production Ready
