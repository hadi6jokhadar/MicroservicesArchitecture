# SuperAdmin Queue Management Endpoint

## Overview

The Notification Service now includes a SuperAdmin-only endpoint for viewing and filtering all notification queue items across all tenants. This endpoint provides comprehensive queue management capabilities with pagination and advanced filtering.

## Endpoint Details

### GET /api/notifications/admin/queue

**Authorization:** SuperAdmin role required (uses global JWT from appsettings.json)

**Description:** Retrieve paginated list of all notification queue items with filtering support.

## Authentication

⚠️ **Important:** This endpoint uses the **global JWT** configured in `appsettings.json` under the `Jwt` section, NOT tenant-specific JWTs.

```json
{
  "Jwt": {
    "Secret": "your-global-secret-key",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp",
    "ExpiryInMinutes": 60
  }
}
```

The JWT token must include a `role` claim with value `SuperAdmin`.

### Bypassing Tenant Middleware

This endpoint is marked with `[BypassTenant]` attribute, which instructs the tenant middleware to skip tenant resolution for this specific endpoint. This allows SuperAdmins to access the queue data across **all tenants** without providing the `x-tenant-id` header.

**Implementation:**

```csharp
adminGroup.MapGet("/queue", NotificationApiHandlers.GetQueueItemsHandler)
    .WithMetadata(new BypassTenantAttribute()) // Bypass tenant requirement
    .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
```

The `TenantMiddleware` checks for this attribute and skips tenant resolution:

```csharp
var endpoint = context.GetEndpoint();
var bypassTenant = endpoint?.Metadata.GetMetadata<BypassTenantAttribute>() != null;

if (bypassTenant)
{
    // Skip tenant resolution - no x-tenant-id header required
    await _next(context);
    return;
}
```

## Query Parameters

| Parameter      | Type         | Required | Default | Description                           |
| -------------- | ------------ | -------- | ------- | ------------------------------------- |
| `pageNumber`   | int          | No       | 1       | Page number (must be > 0)             |
| `pageSize`     | int          | No       | 10      | Page size (1-100)                     |
| `tenantId`     | string       | No       | null    | Filter by specific tenant ID          |
| `userId`       | int          | No       | null    | Filter by specific user ID            |
| `status`       | QueueStatus  | No       | null    | Filter by queue status                |
| `priority`     | Priority     | No       | null    | Filter by priority level              |
| `deliveryType` | DeliveryType | No       | null    | Filter by delivery channel            |
| `fromDate`     | DateTime     | No       | null    | Filter items created after this date  |
| `toDate`       | DateTime     | No       | null    | Filter items created before this date |
| `searchTerm`   | string       | No       | null    | Search in title and message           |

## Enum Values

### QueueStatus

- `0` - Pending
- `1` - Processing
- `2` - Sent
- `3` - Failed
- `4` - Expired

### Priority

- `0` - Waitable
- `1` - Immediate

### DeliveryType

- `1` - SignalR
- `2` - Firebase
- `3` - Both

## Response Schema

```json
{
  "items": [
    {
      "id": 123,
      "tenantId": "ihsandev",
      "userId": 456,
      "deliveryType": 3,
      "priority": 1,
      "title": "New Order Received",
      "message": "Order #12345 has been placed",
      "data": "{\"orderId\": 12345}",
      "queueStatus": 2,
      "retryCount": 0,
      "processedAt": "2025-11-07T10:30:00Z",
      "expiresAt": "2025-11-08T10:00:00Z",
      "error": null,
      "notificationId": 789,
      "createdAt": "2025-11-07T10:00:00Z",
      "updatedAt": "2025-11-07T10:30:00Z"
    }
  ],
  "pageNumber": 1,
  "totalPages": 5,
  "totalCount": 50,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

## Example Requests

### Get all pending notifications (first page)

```bash
GET /api/notifications/admin/queue?status=0&pageSize=20
Authorization: Bearer {superadmin-jwt-token}
```

### Get failed notifications for specific tenant

```bash
GET /api/notifications/admin/queue?tenantId=ihsandev&status=3&pageNumber=1&pageSize=50
Authorization: Bearer {superadmin-jwt-token}
```

### Search notifications by keyword

```bash
GET /api/notifications/admin/queue?searchTerm=order&pageSize=25
Authorization: Bearer {superadmin-jwt-token}
```

### Get immediate priority notifications within date range

```bash
GET /api/notifications/admin/queue?priority=1&fromDate=2025-11-01&toDate=2025-11-07
Authorization: Bearer {superadmin-jwt-token}
```

### Get notifications for specific user

```bash
GET /api/notifications/admin/queue?userId=123&pageNumber=1&pageSize=10
Authorization: Bearer {superadmin-jwt-token}
```

## Use Cases

1. **Queue Monitoring**: Track notification processing status across all tenants
2. **Troubleshooting**: Identify failed notifications and error patterns
3. **Performance Analysis**: Monitor retry counts and processing times
4. **Tenant Analysis**: View notification patterns per tenant
5. **User Support**: Investigate notification delivery issues for specific users
6. **Audit Trail**: Track notification history with timestamps

## Security Considerations

- ✅ **SuperAdmin Only**: Endpoint requires SuperAdmin role
- ✅ **Global JWT**: Uses shared JWT secret from appsettings.json
- ✅ **No Tenant Isolation**: SuperAdmin can view queue items across ALL tenants
- ⚠️ **Sensitive Data**: Queue items may contain user data, titles, and messages
- ⚠️ **Production Access**: Limit SuperAdmin credentials to authorized personnel only

## Performance Notes

- Default page size is 10, maximum is 100
- Queries are optimized with proper indexing on:
  - `TenantId`
  - `UserId`
  - `QueueStatus`
  - `Priority`
  - `Created` (for date range filtering)
- Results are ordered by creation date (most recent first)
- Search filtering may be slower on large datasets

## Implementation Details

### Command

- **File**: `Notification.Application/Commands/GetQueueItemsCommand.cs`
- **Validation**: FluentValidation for page number, page size, and date range

### Handler

- **File**: `Notification.Application/Handlers/GetQueueItemsQueryHandler.cs`
- **Uses**: AutoMapper for entity-to-DTO projection
- **Repository**: Calls `INotificationQueueRepository.GetFilteredQueryable()`

### Repository

- **File**: `Notification.Infrastructure/Repositories/NotificationQueueRepository.cs`
- **Method**: `GetFilteredQueryable()` - Builds dynamic LINQ query based on filters
- **Optimization**: AsNoTracking() for read-only queries

### Endpoint

- **File**: `Notification.API/Extensions/EndpointMappingExtensions.cs`
- **Route**: `/api/notifications/admin/queue`
- **Authorization**: `RequireRole("SuperAdmin")`

## Error Responses

### 401 Unauthorized

```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

**Reason**: Missing or invalid JWT token

### 403 Forbidden

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403
}
```

**Reason**: User does not have SuperAdmin role

### 400 Bad Request

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PageSize": ["Page size cannot exceed 100"]
  }
}
```

**Reason**: Validation failed (e.g., invalid page size, FromDate > ToDate)

## Testing

### Unit Tests

Test the command handler and repository filtering:

```csharp
[Fact]
public async Task GetQueueItems_WithFilters_ReturnsFilteredResults()
{
    // Arrange
    var command = new GetQueueItemsCommand(
        PageNumber: 1,
        PageSize: 10,
        TenantId: "ihsandev",
        Status: QueueStatus.Pending
    );

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    Assert.All(result.Items, item =>
    {
        Assert.Equal("ihsandev", item.TenantId);
        Assert.Equal(QueueStatus.Pending, item.QueueStatus);
    });
}
```

### Integration Tests

Test the endpoint with SuperAdmin authentication:

```csharp
[Fact]
public async Task GetQueueItems_AsSuperAdmin_Returns200()
{
    // Arrange
    var token = GenerateSuperAdminToken();
    _client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    // Act
    var response = await _client.GetAsync(
        "/api/notifications/admin/queue?pageSize=5&status=0");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<PaginatedList<QueueItemDto>>();
    result.Should().NotBeNull();
    result.Items.Should().HaveCountLessThanOrEqualTo(5);
}
```

## Related Documentation

- [NOTIFICATION_SERVICE_README.md](./NOTIFICATION_SERVICE_README.md) - Service overview
- [NOTIFICATION_SYSTEM_FLOW.md](./NOTIFICATION_SYSTEM_FLOW.md) - Technical flow
- [JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md](./JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md) - Authentication
- [MULTI_TENANCY_GUIDE.md](./MULTI_TENANCY_GUIDE.md) - Multi-tenancy architecture
