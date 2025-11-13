# Device Token API - Quick Reference

## Quick Start

### 1. Register a Device Token

```bash
curl -X POST "https://localhost:5001/api/device-tokens" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "userId": 1,
    "token": "your_fcm_token_here",
    "platform": 1,
    "deviceIdentifier": "android-device-123",
    "isPrimary": true
  }'
```

### 2. Get User's Device Tokens

```bash
curl "https://localhost:5001/api/device-tokens/user/1" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### 3. Delete Device Token on Logout

```bash
curl -X DELETE "https://localhost:5001/api/device-tokens/1" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

## Platform Values

| Platform | Value | Description                   |
| -------- | ----- | ----------------------------- |
| iOS      | 0     | Apple devices (APNs tokens)   |
| Android  | 1     | Android devices (FCM tokens)  |
| Web      | 2     | Web browsers (FCM web tokens) |

## Common Scenarios

### Mobile App Startup

```typescript
// React Native / Flutter example
async function registerDeviceToken() {
  const token = await getFCMToken();
  const deviceId = await getDeviceId();

  await api.post("/device-tokens", {
    userId: currentUser.id,
    token: token,
    platform: Platform.isIOS ? 0 : 1,
    deviceIdentifier: deviceId,
    isPrimary: true,
  });
}
```

### Sending Notifications (Server-Side)

```csharp
// In Notification Service
public async Task SendNotificationToUser(int userId, string title, string message)
{
    // Get user's device tokens from Identity Service
    var tokens = await _httpClient.GetFromJsonAsync<List<DeviceTokenDto>>(
        $"https://identity-service/api/device-tokens/user/{userId}");

    // Send to each device
    foreach (var deviceToken in tokens)
    {
        await _fcmService.SendAsync(deviceToken.Token, title, message);
    }
}
```

### Update Token on App Update

```typescript
async function updateDeviceToken(tokenId: number, newToken: string) {
  await api.put(`/device-tokens/${tokenId}`, {
    token: newToken,
  });
}
```

### Clean Up on Logout

```typescript
async function logout() {
  // Delete all device tokens for current user
  await api.delete(`/device-tokens/user/${currentUser.id}`);

  // Or delete specific device token
  await api.delete(`/device-tokens/${currentDeviceTokenId}`);
}
```

## Response Examples

### Success Response (Add Token)

```json
{
  "id": 123,
  "userId": 1,
  "token": "fcm_token_abc123...",
  "platform": 1,
  "deviceIdentifier": "android-device-123",
  "lastVerifiedAt": "2025-11-13T10:30:00Z",
  "isPrimary": true,
  "created": "2025-11-13T10:30:00Z"
}
```

### Error Response (Validation Failed)

```json
{
  "type": "ValidationException",
  "title": "One or more validation errors occurred",
  "status": 400,
  "errors": {
    "Token": ["Token is required"],
    "UserId": ["UserId must be greater than 0"]
  }
}
```

### Error Response (Not Found)

```json
{
  "type": "NotFoundException",
  "title": "Device token with ID 999 not found",
  "status": 404
}
```

## Integration with Notification Service

The Notification Service can retrieve device tokens to send push notifications:

```csharp
// Notification Service example
public class PushNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public async Task<bool> SendPushNotification(
        int userId,
        string title,
        string message)
    {
        var client = _httpClientFactory.CreateClient("IdentityService");

        // Get user's device tokens
        var tokens = await client.GetFromJsonAsync<List<DeviceTokenDto>>(
            $"/api/device-tokens/user/{userId}");

        if (tokens == null || !tokens.Any())
            return false;

        // Send to primary device or all devices
        var primaryToken = tokens.FirstOrDefault(t => t.IsPrimary);
        var targetToken = primaryToken ?? tokens.First();

        return await SendViaFCM(targetToken.Token, title, message);
    }
}
```

## Best Practices

✅ **DO:**

- Register token on app startup/login
- Update token when FCM/APNs token changes
- Delete token on logout
- Set one device as primary for critical notifications
- Validate userId matches authenticated user

❌ **DON'T:**

- Store device tokens in client-side storage
- Share device tokens between users
- Forget to delete tokens on logout
- Send notifications without checking token validity

## Security Notes

- All endpoints require JWT authentication
- Users can only manage their own device tokens (unless Admin)
- Tokens are soft-deleted (IsArchived flag)
- Token strings are indexed for fast lookups

## Testing

### Postman Collection

Import this JSON into Postman:

```json
{
  "info": { "name": "Device Token API" },
  "item": [
    {
      "name": "Add Device Token",
      "request": {
        "method": "POST",
        "url": "{{baseUrl}}/api/device-tokens",
        "header": [{ "key": "Authorization", "value": "Bearer {{jwt_token}}" }],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"userId\": 1,\n  \"token\": \"test_token\",\n  \"platform\": 1,\n  \"isPrimary\": true\n}"
        }
      }
    }
  ]
}
```

## Troubleshooting

**Q: Token not showing up after adding?**

- Check JWT authentication is valid
- Verify userId matches authenticated user
- Check token string is not duplicate

**Q: Multiple primary devices?**

- System automatically unsets other primary flags when setting new primary
- Only one primary per user per platform

**Q: Old FirebaseToken data?**

- Run migration script to move data to DeviceToken table
- See [Full Documentation](DEVICE_TOKEN_MANAGEMENT_GUIDE.md)

---

**See Also:**

- [Full Device Token Management Guide](DEVICE_TOKEN_MANAGEMENT_GUIDE.md)
- [Notification Service README](NOTIFICATION_SERVICE_README.md)
- [Identity Service API Documentation](../src/Services/Identity/IDENTITY_API_DOCUMENTATION.md)
