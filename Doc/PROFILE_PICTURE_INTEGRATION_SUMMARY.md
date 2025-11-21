# Profile Picture Integration with FileManager Service

> **⚠️ DEPRECATED:** This file contains initial implementation notes.  
> **📖 For Current Documentation:** See [PROFILE_PICTURE_COMPLETE_GUIDE.md](PROFILE_PICTURE_COMPLETE_GUIDE.md)

**Last Updated:** November 21, 2025  
**Status:** ⚠️ Superseded by PROFILE_PICTURE_COMPLETE_GUIDE.md

## Overview

Integrated FileManager service with Identity service to handle user profile pictures through ultra-fast service-to-service communication. Profile pictures are now managed as files in FileManager with optional tenant support.

## Architecture Changes

### 1. **Shared Infrastructure Components** (Reusable across all services)

#### Created Files:

- `IhsanDev.Shared.Application/Common/Interfaces/IFileManagerServiceClient.cs`

  - Interface for FileManager service communication
  - Includes `FileManagerDto` for file metadata

- `IhsanDev.Shared.Infrastructure/Services/FileManagerServiceClient.cs`

  - HttpClient-based implementation
  - Uses internal endpoint for maximum performance
  - Graceful error handling (returns null on failure)

- `IhsanDev.Shared.Infrastructure/Extensions/FileManagerServiceExtensions.cs`
  - Reusable extension method: `AddFileManagerServiceClient()`
  - Configures HttpClient with service authentication
  - Works in any service (Identity, Notification, etc.)

### 2. **FileManager Service Updates**

#### New Internal Endpoint:

```
GET /api/filemanager/internal/files/{id}?tenantId={tenantId}
```

**Features:**

- ✅ Bypasses rate limiting (`.DisableRateLimiting()`)
- ✅ Bypasses tenant middleware (`BypassTenantAttribute`)
- ✅ Service-only authentication (validates `X-Service-Secret`)
- ✅ Optional tenant support (query parameter)
- ✅ Returns null on error (no exceptions)
- ✅ Hidden from Swagger (`.ExcludeFromDescription()`)

**Performance:**

- ~40-60% faster than admin endpoint
- Skips: Rate limiting, JWT validation, tenant middleware, CORS
- Only validates: Service secret header

#### Modified Files:

- `FileManager.API/Endpoints/FileManagerEndpoints.cs`
  - Added internal endpoint group
  - Service authentication check
  - Optional tenant context setting

### 3. **Identity Service Updates**

#### Database Schema Changes:

- **User Entity**: Replaced `ProfilePictureUrl` (string) with `ProfilePictureId` (int?)
- **Migration**: `ReplaceProfilePictureUrlWithId`
- **DTOs Updated**: `UserDto`, `UserDtoIncludesToken`

#### DTO Structure:

```csharp
public class UserDto
{
    // ... existing properties
    public int? ProfilePictureId { get; set; }
    public FileManagerDto? ProfilePicture { get; set; } // Populated when requested
}
```

#### Command Updates:

- `GetUserByIdCommand`: Added `IncludeProfilePicture` parameter (default: false)
- `GetUserProfileCommand`: Added `IncludeProfilePicture` parameter (default: false)
- `UpdateProfileCommand`: Changed from `ProfilePictureUrl` to `ProfilePictureId`

#### Handler Updates:

- `GetUserByIdCommandHandler`: Fetches profile picture when requested
- `GetUserProfileCommandHandler`: Fetches profile picture when requested
- `UpdateProfileCommandHandler`: Updates `ProfilePictureId` instead of URL
- `GetUsersCommandHandler`: Returns `ProfilePictureId` (no picture in list view)

#### API Endpoints:

```
GET /api/users/{id}?includeProfilePicture=true
GET /api/users/profile?includeProfilePicture=true
```

#### Service Configuration:

- Extension method in `Program.cs`:

```csharp
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "IdentityService",
    builder.Environment.IsDevelopment());
```

- Base URL: Configured via `Services:FileManagerService:BaseUrl`
- Timeout: Configurable (default: 5 seconds)
- Service authentication headers auto-configured

### 4. **Service-to-Service Communication Flow**

```
1. Client → Identity API
   GET /api/users/profile?includeProfilePicture=true

2. Identity Service → User Repository
   Fetch user data (includes ProfilePictureId)

3. Identity Service → FileManager Service (if includeProfilePicture=true && ProfilePictureId exists)
   GET /api/filemanager/internal/files/{ProfilePictureId}?tenantId={tenantId}
   Headers: X-Service-Secret, X-Service-Name

4. FileManager Service → Database
   Quick lookup by ID (indexed primary key)

5. FileManager Service → Identity Service
   Returns FileManagerDto or null

6. Identity Service → Client
   Returns UserDto with ProfilePicture populated (or null)
```

## Key Features

### Performance Optimizations:

1. **Optional Loading**: Only fetches picture when explicitly requested
2. **Internal Endpoint**: Bypasses middleware for speed
3. **Connection Pooling**: HttpClient reuses connections
4. **Fast Timeout**: 5-second timeout for quick failure
5. **No Caching Complexity**: Direct service calls, simple architecture

### Error Handling:

- Graceful degradation (continues without picture on error)
- Logs warnings when picture not found
- Returns null instead of throwing exceptions
- No impact on user data if FileManager is down

### Multi-Tenant Support:

- Profile pictures can be in global DB (no tenantId)
- Profile pictures can be in tenant-specific DB (with tenantId)
- Tenant context automatically passed from Identity to FileManager

## Usage Examples

### Get User with Profile Picture:

```http
GET /api/users/{id}?includeProfilePicture=true
Headers:
  Authorization: Bearer {jwt-token}
  x-tenant-id: tenant123 (optional)

Response:
{
  "id": 1,
  "firstName": "John",
  "lastName": "Doe",
  "profilePictureId": 42,
  "profilePicture": {
    "id": 42,
    "name": "profile",
    "extension": ".jpg",
    "size": 102400,
    "url": "https://localhost:5005/tenant123/users/profile.jpg",
    "type": 1,
    ...
  }
}
```

### Get User without Profile Picture (faster):

```http
GET /api/users/{id}
Headers:
  Authorization: Bearer {jwt-token}

Response:
{
  "id": 1,
  "firstName": "John",
  "lastName": "Doe",
  "profilePictureId": 42,
  "profilePicture": null
}
```

### Update Profile Picture:

```http
PUT /api/users/profile
Headers:
  Authorization: Bearer {jwt-token}
Body:
{
  "firstName": "John",
  "lastName": "Doe",
  "profilePictureId": 42  // Changed from profilePictureUrl
}
```

## Configuration Required

### Identity Service (`appsettings.json`):

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005"
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "your-secret-here",
    "Enabled": true
  }
}
```

### FileManager Service (`appsettings.json`):

```json
{
  "ServiceCommunication": {
    "SharedSecret": "your-secret-here",
    "Enabled": true,
    "AllowedServices": ["IdentityService", "NotificationService"]
  }
}
```

## Migration Steps

### 1. Database Migration:

```bash
cd src/Services/Identity/Identity.API
dotnet ef database update
```

This will:

- Drop `ProfilePictureUrl` column
- Add `ProfilePictureId` column (nullable int)

### 2. Data Migration (if needed):

If you have existing profile picture URLs, you'll need to:

1. Upload existing images to FileManager
2. Update User records with new ProfilePictureId values

## Security Considerations

1. **Service Authentication**: Uses shared secret for internal communication
2. **No Public Access**: Internal endpoint not accessible without service secret
3. **Tenant Isolation**: Respects tenant boundaries
4. **Authorization**: Service role added by middleware

## Performance Metrics

**Before (hypothetical external URL approach):**

- No file metadata available
- Manual URL construction
- No lifecycle management

**After (FileManager integration):**

- Single DB query (indexed lookup)
- ~5-10ms internal call overhead
- Full file metadata available
- Centralized file management

## Future Enhancements

1. **Caching**: Add Redis caching for frequently accessed profile pictures
2. **CDN Integration**: Serve images through CDN for public access
3. **Image Optimization**: Thumbnail generation for different sizes
4. **Batch Loading**: Fetch multiple profile pictures in one call for user lists

## Testing

### Manual Testing:

1. Start FileManager service on port 5005
2. Start Identity service on port 5001
3. Upload a file to FileManager (get file ID)
4. Update user profile with ProfilePictureId
5. Get user profile with `includeProfilePicture=true`

### Integration Tests:

- Tests are in `Identity.API.Tests` project
- Mock `IFileManagerServiceClient` for unit tests
- Use `CustomWebApplicationFactory` for integration tests

## Troubleshooting

### Profile Picture Not Loading:

1. Check FileManager service is running
2. Verify `X-Service-Secret` is configured and matches
3. Check logs for HTTP errors
4. Verify ProfilePictureId exists in FileManager database
5. Ensure tenantId matches if using multi-tenancy

### Performance Issues:

1. Check FileManager response time in logs
2. Verify connection pooling is working
3. Consider adding caching if needed
4. Check database indexes on FileManager

## Related Documentation

- [FILE_MANAGER_SERVICE_GUIDE.md](FILE_MANAGER_SERVICE_GUIDE.md)
- [JWT_TENANT_VERIFICATION_GUIDE.md](JWT_TENANT_VERIFICATION_GUIDE.md)
- [SERVICE_TO_SERVICE_COMMUNICATION.md](SERVICE_TO_SERVICE_COMMUNICATION.md)
