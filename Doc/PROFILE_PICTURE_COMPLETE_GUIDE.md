# Profile Picture Integration - Complete Implementation Guide

**Last Updated:** November 21, 2025  
**Status:** ✅ Production Ready  
**Version:** 2.1 - Batch Optimization Included

> **⚡ Performance Note:** Version 2.1 includes batch fetching optimization that provides **20-50x performance improvement** for lists. See [PROFILE_PICTURE_BATCH_OPTIMIZATION.md](PROFILE_PICTURE_BATCH_OPTIMIZATION.md) for details.

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Implementation Details](#implementation-details)
4. [Database Schema](#database-schema)
5. [API Usage](#api-usage)
6. [Configuration](#configuration)
7. [Error Handling](#error-handling)
8. [Testing](#testing)
9. [Troubleshooting](#troubleshooting)

---

## Overview

Complete integration between Identity and FileManager services for user profile pictures. Profile pictures are stored as files in FileManager and referenced by ID in Identity service, enabling centralized file management with multi-tenant support.

### Key Features

✅ **Service-to-Service Communication** - Fast internal endpoint bypassing middleware  
✅ **Multi-Tenant Support** - Respects tenant boundaries automatically  
✅ **Automatic Enrichment** - All 12 user endpoints return profile picture details  
✅ **Graceful Degradation** - Continues without picture on errors  
✅ **Parallel Processing** - Batch enrichment for user lists  
✅ **Reusable Infrastructure** - Shared components for any service  
✅ **Scope Timing Fix** - Resolved "first request null" issue

---

## Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Identity Service (Port 5001)                 │
│                                                                   │
│  ┌──────────────┐    ┌─────────────────────┐                   │
│  │  Handlers    │───▶│ ProfilePictureHelper│                   │
│  │ (12 total)   │    │                     │                   │
│  └──────────────┘    └──────────┬──────────┘                   │
│                                  │                               │
│                                  ▼                               │
│                      ┌───────────────────────┐                  │
│                      │IFileManagerService    │                  │
│                      │Client (Shared)        │                  │
│                      └──────────┬────────────┘                  │
└─────────────────────────────────┼────────────────────────────────┘
                                  │ HTTP + X-Service-Secret
                                  │
┌─────────────────────────────────┼────────────────────────────────┐
│                     FileManager Service (Port 5006)              │
│                                  │                               │
│                                  ▼                               │
│                      ┌───────────────────────┐                  │
│                      │ Internal Endpoint     │                  │
│                      │ /internal/files/{id}  │                  │
│                      │                       │                  │
│                      │ • No JWT required     │                  │
│                      │ • No rate limiting    │                  │
│                      │ • Tenant-aware scope  │                  │
│                      └──────────┬────────────┘                  │
│                                  │                               │
│                                  ▼                               │
│                      ┌───────────────────────┐                  │
│                      │   Multi-Tenant DB     │                  │
│                      │   (Per-Tenant DBs)    │                  │
│                      └───────────────────────┘                  │
└─────────────────────────────────────────────────────────────────┘
```

### Request Flow

```
1. Client → Identity API
   GET /api/users/profile
   Headers: Authorization: Bearer {jwt}, x-tenant-id: tenant123

2. Identity Handler
   - Fetch user from database
   - User has ProfilePictureId = 42

3. ProfilePictureHelper.EnrichWithProfilePictureAsync()
   - Calls IFileManagerServiceClient.GetFileByIdAsync(42, "tenant123")

4. FileManagerServiceClient
   - HTTP GET /api/filemanager/internal/files/42?tenantId=tenant123
   - Headers: X-Service-Secret, X-Service-Name: IdentityService

5. FileManager Internal Endpoint
   - Creates scope with tenant context BEFORE DbContext resolution
   - Queries tenant123 database
   - Returns FileManagerDto

6. Identity Handler
   - Enriches UserDto.ProfilePicture with FileManagerDto
   - Returns to client with complete profile data
```

---

## Implementation Details

### 1. Shared Infrastructure (Reusable)

#### IFileManagerServiceClient Interface

**Location:** `IhsanDev.Shared.Application/Common/Interfaces/IFileManagerServiceClient.cs`

```csharp
public interface IFileManagerServiceClient
{
    Task<FileManagerDto?> GetFileByIdAsync(
        int fileId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

public class FileManagerDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Extension { get; set; }
    public string Path { get; set; }
    public string Url { get; set; }
    public long Size { get; set; }
    public string MimeType { get; set; }
    // ... additional properties
}
```

#### FileManagerServiceClient Implementation

**Location:** `IhsanDev.Shared.Infrastructure/Services/FileManagerServiceClient.cs`

```csharp
public class FileManagerServiceClient : IFileManagerServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FileManagerServiceClient> _logger;

    public async Task<FileManagerDto?> GetFileByIdAsync(
        int fileId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"/api/filemanager/internal/files/{fileId}";

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                endpoint += $"?tenantId={Uri.EscapeDataString(tenantId)}";
            }

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch file {FileId} - Status: {StatusCode}",
                    fileId, response.StatusCode);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            return JsonSerializer.Deserialize<FileManagerDto>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching file {FileId}", fileId);
            return null; // Graceful degradation
        }
    }
}
```

#### Extension Method for Easy Registration

**Location:** `IhsanDev.Shared.Infrastructure/Extensions/FileManagerServiceExtensions.cs`

```csharp
public static class FileManagerServiceExtensions
{
    public static IServiceCollection AddFileManagerServiceClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool isDevelopment = false)
    {
        var baseUrl = configuration["Services:FileManagerService:BaseUrl"]
            ?? throw new InvalidOperationException("FileManager BaseUrl not configured");

        var sharedSecret = configuration["ServiceCommunication:SharedSecret"]
            ?? throw new InvalidOperationException("Shared secret not configured");

        services.AddHttpClient<IFileManagerServiceClient, FileManagerServiceClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue<int>("Services:FileManagerService:Timeout", 5));

            // Service authentication headers
            client.DefaultRequestHeaders.Add("X-Service-Secret", sharedSecret);
            client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
        });

        return services;
    }
}
```

### 2. FileManager Service Updates

#### Internal Endpoint with Scope Timing Fix

**Location:** `FileManager.API/Endpoints/FileManagerEndpoints.cs`

**Critical Pattern - CreateScopeWithTenantAsync Helper:**

```csharp
/// <summary>
/// Helper to set tenant context in new scope BEFORE resolving dependencies.
/// Prevents "first request returns null" issue where DbContext is configured
/// before tenant context is set.
/// </summary>
private static async Task<(IServiceScope scope, ITenantContext? tenantContext)>
    CreateScopeWithTenantAsync(
        IServiceProvider serviceProvider,
        string? tenantId,
        ITenantConfigurationProvider tenantConfigProvider,
        CancellationToken cancellationToken)
{
    // Create new scope for fresh DbContext
    var scope = serviceProvider.CreateScope();
    var scopedServices = scope.ServiceProvider;
    var tenantContext = scopedServices.GetRequiredService<ITenantContext>();

    // Set tenant context BEFORE resolving any other services (including DbContext)
    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        var tenant = await tenantConfigProvider.GetTenantConfigurationAsync(
            tenantId, cancellationToken);
        if (tenant != null)
        {
            tenantContext.SetTenant(tenant);
        }
    }

    return (scope, tenantContext);
}
```

**Internal Endpoint Implementation:**

```csharp
var internalGroup = app.MapGroup("/api/filemanager/internal")
    .WithTags("FileManager - Internal")
    .DisableRateLimiting() // Skip rate limiting
    .WithMetadata(new BypassTenantAttribute()); // Skip tenant middleware

internalGroup.MapGet("/files/{id:int}", async (
    int id,
    [FromQuery] string? tenantId,
    ITenantConfigurationProvider tenantConfigProvider,
    HttpContext httpContext,
    ILogger<Program> logger,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken) =>
{
    // Validate service-to-service call
    var isService = httpContext.User.HasClaim("IsInternalService", "true");
    if (!isService)
    {
        logger.LogWarning("Internal endpoint access denied - missing claim");
        return Results.Json(null, statusCode: StatusCodes.Status403Forbidden);
    }

    // Create scope with tenant context set BEFORE DbContext resolution
    var scopeResult = await CreateScopeWithTenantAsync(
        serviceProvider, tenantId, tenantConfigProvider, cancellationToken);

    using var scope = scopeResult.scope;

    // Now resolve MediatR - DbContext will see correct tenant context
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    var query = new GetFileByIdQuery(id);
    var result = await mediator.Send(query, cancellationToken);

    return Results.Ok(result); // Returns null on not found (graceful)
})
.WithName("GetFileByIdInternal")
.AllowAnonymous() // ServiceAuthenticationMiddleware handles auth
.Produces<FileManagerResponse?>()
.ExcludeFromDescription(); // Hide from Swagger
```

**Why CreateScopeWithTenantAsync?**

- **Problem:** DbContext configuration happens when service is resolved from DI container
- **Issue:** If tenant context is set AFTER DbContext is resolved, DbContext uses wrong connection
- **Solution:** Create new scope, set tenant context FIRST, then resolve services (including DbContext)
- **Result:** DbContext always sees correct tenant context and connects to correct database

### 3. Identity Service Updates

#### Database Schema

**Entity Changes:**

```csharp
// User.cs - Before
public class User
{
    public string? ProfilePictureUrl { get; set; } // ❌ Removed
}

// User.cs - After
public class User
{
    public int? ProfilePictureId { get; set; } // ✅ Added
}
```

**Migration:** `20251121190156_ReplaceProfilePictureUrlWithId`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "ProfilePictureUrl", table: "Users");
    migrationBuilder.AddColumn<int>(
        name: "ProfilePictureId",
        table: "Users",
        type: "integer",
        nullable: true);
}
```

#### DTO Updates

```csharp
public class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    // ... other properties

    public int? ProfilePictureId { get; set; } // New
    public FileManagerDto? ProfilePicture { get; set; } // New - enriched by helper
}

public class UserDtoIncludesToken : BaseUserDto
{
    // Same ProfilePictureId and ProfilePicture properties
    public int? ProfilePictureId { get; set; }
    public FileManagerDto? ProfilePicture { get; set; }

    // Token properties
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? RefreshTokenExpiryTime { get; set; }
}
```

#### ProfilePictureHelper (Central Enrichment)

**Location:** `Identity.Application/Helpers/ProfilePictureHelper.cs`

**Key Methods:**

```csharp
public class ProfilePictureHelper
{
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ProfilePictureHelper> _logger;

    // Single UserDto enrichment
    public async Task<UserDto> EnrichWithProfilePictureAsync(
        UserDto userDto,
        int? profilePictureId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!profilePictureId.HasValue)
            return userDto;

        try
        {
            var tenantId = _tenantContext.CurrentTenant?.TenantId;

            userDto.ProfilePicture = await _fileManagerClient.GetFileByIdAsync(
                profilePictureId.Value, tenantId, cancellationToken);

            if (userDto.ProfilePicture == null)
            {
                _logger.LogWarning("Profile picture {PictureId} not found for user {UserId}",
                    profilePictureId.Value, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch profile picture for user {UserId}", userId);
            // Graceful degradation - continue without picture
        }

        return userDto;
    }

    // UserDtoIncludesToken enrichment (overload)
    public async Task<UserDtoIncludesToken> EnrichWithProfilePictureAsync(
        UserDtoIncludesToken userDto,
        int? profilePictureId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // Same logic as above
    }

    // Batch enrichment for lists (v2.1) - Optimized to prevent N+1 queries
    public async Task<IEnumerable<UserDto>> EnrichWithProfilePicturesAsync(
        IEnumerable<UserDto> userDtos,
        CancellationToken cancellationToken = default)
    {
        var userList = userDtos.ToList();

        // Collect all unique profile picture IDs
        var pictureIds = userList
            .Where(u => u.ProfilePictureId.HasValue)
            .Select(u => u.ProfilePictureId!.Value)
            .Distinct()
            .ToList();

        if (!pictureIds.Any())
            return userList;

        try
        {
            var tenantId = _tenantContext.CurrentTenant?.TenantId;

            // ✅ Single batch request for all pictures (v2.1)
            var picturesDict = await _fileManagerClient.GetFilesByIdsAsync(
                pictureIds, tenantId, cancellationToken);

            // Enrich users with fetched pictures
            foreach (var user in userList.Where(u => u.ProfilePictureId.HasValue))
            {
                if (picturesDict.TryGetValue(user.ProfilePictureId!.Value, out var picture))
                {
                    user.ProfilePicture = picture;
                }
                else
                {
                    _logger.LogWarning(
                        "Profile picture {PictureId} not found for user {UserId}",
                        user.ProfilePictureId.Value, user.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to batch fetch profile pictures for {Count} users",
                userList.Count);
            // Graceful degradation
        }

        return userList;
    }
}
```

#### Handler Updates (All 12 Handlers)

**Pattern:**

```csharp
public class GetUserProfileCommandHandler : IRequestHandler<GetUserProfileCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;

    public GetUserProfileCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
    }

    public async Task<UserDto> Handle(
        GetUserProfileCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user == null)
            throw new NotFoundException("User not found");

        var result = UserDto.MapFrom(user);

        // Enrich with profile picture
        await _profilePictureHelper.EnrichWithProfilePictureAsync(
            result,
            user.ProfilePictureId,
            user.Id,
            cancellationToken);

        return result;
    }
}
```

**All Updated Handlers:**

1. ✅ GetUserProfileCommandHandler - User profile
2. ✅ UpdateProfileCommandHandler - Profile update
3. ✅ GetUsersCommandHandler - User list (parallel)
4. ✅ GetUserByIdCommandHandler - Admin get user
5. ✅ CreateUserCommandHandler - Admin create user
6. ✅ UpdateUserCommandHandler - Admin update user
7. ✅ ToggleUserStatusCommandHandler - Admin toggle status
8. ✅ LoginCommandHandler - Login
9. ✅ RegisterCommandHandler - Registration
10. ✅ RefreshTokenCommandHandler - Token refresh
11. ✅ LoginWithCodeByEmailCommandHandler - Email OTP
12. ✅ LoginWithCodeByPhoneCommandHandler - Phone OTP

---

## Database Schema

### Identity Service

**Users Table:**

```sql
CREATE TABLE "Users" (
    "Id" SERIAL PRIMARY KEY,
    "FirstName" VARCHAR(100),
    "LastName" VARCHAR(100),
    "Email" VARCHAR(255) UNIQUE,
    "ProfilePictureId" INTEGER NULL, -- References FileManager file ID
    -- ... other columns
    CONSTRAINT "FK_Users_ProfilePicture" FOREIGN KEY ("ProfilePictureId")
        REFERENCES "Files"("Id") ON DELETE SET NULL -- Optional FK
);
```

### FileManager Service

**Files Table:**

```sql
CREATE TABLE "Files" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(255),
    "Extension" VARCHAR(20),
    "Path" TEXT,
    "Url" TEXT,
    "Size" BIGINT,
    "MimeType" VARCHAR(100),
    "UserId" INTEGER NULL,
    "TenantId" VARCHAR(100) NULL,
    "CreatedAt" TIMESTAMP,
    -- ... other columns
);

CREATE INDEX "IX_Files_UserId" ON "Files" ("UserId");
CREATE INDEX "IX_Files_TenantId" ON "Files" ("TenantId");
```

---

## API Usage

### Upload Profile Picture

```http
POST /api/filemanager/files
Content-Type: multipart/form-data
Authorization: Bearer {jwt-token}
x-tenant-id: tenant123

Form Data:
  file: profile.jpg
  group: 1 (User profile pictures)
  userId: 42

Response:
{
  "id": 123,
  "name": "profile",
  "extension": ".jpg",
  "path": "/tenant123/users/profile_20251121.jpg",
  "url": "https://files.example.com/tenant123/users/profile_20251121.jpg",
  "size": 102400,
  "mimeType": "image/jpeg"
}
```

### Update User Profile with Picture

```http
PUT /api/users/profile
Content-Type: application/json
Authorization: Bearer {jwt-token}
x-tenant-id: tenant123

{
  "firstName": "John",
  "lastName": "Doe",
  "profilePictureId": 123
}

Response:
{
  "id": 42,
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "profilePictureId": 123,
  "profilePicture": {
    "id": 123,
    "name": "profile",
    "extension": ".jpg",
    "url": "https://files.example.com/tenant123/users/profile_20251121.jpg",
    "size": 102400
  }
}
```

### Get User Profile (with Picture)

```http
GET /api/users/profile
Authorization: Bearer {jwt-token}
x-tenant-id: tenant123

Response:
{
  "id": 42,
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "profilePictureId": 123,
  "profilePicture": {
    "id": 123,
    "name": "profile",
    "extension": ".jpg",
    "url": "https://files.example.com/tenant123/users/profile_20251121.jpg",
    "size": 102400,
    "mimeType": "image/jpeg"
  }
}
```

### Get User List

```http
GET /api/users?page=1&pageSize=10
Authorization: Bearer {jwt-token}
x-tenant-id: tenant123

Response:
{
  "items": [
    {
      "id": 42,
      "firstName": "John",
      "profilePictureId": 123,
      "profilePicture": {
        "id": 123,
        "url": "https://files.example.com/tenant123/users/profile.jpg"
      }
    }
  ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 10
}
```

---

## Configuration

### Identity Service (Program.cs)

```csharp
// Add FileManager service client
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "IdentityService",
    builder.Environment.IsDevelopment());

// Register ProfilePictureHelper
builder.Services.AddScoped<ProfilePictureHelper>();
```

### appsettings.json (Identity Service)

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://localhost:5006",
      "Timeout": 5
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "your-shared-secret-here",
    "Enabled": true
  }
}
```

### appsettings.json (FileManager Service)

```json
{
  "ServiceCommunication": {
    "SharedSecret": "your-shared-secret-here",
    "Enabled": true,
    "AllowedServices": ["IdentityService", "NotificationService"]
  }
}
```

**Security:** Use environment variables for secrets in production!

---

## Error Handling

### Graceful Degradation

The system continues functioning even if profile picture fetch fails:

```csharp
// If FileManager is down or file not found
{
  "id": 42,
  "firstName": "John",
  "lastName": "Doe",
  "profilePictureId": 123,
  "profilePicture": null  // ← Graceful: null instead of error
}
```

### Error Scenarios

| Scenario            | Behavior                       | HTTP Status |
| ------------------- | ------------------------------ | ----------- |
| File not found      | Returns `profilePicture: null` | 200 OK      |
| FileManager down    | Returns `profilePicture: null` | 200 OK      |
| Invalid tenantId    | Returns `profilePicture: null` | 200 OK      |
| No profilePictureId | Returns `profilePicture: null` | 200 OK      |
| Service auth failed | Logs error, returns null       | 200 OK      |

**User data is NEVER blocked by profile picture errors.**

### Logging

```
// Normal operation (no logs)
// ✅ Silent success

// Warning when picture not found
[Warning] Profile picture 123 not found for user 42

// Error on exception
[Error] Failed to fetch profile picture 123 for user 42
Exception: HttpRequestException - Connection refused
```

---

## Testing

### Unit Tests

```csharp
[Fact]
public async Task GetUserProfile_WithProfilePicture_ReturnsEnrichedDto()
{
    // Arrange
    var mockFileManagerClient = new Mock<IFileManagerServiceClient>();
    mockFileManagerClient
        .Setup(x => x.GetFileByIdAsync(123, It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new FileManagerDto { Id = 123, Name = "profile", Extension = ".jpg" });

    var helper = new ProfilePictureHelper(
        mockFileManagerClient.Object,
        mockTenantContext.Object,
        mockLogger.Object);

    var userDto = new UserDto { Id = 42, ProfilePictureId = 123 };

    // Act
    await helper.EnrichWithProfilePictureAsync(userDto, 123, 42, CancellationToken.None);

    // Assert
    Assert.NotNull(userDto.ProfilePicture);
    Assert.Equal(123, userDto.ProfilePicture.Id);
}
```

### Integration Tests

```csharp
[Fact]
public async Task UpdateProfile_WithProfilePictureId_UpdatesSuccessfully()
{
    // Arrange
    var client = _factory.CreateClient();
    var token = await GetAuthTokenAsync();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    client.DefaultRequestHeaders.Add("x-tenant-id", "test-tenant");

    var request = new
    {
        firstName = "John",
        lastName = "Doe",
        profilePictureId = 123
    };

    // Act
    var response = await client.PutAsJsonAsync("/api/users/profile", request);

    // Assert
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<UserDto>();
    Assert.Equal(123, result.ProfilePictureId);
}
```

### Test Results

```
✅ Identity.API.Tests - 107 tests passed
   - User profile operations: 15 tests
   - Admin user operations: 18 tests
   - Auth flows (login/register/OTP): 24 tests
   - Profile picture enrichment: 8 tests
   - Validation tests: 42 tests
```

---

## Troubleshooting

### Issue: Profile Picture Not Loading

**Symptoms:**

- `profilePicture` is always `null`
- No errors in logs

**Diagnosis:**

```bash
# Check FileManager is running
curl https://localhost:5006/health

# Check internal endpoint access
curl -H "X-Service-Secret: your-secret" \
     https://localhost:5006/api/filemanager/internal/files/123
```

**Solutions:**

1. Verify FileManager service is running on correct port
2. Check `X-Service-Secret` matches in both services
3. Verify file ID exists in FileManager database
4. Check tenant ID matches if using multi-tenancy

### Issue: "First Request Returns Null, Second Works"

**Symptoms:**

- First API call after startup returns `profilePicture: null`
- Subsequent calls work correctly

**Root Cause:**

- DbContext resolved before tenant context is set
- DbContext connects to wrong database

**Solution:**
✅ **Already Fixed** - Using `CreateScopeWithTenantAsync` pattern in FileManager endpoints

### Issue: 403 Forbidden on Internal Endpoint

**Symptoms:**

```
[Warning] Internal endpoint access denied - missing IsInternalService claim
```

**Solutions:**

1. Verify `ServiceAuthenticationMiddleware` is registered in FileManager
2. Check `X-Service-Secret` header is being sent
3. Verify secret matches in both services' appsettings

### Issue: Slow Performance

**Symptoms:**

- API responses taking > 500ms
- Multiple profile picture fetches

**Solutions:**

1. ✅ Use parallel enrichment for user lists (already implemented)
2. Consider caching profile pictures in Redis (future enhancement)
3. Check FileManager database indexes on Files table

### Issue: Tests Failing After Migration

**Symptoms:**

```
Error: Column 'ProfilePictureUrl' does not exist
```

**Solutions:**

1. Update test code to use `ProfilePictureId` instead of `ProfilePictureUrl`
2. Run database migration on test database:
   ```bash
   cd src/Services/Identity/Identity.API.Tests
   dotnet test
   # Migrations auto-apply on test database
   ```

---

## Related Documentation

- **[PROFILE_PICTURE_BATCH_OPTIMIZATION.md](PROFILE_PICTURE_BATCH_OPTIMIZATION.md)** - 🚀 **N+1 query prevention & batch fetching (20-50x faster)**
- [PROFILE_PICTURE_QUICK_REFERENCE.md](PROFILE_PICTURE_QUICK_REFERENCE.md) - Quick API reference
- [PROFILE_PICTURE_ENRICHMENT_COMPLETE.md](PROFILE_PICTURE_ENRICHMENT_COMPLETE.md) - Handler implementation summary
- [FILE_MANAGER_SERVICE_GUIDE.md](FILE_MANAGER_SERVICE_GUIDE.md) - FileManager architecture
- [FILE_MANAGER_QUICK_REFERENCE.md](FILE_MANAGER_QUICK_REFERENCE.md) - FileManager API reference
- [BYPASS_TENANT_ENDPOINTS_GUIDE.md](BYPASS_TENANT_ENDPOINTS_GUIDE.md) - Internal endpoint patterns
- [SHARED_IDENTITY_SERVICE_GUIDE.md](SHARED_IDENTITY_SERVICE_GUIDE.md) - Identity service
- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Multi-tenancy concepts

---

## Appendix: Complete File Locations

### Shared Infrastructure

- `IhsanDev.Shared.Application/Common/Interfaces/IFileManagerServiceClient.cs`
- `IhsanDev.Shared.Infrastructure/Services/FileManagerServiceClient.cs`
- `IhsanDev.Shared.Infrastructure/Extensions/FileManagerServiceExtensions.cs`

### FileManager Service

- `FileManager.API/Endpoints/FileManagerEndpoints.cs` (internal endpoint + scope helper)

### Identity Service

- `Identity.Domain/Entities/User.cs`
- `Identity.Application/DTOs/UserDto.cs`
- `Identity.Application/DTOs/UserDtoIncludesToken.cs`
- `Identity.Application/Helpers/ProfilePictureHelper.cs`
- `Identity.Application/Handlers/*/` (12 handlers updated)
- `Identity.Infrastructure/Migrations/20251121190156_ReplaceProfilePictureUrlWithId.cs`
- `Identity.API/Program.cs`

### Tests

- `Identity.API.Tests/Endpoints/UserEndpointsTests.cs`
- `Identity.API.Tests/Handlers/GetUserProfileCommandHandlerTests.cs`

---

**Status:** ✅ Complete, Tested, Production Ready  
**Last Verified:** November 21, 2025  
**Test Coverage:** 107/107 tests passing  
**Performance:** 20-50x improvement with batch fetching (v2.1)
