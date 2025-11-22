# Profile Picture File Lifecycle Management (v2.2)

## Overview

Automatic file lifecycle management ensures that uploaded profile pictures are properly tracked and marked for cleanup when no longer needed. Files are uploaded with `Temp=true` by default and automatically become permanent (`Temp=false`) when attached to a user, or revert to temporary when detached.

## Architecture

### Internal Endpoint

```
PATCH /api/filemanager/internal/files/{id}/temp-status?temp={bool}&tenantId={optional}
```

**Location:** `FileManager.API/Endpoints/FileManagerEndpoints.cs`

**Features:**

- Service-to-service authentication (X-Service-Secret header)
- Optional tenant context support
- Uses existing `UpdateFileCommand` for atomic updates
- Returns 200 (OK) or 404 (Not Found)

### Service Client Method

```csharp
Task<bool> ChangeTempStatusAsync(
    int fileId,
    bool temp,
    string? tenantId = null,
    CancellationToken cancellationToken = default);
```

**Location:** `IhsanDev.Shared.Infrastructure/Services/FileManagerServiceClient.cs`

**Behavior:**

- Returns `true` if successful
- Returns `false` on failure (logged as warning, doesn't throw)
- Fire-and-forget approach (non-blocking)

## Implementation in Identity Service

### UpdateProfileCommandHandler

**Location:** `Identity.Application/Handlers/User/UpdateProfileCommandHandler.cs`

**Logic:**

1. Capture old `ProfilePictureId` before update
2. Update user profile with new `ProfilePictureId`
3. If old ID exists and differs from new:
   - Mark old file as temporary (`temp=true`)
4. If new ID exists:
   - Mark new file as permanent (`temp=false`)

**Example Scenario:**

```csharp
// User had profile picture ID 42, now updating to ID 99
var oldProfilePictureId = user.ProfilePictureId; // 42
user.ProfilePictureId = request.ProfilePictureId; // 99
await _userRepository.UpdateAsync(user, cancellationToken);

// Mark old file as temporary (eligible for cleanup)
await _fileManagerClient.ChangeTempStatusAsync(42, true, tenantId, cancellationToken);

// Mark new file as permanent (keep forever)
await _fileManagerClient.ChangeTempStatusAsync(99, false, tenantId, cancellationToken);
```

### DeleteUserCommandHandler

**Location:** `Identity.Application/Handlers/Admin/DeleteUserCommandHandler.cs`

**Logic:**

1. Soft delete user (set `IsArchived=true`, `Status=false`)
2. If user has `ProfilePictureId`:
   - Mark file as temporary (`temp=true`)

**Example Scenario:**

```csharp
// User has profile picture ID 42
user.IsArchived = true;
user.Status = false;
await _userRepository.UpdateAsync(user, cancellationToken);

// Mark file as temporary (eligible for cleanup)
if (user.ProfilePictureId.HasValue)
    await _fileManagerClient.ChangeTempStatusAsync(42, true, tenantId, cancellationToken);
```

### RegisterCommandHandler

**Note:** Registration does NOT support profile pictures. The `RegisterCommand` does not include a `ProfilePictureId` parameter. Profile pictures can only be added via `UpdateProfile` after account creation.

## File Lifecycle States

```
┌─────────────────────────────────────────────────────────────┐
│                    FILE UPLOAD (Temp=true)                  │
│                 Files are temporary by default              │
└───────────────────────────┬─────────────────────────────────┘
                            │
                            ▼
        ┌───────────────────────────────────────┐
        │   User Updates Profile Picture       │
        │   (UpdateProfileCommand)              │
        └───────┬───────────────────────────────┘
                │
                ├─────────────────────┬──────────────────────┐
                ▼                     ▼                      ▼
    ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
    │   Old File       │  │   New File       │  │   No Old File    │
    │   Temp → true    │  │   Temp → false   │  │   New File       │
    │   (cleanup)      │  │   (permanent)    │  │   Temp → false   │
    └──────────────────┘  └──────────────────┘  └──────────────────┘
                │                     │                      │
                │                     │                      │
                ▼                     ▼                      ▼
    ┌──────────────────────────────────────────────────────────┐
    │           User Deleted (DeleteUserCommand)               │
    │           Existing ProfilePictureId → Temp=true          │
    └──────────────────────────────────────────────────────────┘
                            │
                            ▼
                ┌───────────────────────┐
                │   File Cleanup Job    │
                │   (Future Feature)    │
                │   Deletes Temp Files  │
                └───────────────────────┘
```

## Error Handling

All temp status changes are wrapped in try-catch blocks:

```csharp
try
{
    await _fileManagerClient.ChangeTempStatusAsync(fileId, temp, tenantId, cancellationToken);
}
catch (Exception ex)
{
    // Log warning but don't fail the main operation
    Console.WriteLine($"Warning: Failed to mark file {fileId} as {(temp ? "temporary" : "permanent")}: {ex.Message}");
}
```

**Why Fire-and-Forget?**

- Profile updates should succeed even if temp status fails
- File cleanup is not critical for user experience
- Failures are logged for monitoring
- Eventual consistency is acceptable

## Testing

### Unit Tests

All 107 tests pass, including:

- Profile update with file ID changes
- User deletion with profile picture
- Validation failures (graceful degradation)

### Manual Testing Scenarios

1. **Upload → Attach → Verify Permanent:**

   ```bash
   # Upload file (Temp=true by default)
   POST /api/filemanager/files

   # Update user profile with file ID
   PATCH /api/identity/profile
   {
     "firstName": "John",
     "lastName": "Doe",
     "profilePictureId": 123
   }

   # Verify file is now Temp=false
   GET /api/filemanager/files/123
   ```

2. **Change Profile Picture:**

   ```bash
   # User has file 42, upload new file 99
   POST /api/filemanager/files

   # Update profile with new file
   PATCH /api/identity/profile
   {
     "profilePictureId": 99
   }

   # Verify: File 42 is Temp=true, File 99 is Temp=false
   GET /api/filemanager/files/42  # Temp=true
   GET /api/filemanager/files/99  # Temp=false
   ```

3. **Delete User:**

   ```bash
   # User has file 123
   DELETE /api/identity/admin/users/5

   # Verify file is now Temp=true (eligible for cleanup)
   GET /api/filemanager/files/123
   ```

## Dependencies Injected

**UpdateProfileCommandHandler:**

```csharp
public UpdateProfileCommandHandler(
    IUserRepository userRepository,
    ProfilePictureHelper profilePictureHelper,
    IFileManagerServiceClient fileManagerClient,  // NEW
    ITenantContext tenantContext)                 // NEW
```

**DeleteUserCommandHandler:**

```csharp
public DeleteUserCommandHandler(
    IUserRepository userRepository,
    IFileManagerServiceClient fileManagerClient,  // NEW
    ITenantContext tenantContext)                 // NEW
```

## Future Enhancements

1. **Cleanup Job:**

   - Scheduled job to delete files where `Temp=true` and `Created < 24 hours ago`
   - Configurable retention period
   - Soft delete → hard delete workflow

2. **Batch Temp Status Updates:**

   - If multiple files change at once
   - Similar to batch file fetching (v2.1)

3. **Audit Trail:**
   - Track why files became temporary
   - Store reference to user/entity that owned the file

## Performance Considerations

- **HTTP Overhead:** One additional HTTP call per profile update/delete
- **Non-Blocking:** Fire-and-forget approach doesn't block main flow
- **Graceful Degradation:** Main operation succeeds even if temp status fails
- **Network Resilience:** Client has built-in error handling and logging

## Related Documentation

- [Profile Picture Complete Guide](PROFILE_PICTURE_COMPLETE_GUIDE.md) - v2.0 implementation
- [Profile Picture Batch Optimization](PROFILE_PICTURE_BATCH_OPTIMIZATION.md) - v2.1 batch fetching
- [FileManager Quick Reference](FILE_MANAGER_QUICK_REFERENCE.md) - All endpoints
- [Service-to-Service Communication](BYPASS_TENANT_ENDPOINTS_GUIDE.md) - Internal endpoints pattern

## Version History

- **v2.2 (Nov 2024):** Added automatic file lifecycle management
- **v2.1 (Nov 2024):** Batch file fetching for N+1 query prevention
- **v2.0 (Nov 2024):** Initial profile picture integration

## Summary

The file lifecycle management system ensures that:

1. ✅ Uploaded files are temporary by default
2. ✅ Files become permanent when attached to entities
3. ✅ Files revert to temporary when detached or user deleted
4. ✅ Main operations never fail due to temp status changes
5. ✅ All changes are logged for monitoring
6. ✅ System is ready for future cleanup job implementation
