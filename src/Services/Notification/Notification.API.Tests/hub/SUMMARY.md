# Notification Hub Test Page - Summary

## 📁 Files Created

Created in `src/Services/Notification/Notification.API/test/`:

1. **hub-test.html** - Main test page
2. **styles.css** - Styling and animations
3. **script.js** - SignalR logic and interactions
4. **README.md** - Complete documentation

## 🎯 Key Features

### Section 1: SuperAdmin - Send Notifications
- **Purpose**: Send notifications to various targets
- **Capabilities**:
  - ✅ Global broadcast (all users, all tenants)
  - ✅ Tenant-wide broadcast (all users in specific tenant)
  - ✅ User-specific (specific user in specific tenant)
- **Inputs**: JWT token, tenant ID, user ID, title, message, priority, delivery type, additional data

### Section 2 & 3: User Listeners
- **Purpose**: Connect multiple users to test real-time notification delivery
- **Capabilities**:
  - ✅ Authenticated connection (with JWT)
  - ✅ Anonymous connection (without JWT)
  - ✅ Tenant-scoped connection (with x-tenant-id header)
  - ✅ Global-only connection (no tenant)
  - ✅ Automatic acknowledgment for authenticated users
  - ✅ Real-time connection status
- **Display**: Shows all received notifications with metadata

## 🔥 Testing Scenarios

### 1. Global Broadcast
```
SuperAdmin → Send Global (no tenantId, no userId)
Result → All connected users receive notification
```

### 2. Tenant Broadcast
```
SuperAdmin → Send to Tenant (tenantId="acme-corp", no userId)
Result → Only users connected to "acme-corp" tenant receive
```

### 3. User-Specific
```
SuperAdmin → Send to User (tenantId="acme-corp", userId=5)
Result → Only user 5 in "acme-corp" tenant receives
```

### 4. Anonymous vs Authenticated
```
User 1 → Anonymous connection
User 2 → Authenticated connection
SuperAdmin → Send user-specific notification
Result → Only authenticated user receives (anonymous cannot get user-specific)
```

## 🎨 UI Highlights

- **Modern Design**: Gradient backgrounds, smooth animations
- **Responsive**: Works on desktop and mobile
- **Real-time Updates**: 
  - Connection status (Connected/Disconnected/Connecting)
  - Animated notification cards
  - Auto-scroll to latest
- **Visual Feedback**:
  - Color-coded sections (Admin=Pink, Users=Blue, Config=Orange)
  - Status messages (Success/Error/Info)
  - Browser desktop notifications

## 🛠️ Technical Implementation

### SignalR Connection
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
        accessTokenFactory: () => token || null
    })
    .withAutomaticReconnect()
    .build();

// Add tenant header
connection.headers = { "x-tenant-id": tenantId };

// Listen for notifications
connection.on("ReceiveNotification", (notification) => {
    // Display notification
    // Acknowledge if authenticated
});
```

### Send Notification
```javascript
fetch('/api/notifications/send', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
        'x-tenant-id': tenantId
    },
    body: JSON.stringify({
        tenantId: "acme-corp",  // or null for global
        userId: 5,              // or null for broadcast
        title: "Test",
        message: "Hello",
        priority: 0,            // Immediate
        deliveryType: 0         // SignalR
    })
});
```

## 📋 Usage Instructions

### Quick Start
1. Open `hub-test.html` in browser
2. **Section 1**: Enter SuperAdmin JWT token
3. **Section 2**: Connect User 1 (optionally with JWT and tenant ID)
4. **Section 3**: Connect User 2 (different configuration)
5. **Section 1**: Send notifications and watch them appear in real-time!

### Configuration
- **Hub URL**: `https://localhost:5104/hubs/notifications`
- **API URL**: `https://localhost:5104/api/notifications/send`
- **Auto-Reconnect**: Enabled by default

### Connection Modes

| Mode | JWT Token | Tenant ID | Receives |
|------|-----------|-----------|----------|
| Anonymous Global | ❌ No | ❌ No | Global only |
| Anonymous Tenant | ❌ No | ✅ Yes | Global + Tenant |
| Authenticated Global | ✅ Yes | ❌ No | Global + User-specific |
| Authenticated Tenant | ✅ Yes | ✅ Yes | Global + Tenant + User-specific |

## 💾 Data Persistence

- Form data auto-saved to localStorage
- Persists across page refreshes
- Saves: Hub URL, API URL, tenant IDs, auto-reconnect setting

## ⌨️ Keyboard Shortcuts

- `Ctrl/Cmd + Enter`: Send global notification (when focused in admin section)

## 🔔 Browser Notifications

- Requests permission on page load
- Shows desktop notifications when received
- Works in background tabs

## 🐛 Common Issues

### CORS Error
**Solution**: Ensure Notification Service CORS allows your origin

### SSL Certificate Error
**Solution**: Visit `https://localhost:5104` first and accept certificate

### Connection Fails
**Solution**: 
- Check service is running
- Verify Hub URL
- Check JWT token validity
- Open console (F12) for errors

### No Notifications Received
**Solution**:
- Verify connection is "Connected" (green badge)
- Check tenant/user targeting matches
- Review server logs
- Verify API request succeeded

## 📚 Documentation

Aligned with official docs:
- `NOTIFICATION_HUB_GUIDE.md`
- `NOTIFICATION_HUB_QUICK_REFERENCE.md`
- `NOTIFICATION_SERVICE_README.md`

## ✨ Advanced Features

### Automatic Acknowledgment
Authenticated users automatically acknowledge notifications:
```javascript
connection.invoke("AcknowledgeDelivery", queueItemId)
```

### Notification Metadata Display
Each notification shows:
- Title & Message
- Timestamp
- Tenant ID (if applicable)
- User ID (if applicable)
- Priority level
- Queue Item ID

### Connection Lifecycle Events
- `onConnected`: Updates UI, enables buttons
- `onDisconnected`: Updates status, disables buttons
- `onReconnecting`: Shows "Connecting" status
- `onReconnected`: Restores "Connected" status

## 🎯 Testing Matrix

| Scenario | Section 1 Action | Section 2 State | Section 3 State | Expected Result |
|----------|-----------------|-----------------|-----------------|-----------------|
| Global | Send Global | Connected (any) | Connected (any) | Both receive |
| Tenant | Send Tenant (A) | Connected Tenant A | Connected Tenant B | Only Sec 2 receives |
| User | Send User (A, 5) | Auth User 5, Tenant A | Auth User 10, Tenant A | Only Sec 2 receives |
| Anonymous | Send User (A, 5) | Anonymous, Tenant A | Auth User 5, Tenant A | Only Sec 3 receives |

## 🚀 Next Steps

1. **Run Notification Service**:
   ```bash
   cd src/Services/Notification/Notification.API
   dotnet run
   ```

2. **Open Test Page**:
   ```bash
   start test/hub-test.html
   ```

3. **Get JWT Tokens**:
   - Use Identity Service to login and get tokens
   - Or use existing valid tokens

4. **Test All Scenarios**:
   - Follow the testing checklist in README.md

## 🎉 Summary

You now have a fully functional, beautiful, and comprehensive test page for the Notification Hub that:
- ✅ Tests all notification scenarios (global, tenant, user-specific)
- ✅ Supports authenticated and anonymous connections
- ✅ Shows real-time connection status
- ✅ Displays detailed notification metadata
- ✅ Automatically acknowledges notifications
- ✅ Persists configuration data
- ✅ Provides browser notifications
- ✅ Has a modern, responsive UI
- ✅ Includes comprehensive documentation

Happy testing! 🔔
