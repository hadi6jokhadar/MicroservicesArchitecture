# Firebase Push Notification Flow - Complete Journey

**Version:** 1.0.0  
**Last Updated:** November 13, 2025

---

## 📋 Complete Flow: Send → Client

This document shows the **complete end-to-end flow** of how a push notification travels from the moment it's sent until it reaches the client's mobile device.

---

## 🎯 Overview

```
Service/API → Notification Service → Queue → Background Processor
→ Identity Service → Firebase Cloud → Mobile Device
```

**Total Time:** ~2-5 seconds (depending on queue processing interval)

---

## 📊 Detailed Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          PHASE 1: NOTIFICATION CREATION                  │
└─────────────────────────────────────────────────────────────────────────┘

[1] CLIENT/SERVICE SENDS REQUEST
    │
    │  POST /api/notifications/send
    │  {
    │    "tenantId": "ihsandev",
    │    "userId": 1,
    │    "title": "New Message",
    │    "message": "You have a new message from John",
    │    "deliveryType": "Both",  ← SignalR + Firebase
    │    "priority": "Immediate"
    │  }
    │
    ▼
[2] NOTIFICATION SERVICE API
    │
    ├─→ Validates request (FluentValidation)
    │   • Title required, max 200 chars
    │   • UserId provided → TenantId required
    │   • DeliveryType valid enum
    │
    ├─→ Creates NotificationQueueItem
    │   {
    │     Id: 12345,
    │     TenantId: "ihsandev",
    │     UserId: 1,
    │     Title: "New Message",
    │     Message: "You have a new message from John",
    │     DeliveryType: Both (3),
    │     Priority: Immediate (1),
    │     QueueStatus: Pending (0),
    │     Created: 2025-11-13T10:00:00Z,
    │     ExpiresAt: 2025-11-20T10:00:00Z
    │   }
    │
    ├─→ Saves to Global Queue Database
    │   INSERT INTO NotificationQueue (...)
    │
    └─→ Returns Response
        {
          "queueItemId": 12345,
          "status": "Pending"
        }

┌─────────────────────────────────────────────────────────────────────────┐
│                     PHASE 2: BACKGROUND PROCESSING                       │
│                         (Runs every 2-5 seconds)                         │
└─────────────────────────────────────────────────────────────────────────┘

[3] NOTIFICATION PROCESSOR (Background Service)
    │
    ├─→ Timer triggers (every 2 seconds)
    │
    ├─→ Queries Global Queue Database
    │   SELECT * FROM NotificationQueue
    │   WHERE QueueStatus = Pending
    │     AND ExpiresAt > NOW()
    │     AND (NextRetryAt IS NULL OR NextRetryAt <= NOW())
    │   ORDER BY Priority, Created
    │   LIMIT 500  ← Dynamic batch sizing
    │
    ├─→ Fetches pending items
    │   Found: QueueItem #12345
    │
    ├─→ Marks as Processing
    │   UPDATE NotificationQueue
    │   SET QueueStatus = Processing
    │   WHERE Id = 12345
    │
    └─→ Processes notification
        ↓

[4] DELIVERY TYPE CHECK
    │
    ├─→ DeliveryType = Both
    │   ├─→ Send via SignalR ✓
    │   └─→ Send via Firebase ✓
    │
    └─→ Continue to both delivery channels...

┌─────────────────────────────────────────────────────────────────────────┐
│                    PHASE 3A: SIGNALR DELIVERY (Real-time)                │
└─────────────────────────────────────────────────────────────────────────┘

[5] SEND VIA SIGNALR
    │
    ├─→ Determines target group
    │   tenantId: "ihsandev" + userId: 1
    │   → Group: "tenant:ihsandev:user:1"
    │
    ├─→ Sends to SignalR Hub
    │   await hubContext.Clients
    │     .Group("tenant:ihsandev:user:1")
    │     .SendAsync("ReceiveNotification", {
    │       queueItemId: 12345,
    │       title: "New Message",
    │       message: "You have a new message from John",
    │       created: "2025-11-13T10:00:00Z"
    │     });
    │
    └─→ SUCCESS: Sent to all connected SignalR clients
        (If user is online with WebSocket connection)

┌─────────────────────────────────────────────────────────────────────────┐
│                  PHASE 3B: FIREBASE DELIVERY (Push Notification)         │
└─────────────────────────────────────────────────────────────────────────┘

[6] SEND VIA FIREBASE - Step 1: Get Device Tokens
    │
    ├─→ Check if userId exists
    │   userId = 1 ✓
    │
    ├─→ Call Identity Service
    │   GET https://localhost:5001/api/device-tokens/user/1
    │   Headers: {
    │     "x-tenant-id": "ihsandev",
    │     "X-Service-Secret": "shared-secret-here"
    │   }
    │
    └─→ Identity Service Returns
        [
          {
            "id": 101,
            "userId": 1,
            "token": "fcm_android_token_abc123xyz...",
            "platform": 1,  // Android
            "deviceIdentifier": "samsung-s21",
            "isPrimary": true
          },
          {
            "id": 102,
            "userId": 1,
            "token": "fcm_ios_token_def456uvw...",
            "platform": 0,  // iOS
            "deviceIdentifier": "iphone-13",
            "isPrimary": false
          }
        ]

        Total: 2 device tokens found

[7] SEND VIA FIREBASE - Step 2: Prepare FCM Message
    │
    ├─→ Extract tokens from device list
    │   tokens = [
    │     "fcm_android_token_abc123xyz...",
    │     "fcm_ios_token_def456uvw..."
    │   ]
    │
    ├─→ Prepare data payload
    │   data = {
    │     "queueItemId": "12345",
    │     "notificationId": "0",
    │     "tenantId": "ihsandev",
    │     "userId": "1",
    │     "priority": "Immediate",
    │     "customData": "{}" // Optional custom data
    │   }
    │
    └─→ Create FCM MulticastMessage
        {
          tokens: ["fcm_android...", "fcm_ios..."],
          notification: {
            title: "New Message",
            body: "You have a new message from John"
          },
          data: { ...data }
        }

[8] SEND VIA FIREBASE - Step 3: Send to Firebase Cloud
    │
    ├─→ Call Firebase Admin SDK
    │   await FirebaseMessaging.SendEachForMulticastAsync(
    │     multicastMessage,
    │     cancellationToken
    │   )
    │
    ├─→ Firebase Cloud Processes
    │   ├─→ Android Token → Google FCM → Android Device
    │   └─→ iOS Token → Apple APNs → iOS Device
    │
    └─→ Firebase Returns Response
        {
          successCount: 2,
          failureCount: 0,
          responses: [
            {
              isSuccess: true,
              messageId: "projects/ihsandev/messages/0:123456",
              exception: null
            },
            {
              isSuccess: true,
              messageId: "projects/ihsandev/messages/0:789012",
              exception: null
            }
          ]
        }

[9] SEND VIA FIREBASE - Step 4: Process Results
    │
    ├─→ Check each response
    │
    │   Token 1 (Android): ✓ SUCCESS
    │   Token 2 (iOS): ✓ SUCCESS
    │
    │   Success: 2, Failed: 0
    │
    ├─→ Handle failures (if any)
    │   │
    │   ├─→ If ErrorCode = Unregistered OR InvalidArgument
    │   │   ├─→ Call Identity Service to delete token
    │   │   │   DELETE /api/device-tokens/{tokenId}
    │   │   │
    │   │   └─→ Log: "Deleted invalid device token"
    │   │
    │   └─→ If other error (Unavailable, Internal)
    │       └─→ Will retry (queue retry mechanism)
    │
    └─→ Log Summary
        "Firebase notification completed - Success: 2, Failed: 0"

┌─────────────────────────────────────────────────────────────────────────┐
│                     PHASE 4: PERSIST TO TENANT DATABASE                  │
└─────────────────────────────────────────────────────────────────────────┘

[10] PERSIST NOTIFICATION
     │
     ├─→ Set Tenant Context
     │   tenantContext.SetTenant("ihsandev")
     │
     ├─→ Save to Tenant Database
     │   INSERT INTO Notifications (
     │     UserId: 1,
     │     Title: "New Message",
     │     Message: "You have a new message from John",
     │     IsRead: false,
     │     QueueItemId: 12345,
     │     Created: 2025-11-13T10:00:00Z
     │   )
     │
     └─→ Returns NotificationId
         notificationId: 456

┌─────────────────────────────────────────────────────────────────────────┐
│                      PHASE 5: UPDATE QUEUE STATUS                        │
└─────────────────────────────────────────────────────────────────────────┘

[11] MARK AS SENT
     │
     ├─→ Update Global Queue
     │   UPDATE NotificationQueue
     │   SET QueueStatus = Sent,
     │       NotificationId = 456,
     │       ProcessedAt = NOW(),
     │       LastModified = NOW()
     │   WHERE Id = 12345
     │
     └─→ Log Success
         "Notification processed: QueueItemId=12345"

┌─────────────────────────────────────────────────────────────────────────┐
│                       PHASE 6: CLIENT RECEIVES                           │
└─────────────────────────────────────────────────────────────────────────┘

[12] SIGNALR CLIENT (Web/Desktop - If Online)
     │
     ├─→ WebSocket connection receives event
     │   connection.on("ReceiveNotification", (notification) => {
     │     console.log("Received:", notification);
     │   });
     │
     ├─→ Display notification in UI
     │   showNotification({
     │     title: "New Message",
     │     message: "You have a new message from John"
     │   });
     │
     └─→ Acknowledge delivery (optional)
         await connection.invoke("AcknowledgeDelivery", 12345);

[13] MOBILE CLIENT (Android/iOS - Always)
     │
     ├─→ Firebase Cloud Messaging delivers to device
     │
     │   Android Device:
     │   ├─→ Google FCM → Android OS → App
     │   └─→ Shows system notification
     │       ┌─────────────────────────────┐
     │       │ 📱 New Message              │
     │       │ You have a new message from │
     │       │ John                        │
     │       └─────────────────────────────┘
     │
     │   iOS Device:
     │   ├─→ Apple APNs → iOS OS → App
     │   └─→ Shows system notification
     │       ┌─────────────────────────────┐
     │       │ 🍎 New Message              │
     │       │ You have a new message from │
     │       │ John                        │
     │       └─────────────────────────────┘
     │
     ├─→ User taps notification
     │
     ├─→ App opens with notification data
     │   {
     │     "queueItemId": "12345",
     │     "tenantId": "ihsandev",
     │     "userId": "1",
     │     "customData": "{}"
     │   }
     │
     └─→ App navigates to relevant screen
         (e.g., Open message from John)
```

---

## 🔄 Error Handling & Retry Flow

### Invalid Token Scenario

```
[SEND VIA FIREBASE] Step 3: Send to Firebase Cloud
│
├─→ Firebase Returns Error
│   {
│     isSuccess: false,
│     exception: {
│       messagingErrorCode: "Unregistered",
│       message: "Requested entity was not found."
│     }
│   }
│
├─→ [AUTOMATIC CLEANUP]
│   │
│   ├─→ Log Warning
│   │   "Invalid or unregistered device token: fcm_andr...xyz"
│   │
│   ├─→ Add to invalid tokens list
│   │   invalidTokenIds = [101]
│   │
│   ├─→ Call Identity Service
│   │   DELETE /api/device-tokens/101
│   │   Headers: {
│   │     "x-tenant-id": "ihsandev",
│   │     "X-Service-Secret": "shared-secret-here"
│   │   }
│   │
│   └─→ Log Success
│       "Deleted invalid device token 101 for user 1"
│
└─→ Continue with remaining valid tokens
    Other tokens still receive notification ✓
```

### Queue Retry Scenario

```
[SEND VIA FIREBASE] Error Occurred
│
├─→ Exception caught
│   "Failed to send Firebase notification"
│
├─→ Increment Retry Count
│   retryCount = 1
│
├─→ Calculate Next Retry Time
│   Exponential Backoff:
│   Retry 1: NOW + 30 seconds
│   Retry 2: NOW + 60 seconds
│   Retry 3: NOW + 120 seconds
│
├─→ Update Queue Status
│   UPDATE NotificationQueue
│   SET QueueStatus = Pending,
│       RetryCount = 1,
│       NextRetryAt = NOW + 30 seconds
│   WHERE Id = 12345
│
└─→ Will retry after 30 seconds
    Background processor picks it up again
```

---

## ⏱️ Timeline Example

**Real-time scenario with all steps:**

```
T+0.000s  : Client sends POST /api/notifications/send
T+0.050s  : Notification saved to queue database
T+0.051s  : API returns response { "queueItemId": 12345 }

⏰ Background processor next tick...

T+2.000s  : Processor fetches pending items (finds #12345)
T+2.010s  : Marks as Processing
T+2.020s  : Sends via SignalR → Connected clients receive instantly ✓
T+2.030s  : Calls Identity Service → GET device tokens
T+2.150s  : Identity Service returns 2 tokens
T+2.160s  : Prepares FCM multicast message
T+2.170s  : Calls Firebase Admin SDK
T+2.800s  : Firebase Cloud sends to Google FCM + Apple APNs
T+3.200s  : Android device receives push notification 📱
T+3.300s  : iOS device receives push notification 🍎
T+3.310s  : Firebase returns success response
T+3.320s  : Saves to tenant database
T+3.350s  : Updates queue status to Sent
T+3.360s  : Process complete ✅

Total Time: ~3.4 seconds from API call to device delivery
```

---

## 🎯 Key Components Involved

### 1. Notification Service API

- **Location:** `Notification.API`
- **Responsibility:** Receive requests, validate, queue notifications
- **Endpoint:** `POST /api/notifications/send`

### 2. Global Queue Database

- **Location:** PostgreSQL (Shared database)
- **Table:** `NotificationQueue`
- **Responsibility:** Store pending notifications across all tenants

### 3. Background Processor

- **Location:** `Notification.API/BackgroundServices/NotificationProcessor.cs`
- **Responsibility:** Process queue every 2-5 seconds
- **Features:** Dynamic batching, parallel processing, retry logic

### 4. Identity Service

- **Location:** `Identity.API`
- **Endpoint:** `GET /api/device-tokens/user/{userId}`
- **Responsibility:** Store and retrieve device tokens

### 5. Firebase Service

- **Location:** `Notification.Infrastructure/Services/FirebaseService.cs`
- **Responsibility:** Send FCM notifications via Firebase Admin SDK
- **Integration:** Firebase Cloud Messaging → Google FCM / Apple APNs

### 6. Tenant Database

- **Location:** Per-tenant PostgreSQL database
- **Table:** `Notifications`
- **Responsibility:** Store notification history per tenant

---

## 📱 Client Integration

### Web Client (SignalR)

```typescript
// Connect to hub
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:5104/hubs/notifications", {
    accessTokenFactory: () => getJwtToken(),
  })
  .withAutomaticReconnect()
  .build();

// Listen for notifications
connection.on("ReceiveNotification", (notification) => {
  // Show notification to user
  showNotification(notification.title, notification.message);

  // Acknowledge receipt
  connection.invoke("AcknowledgeDelivery", notification.queueItemId);
});

// Start connection
await connection.start();
```

### Mobile Client (Firebase)

**Android (Kotlin):**

```kotlin
class MyFirebaseMessagingService : FirebaseMessagingService() {
    override fun onMessageReceived(remoteMessage: RemoteMessage) {
        val title = remoteMessage.notification?.title
        val message = remoteMessage.notification?.body
        val data = remoteMessage.data

        // Show notification
        showNotification(title, message)

        // Handle data payload
        val queueItemId = data["queueItemId"]
        val tenantId = data["tenantId"]
        val userId = data["userId"]
    }

    override fun onNewToken(token: String) {
        // Send new token to Identity Service
        registerDeviceToken(token, Platform.ANDROID)
    }
}
```

**iOS (Swift):**

```swift
func application(_ application: UIApplication,
                didReceiveRemoteNotification userInfo: [AnyHashable: Any]) {
    let title = userInfo["title"] as? String
    let message = userInfo["message"] as? String
    let data = userInfo["data"] as? [String: Any]

    // Show notification
    showNotification(title: title, message: message)

    // Handle data payload
    let queueItemId = data?["queueItemId"] as? String
    let tenantId = data?["tenantId"] as? String
    let userId = data?["userId"] as? String
}

func application(_ application: UIApplication,
                didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
    let token = deviceToken.map { String(format: "%02.2hhx", $0) }.joined()

    // Send token to Identity Service
    registerDeviceToken(token: token, platform: .iOS)
}
```

---

## 🔍 Monitoring & Debugging

### Check Queue Status

```sql
-- Check pending notifications
SELECT * FROM "NotificationQueue"
WHERE "QueueStatus" = 0
ORDER BY "Created" DESC
LIMIT 10;

-- Check failed notifications
SELECT * FROM "NotificationQueue"
WHERE "QueueStatus" = 3
ORDER BY "Created" DESC
LIMIT 10;
```

### Check Device Tokens

```sql
-- Check user's device tokens
SELECT * FROM "DeviceTokens"
WHERE "UserId" = 1 AND "IsArchived" = false;
```

### Check Notification History

```sql
-- Check user's notifications (tenant database)
SELECT * FROM "Notifications"
WHERE "UserId" = 1
ORDER BY "Created" DESC
LIMIT 10;
```

### Logs to Monitor

**Notification Service:**

```
[INFO] Retrieved 2 device tokens for user 1 in tenant ihsandev
[INFO] Sending Firebase notification to 2 device(s) for user 1
[INFO] Firebase notification sent successfully. MessageId: 0:123456
[INFO] Firebase notification completed - Success: 2, Failed: 0
[INFO] Notification persisted to tenant DB: NotificationId=456
[INFO] Notification processed: QueueItemId=12345
```

**Identity Service:**

```
[INFO] Device token registered: UserId=1, Platform=Android
[INFO] Retrieved device tokens for user 1: Count=2
[INFO] Deleted device token 101 for user 1
```

---

## 🎉 Summary

This is the **complete end-to-end flow** of how notifications work:

1. ✅ **Request sent** → Notification Service API
2. ✅ **Queued** → Global database
3. ✅ **Processed** → Background service (every 2-5 seconds)
4. ✅ **SignalR delivery** → Real-time to connected clients
5. ✅ **Device tokens fetched** → From Identity Service
6. ✅ **Firebase delivery** → Via Firebase Cloud to mobile devices
7. ✅ **Invalid tokens cleaned** → Automatically removed
8. ✅ **Persisted** → Tenant database for history
9. ✅ **Queue updated** → Marked as Sent
10. ✅ **Client receives** → Web (SignalR) + Mobile (FCM)

**Total latency:** ~2-5 seconds from send to device delivery! 🚀

---

_Last Updated: November 13, 2025_
