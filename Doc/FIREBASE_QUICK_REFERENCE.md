# Firebase Push Notifications - Quick Reference

**Status:** ✅ Production Ready

---

## 🚀 Quick Start

### 1. Configuration (Already Done ✅)

```json
{
  "Firebase": {
    "Enabled": true,
    "ProjectId": "CHANGE_ME_FIREBASE_PROJECT_ID",
    "ServiceAccountKeyPath": "Firebase-Ihsan-SDK-Key.json"
  }
}
```

### 2. Register Device Token

```bash
POST https://localhost:5001/api/device-tokens
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "userId": 1,
  "token": "fcm_device_token_here",
  "platform": 1,
  "isPrimary": true
}
```

### 3. Send Notification

```bash
POST https://localhost:5104/api/notifications/send
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "tenantId": "ihsandev",
  "userId": 1,
  "title": "New Message",
  "message": "You have a new message",
  "deliveryType": "Both",
  "priority": "Immediate"
}
```

---

## 📊 Platform Values

| Platform | Value | Description           |
| -------- | ----- | --------------------- |
| iOS      | 0     | Apple devices (APNs)  |
| Android  | 1     | Android devices (FCM) |
| Web      | 2     | Web browsers (FCM)    |

---

## 📦 Delivery Types

| Type     | Value      | Description                      |
| -------- | ---------- | -------------------------------- |
| SignalR  | "SignalR"  | Real-time only (connected users) |
| Firebase | "Firebase" | Push notification only           |
| Both     | "Both"     | SignalR + Firebase (recommended) |

---

## 🔄 How It Works

```
Client App
   ↓ (registers FCM token)
Identity Service
   ↓ (stores token)
Notification Queue
   ↓ (processes)
NotificationProcessor
   ↓ (gets tokens)
Identity Service
   ↓ (sends notification)
Firebase FCM
   ↓ (delivers)
Mobile Device
```

---

## 🧪 Testing

### Test Invalid Token

```bash
# 1. Register invalid token
POST https://localhost:5001/api/device-tokens
{
  "userId": 1,
  "token": "invalid_token_12345",
  "platform": 1
}

# 2. Send notification
POST https://localhost:5104/api/notifications/send
{
  "userId": 1,
  "title": "Test",
  "message": "Test",
  "deliveryType": "Firebase"
}

# 3. Check logs for automatic deletion
# [INFO] Deleted invalid device token 123 for user 1
```

---

## 📝 Logs to Monitor

### Success

```
[INFO] Retrieved 2 device tokens for user 1 in tenant ihsandev
[INFO] Sending Firebase notification to 2 device(s) for user 1
[INFO] Firebase notification sent successfully. MessageId: abc123
[INFO] Firebase notification completed - Success: 2, Failed: 0
```

### Invalid Token

```
[WARN] Invalid or unregistered device token: fcm_abc1...xyz9
[INFO] Removing 1 invalid/expired device tokens for user 1
[INFO] Deleted invalid device token 123 for user 1
```

---

## 🔥 Common Issues

### Issue: No notifications sent

**Check:**

1. Firebase.Enabled = true?
2. Service account key file exists?
3. Identity Service running?
4. Device tokens registered?

**Logs:**

```bash
# Notification Service
grep "Firebase" Logs/Notification/*.log

# Identity Service
grep "device-tokens" Logs/Identity/*.log
```

### Issue: Token not deleted

**Check:**

1. ServiceCommunication.SharedSecret matches?
2. Identity Service accessible?

---

## 📚 Documentation

- **Complete Guide:** [FIREBASE_PUSH_NOTIFICATIONS_GUIDE.md](FIREBASE_PUSH_NOTIFICATIONS_GUIDE.md)
- **Device Tokens:** [DEVICE_TOKEN_MANAGEMENT_GUIDE.md](DEVICE_TOKEN_MANAGEMENT_GUIDE.md)
- **Notification Service:** [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md)

---

## 🎯 Next Steps

1. Test with real device token from mobile app
2. Monitor Firebase Console → Cloud Messaging → Reports
3. Set up Firebase Analytics (optional)
4. Configure notification preferences per user
5. Implement notification templates

---

**Implementation Complete!** 🎉
