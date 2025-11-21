# Profile Picture Integration - Quick Reference

## 🚀 Quick Start

### Client API Usage

```http
# Get user WITH profile picture (slower, more data)
GET /api/users/{id}?includeProfilePicture=true

# Get user WITHOUT profile picture (faster)
GET /api/users/{id}

# Update profile picture reference
PUT /api/users/profile
{
  "profilePictureId": 123  // File ID from FileManager
}
```

## 📋 Implementation Checklist

- [x] Created `IFileManagerServiceClient` in Shared
- [x] Created `FileManagerServiceClient` implementation
- [x] Added internal endpoint in FileManager (`/api/filemanager/internal/files/{id}`)
- [x] Updated User entity (`ProfilePictureUrl` → `ProfilePictureId`)
- [x] Updated DTOs (`UserDto`, `UserDtoIncludesToken`)
- [x] Updated commands with `includeProfilePicture` parameter
- [x] Updated handlers to fetch profile picture
- [x] Registered HttpClient in Identity service
- [x] Created database migration
- [x] All builds successful

## 🔧 Configuration

### Program.cs (Any Service)

```csharp
// One-line setup in any service!
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "YourServiceName",  // e.g., "IdentityService", "NotificationService"
    builder.Environment.IsDevelopment());
```

### appsettings.json (Any Service)

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005",
      "Timeout": 5
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "your-shared-secret",
    "Enabled": true
  }
}
```

## 🎯 Key Endpoints

### Internal FileManager Endpoint (Service-Only)

```
GET /api/filemanager/internal/files/{id}?tenantId={optional}

Headers:
  X-Service-Secret: {shared-secret}
  X-Service-Name: IdentityService

Response: FileManagerDto | null
```

### Identity Endpoints (Client)

```
GET /api/users/{id}?includeProfilePicture={true|false}
GET /api/users/profile?includeProfilePicture={true|false}
PUT /api/users/profile (profilePictureId in body)
```

## ⚡ Performance

| Operation   | Single User | User List (100 users)  |
| ----------- | ----------- | ---------------------- |
| Get User    | ~20-30ms    | ~50-100ms (batch v2.1) |
| Before v2.1 | ~20-30ms    | ~2-5 seconds (N+1)     |

**Batch Optimization (v2.1):** 20-50x faster for lists! 🚀

See [PROFILE_PICTURE_BATCH_OPTIMIZATION.md](PROFILE_PICTURE_BATCH_OPTIMIZATION.md) for details.

## 🔒 Security

✅ Internal endpoint requires `X-Service-Secret`  
✅ No JWT needed for service-to-service  
✅ Bypasses rate limiting  
✅ Hidden from Swagger  
✅ Tenant isolation maintained

## 🛠️ Middleware Bypass

Internal endpoint **SKIPS**:

- Rate limiting
- JWT authentication
- Tenant resolution middleware
- CORS checks

Internal endpoint **USES**:

- Service authentication (header check)
- Optional tenant context (query parameter)

## 📊 Database Changes

```sql
-- Old schema
ProfilePictureUrl VARCHAR(500)

-- New schema
ProfilePictureId INT NULL

-- Migration
dotnet ef database update
```

## 🧪 Testing Flow

1. **Upload file to FileManager**

   ```http
   POST /api/filemanager/files
   Form-Data: file=profile.jpg
   Response: { "id": 123 }
   ```

2. **Update user profile**

   ```http
   PUT /api/users/profile
   Body: { "profilePictureId": 123 }
   ```

3. **Get user with picture**
   ```http
   GET /api/users/{id}?includeProfilePicture=true
   Response: { "profilePictureId": 123, "profilePicture": {...} }
   ```

## 🚨 Troubleshooting

| Issue               | Solution                                           |
| ------------------- | -------------------------------------------------- |
| Picture not loading | Check FileManager is running on port 5005          |
| 403 Forbidden       | Verify `X-Service-Secret` matches in both services |
| Picture null        | Check file exists and tenantId matches             |
| Slow response       | Remove `includeProfilePicture=true` if not needed  |

## 💡 Best Practices

✅ **DO**: Use `includeProfilePicture=true` only when displaying user details  
✅ **DO**: Omit parameter for user lists (better performance)  
✅ **DO**: Handle null ProfilePicture gracefully in UI  
❌ **DON'T**: Always fetch profile picture for every request  
❌ **DON'T**: Use public FileManager endpoints for service calls

## 🔄 Migration Path

### For Existing Data:

1. **Backup** existing ProfilePictureUrl values
2. **Upload** images to FileManager
3. **Map** URLs to FileManager IDs
4. **Update** User records
5. **Run** migration
6. **Verify** picture loading

### Example Migration Script:

```csharp
// Pseudo-code for data migration
foreach (var user in usersWithPictures)
{
    // Upload old picture URL to FileManager
    var file = await UploadToFileManager(user.ProfilePictureUrl);

    // Update user with new ID
    user.ProfilePictureId = file.Id;
    await userRepository.UpdateAsync(user);
}
```

## 📚 Related Files

### **📖 Complete Documentation:**

- [PROFILE_PICTURE_COMPLETE_GUIDE.md](PROFILE_PICTURE_COMPLETE_GUIDE.md) - **Complete implementation guide**
- [PROFILE_PICTURE_BATCH_OPTIMIZATION.md](PROFILE_PICTURE_BATCH_OPTIMIZATION.md) - **N+1 prevention (20-50x faster)**

### Shared Components:

- `IhsanDev.Shared.Application/Common/Interfaces/IFileManagerServiceClient.cs`
- `IhsanDev.Shared.Infrastructure/Services/FileManagerServiceClient.cs`
- `IhsanDev.Shared.Infrastructure/Extensions/FileManagerServiceExtensions.cs` ⭐

### FileManager:

- `FileManager.API/Endpoints/FileManagerEndpoints.cs` (internal endpoint + scope timing fix)

### Identity:

- `Identity.Domain/Entities/User.cs` (ProfilePictureId)
- `Identity.Application/DTOs/UserDTOs.cs` (DTO updates)
- `Identity.Application/Helpers/ProfilePictureHelper.cs` (central enrichment)
- `Identity.Application/Handlers/` (12 handlers updated)
- `Identity.API/Program.cs` (HttpClient registration)

## 🎓 Architecture Benefits

✅ **Single Source of Truth**: FileManager owns all files  
✅ **Reusable**: Any service can use `IFileManagerServiceClient`  
✅ **Performance**: Fast internal endpoint  
✅ **Flexibility**: Optional loading pattern  
✅ **Tenant-Aware**: Respects multi-tenancy  
✅ **Maintainable**: Centralized file logic
