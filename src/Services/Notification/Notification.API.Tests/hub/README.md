# Notification Hub Test Page

A comprehensive HTML/CSS/JavaScript test page for testing the Notification SignalR Hub with multi-tenancy support.

## 📋 Overview

This test page allows you to:
- **Send notifications** as a SuperAdmin to different targets (global, tenant, or specific users)
- **Connect multiple users** simultaneously to test real-time notification delivery
- **Test anonymous and authenticated connections**
- **Verify multi-tenancy** notification routing

## 🚀 Getting Started

### Prerequisites

1. **Notification Service** must be running (default: `https://localhost:5104` or `http://localhost:5004`)
2. **Valid JWT tokens** for authentication testing
3. Modern web browser with JavaScript enabled

### Running the Test Page

Simply open `hub-test.html` in your browser:

```bash
# Navigate to the test folder
cd src/Services/Notification/Notification.API/test

# Open the file in your default browser (Windows)
start hub-test.html

# Or use a local web server (recommended)
python -m http.server 8080
# Then visit: http://localhost:8080/hub-test.html
```

## 📖 How to Use

### Section 1: SuperAdmin - Send Notifications

This section allows you to send notifications to different targets.

#### Fields:
- **JWT Token** (required): SuperAdmin JWT token for authentication
- **Tenant ID** (optional): Target tenant identifier
- **User ID** (optional): Target user identifier
- **Title** (required): Notification title
- **Message** (required): Notification message
- **Priority**: Immediate, Waitable, or Background
- **Delivery Type**: SignalR, Firebase, or Both
- **Additional Data** (optional): JSON object with custom data

#### Buttons:

1. **🌍 Send Global (All Tenants)**
   - Sends to ALL users in ALL tenants
   - Leave both Tenant ID and User ID empty
   - Targets SignalR group: `global`

2. **🏢 Send to Tenant**
   - Sends to ALL users in a specific tenant
   - Set Tenant ID, leave User ID empty
   - Targets SignalR group: `tenant:{tenantId}`

3. **👤 Send to Specific User**
   - Sends to a specific user in a specific tenant
   - Set both Tenant ID and User ID
   - Targets SignalR group: `tenant:{tenantId}:user:{userId}`

### Section 2 & 3: User Listeners

These sections allow you to connect multiple users simultaneously to test notification delivery.

#### Fields:
- **JWT Token** (optional): User JWT token (leave empty for anonymous connection)
- **Tenant ID** (optional): Tenant to connect to (leave empty to receive only global notifications)

#### Connection Types:

1. **Anonymous + No Tenant**
   - Leave both JWT Token and Tenant ID empty
   - Receives: Global notifications only
   - Joins group: `global`

2. **Anonymous + Tenant**
   - Leave JWT Token empty, set Tenant ID
   - Receives: Global + Tenant-wide notifications
   - Joins groups: `global`, `tenant:{tenantId}`

3. **Authenticated + No Tenant**
   - Set JWT Token, leave Tenant ID empty
   - Receives: Global + User-specific notifications (cross-tenant)
   - Joins groups: `global`, `user:{userId}`

4. **Authenticated + Tenant**
   - Set both JWT Token and Tenant ID
   - Receives: All notification types
   - Joins groups: `global`, `tenant:{tenantId}`, `tenant:{tenantId}:user:{userId}`

#### Buttons:
- **🔌 Connect**: Establish SignalR connection
- **⏹️ Disconnect**: Close SignalR connection
- **🗑️ Clear Logs**: Clear received notifications display

## 🧪 Test Scenarios

### Scenario 1: Global Broadcast
**Goal**: Test that all connected users receive global notifications

1. Connect User 1 (any configuration)
2. Connect User 2 (any configuration)
3. Send a global notification (leave Tenant ID and User ID empty)
4. **Expected**: Both users receive the notification

### Scenario 2: Tenant-Wide Broadcast
**Goal**: Test that only users in a specific tenant receive notifications

1. Connect User 1 with Tenant ID = "tenant-a"
2. Connect User 2 with Tenant ID = "tenant-b"
3. Send notification with Tenant ID = "tenant-a", User ID empty
4. **Expected**: Only User 1 receives the notification

### Scenario 3: User-Specific Notification
**Goal**: Test that only a specific user receives the notification

1. Connect User 1 with JWT (userId=5) and Tenant ID = "tenant-a"
2. Connect User 2 with JWT (userId=10) and Tenant ID = "tenant-a"
3. Send notification with Tenant ID = "tenant-a", User ID = 5
4. **Expected**: Only User 1 receives the notification

### Scenario 4: Anonymous vs Authenticated
**Goal**: Test that anonymous users cannot receive user-specific notifications

1. Connect User 1 anonymously (no JWT) with Tenant ID = "tenant-a"
2. Connect User 2 authenticated (JWT, userId=5) with Tenant ID = "tenant-a"
3. Send user-specific notification (Tenant ID = "tenant-a", User ID = 5)
4. **Expected**: Only User 2 receives the notification

## ⚙️ Configuration Section

- **Hub URL**: SignalR hub endpoint (default: `https://localhost:5104/hubs/notifications`)
- **API URL**: Send notification API endpoint (default: `https://localhost:5104/api/notifications/send`)
- **Enable Auto-Reconnect**: Automatically reconnect on connection loss

## 🔑 Features

### Auto-Save
- Form data is automatically saved to browser's localStorage
- Data persists across page refreshes

### Keyboard Shortcuts
- `Ctrl/Cmd + Enter`: Send global notification (when focused in admin section)

### Browser Notifications
- Desktop notifications appear when enabled in browser
- Requests permission on page load

### Automatic Acknowledgment
- Authenticated connections automatically acknowledge notification delivery
- Acknowledgment is logged to browser console

### Real-time Connection Status
- Visual indicators show connection state (Connected, Disconnected, Connecting)
- Animated status badges

### Notification Metadata
- Each received notification displays:
  - Title and Message
  - Timestamp
  - Tenant ID (if applicable)
  - User ID (if applicable)
  - Priority level
  - Queue Item ID

## 🐛 Troubleshooting

### CORS Errors
If you see CORS errors in the console:
1. Ensure the Notification Service allows your origin in CORS configuration
2. Or use the same origin (e.g., host the HTML on the same server)

### SSL/Certificate Errors
For local development with HTTPS:
1. Accept the self-signed certificate in your browser
2. Visit `https://localhost:5104` directly first to accept the certificate
3. Then reload the test page

### Connection Fails
- Verify the Notification Service is running
- Check the Hub URL in Configuration section
- Open browser console (F12) for detailed error messages
- Ensure JWT token is valid (not expired)

### Notifications Not Received
- Verify the connection is established (green "Connected" badge)
- Check that tenant/user targeting matches the connection configuration
- Review server logs for SignalR group assignments
- Ensure the API request was successful (check admin status message)

## 📝 Notes

### Multi-Tenancy Mode
When `MultiTenancy:Enabled = true` in service configuration:
- Tenant-specific routing is active
- Use tenant IDs for scoped notifications

### Single-Tenant Mode
When `MultiTenancy:Enabled = false` in service configuration:
- All notifications go to the same database
- Tenant IDs are ignored
- Uses `all-clients` group instead of tenant groups

### JWT Token Format
Tokens should be in the format: `Bearer {your-jwt-token-here}`
Or just: `{your-jwt-token-here}` (the page adds "Bearer" automatically in the Authorization header)

### User ID Extraction
For authenticated connections:
- User ID is extracted from JWT claims (`ClaimTypes.NameIdentifier`)
- Must match the User ID used when sending user-specific notifications

## 🎨 UI Features

- Responsive design (mobile-friendly)
- Modern gradient styling
- Animated notifications
- Auto-scroll to latest notification
- Color-coded sections
- Real-time status updates

## 📚 Related Documentation

- [NOTIFICATION_HUB_GUIDE.md](../../../Doc/NOTIFICATION_HUB_GUIDE.md) - Complete hub documentation
- [NOTIFICATION_HUB_QUICK_REFERENCE.md](../../../Doc/NOTIFICATION_HUB_QUICK_REFERENCE.md) - Quick reference guide
- [NOTIFICATION_SERVICE_README.md](../../../Doc/NOTIFICATION_SERVICE_README.md) - Service documentation

## 🔧 Development

### File Structure
```
test/
├── hub-test.html     # Main HTML page
├── styles.css        # Styling
├── script.js         # JavaScript logic
└── README.md         # This file
```

### Technologies Used
- HTML5
- CSS3 (Grid, Flexbox, Animations)
- Vanilla JavaScript (ES6+)
- SignalR JavaScript Client Library (loaded from CDN)

## ✅ Testing Checklist

- [ ] Global broadcast reaches all users
- [ ] Tenant broadcast reaches only users in that tenant
- [ ] User-specific notifications reach only the target user
- [ ] Anonymous users cannot receive user-specific notifications
- [ ] Authenticated users can acknowledge notifications
- [ ] Auto-reconnect works after connection loss
- [ ] Multiple connections can be active simultaneously
- [ ] Browser notifications appear (if enabled)
- [ ] Connection status updates correctly
- [ ] Clear logs button works
- [ ] Form data persists across page refresh

---

**Last Updated**: 2025-11-08
**Version**: 1.0.0
