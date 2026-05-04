# File Manager Service

**Purpose:** Complete guide to the File Manager Service - handles file upload, storage, retrieval, and management with multi-tenancy support.  
**Last Updated:** May 4, 2026  
**Status:** ✅ Production Ready (v3.1.0)

---

## Overview

The File Manager Service is a centralized file storage microservice using Clean Architecture, DDD, and CQRS patterns. It provides secure file operations with tenant isolation, Redis caching, static file serving, and automatic cleanup.

**Port:** 5005 (Development)  
**Database:** PostgreSQL (multi-provider support)  
**Storage:** Local file system (production: Azure Blob, AWS S3, MinIO)  
**Caching:** Redis distributed cache (7-day TTL) with MemoryCache fallback

### Key Features

- ✅ **Multi-Tenancy**: Database-per-tenant isolation
- ✅ **Dual Endpoints**: Tenant endpoints (user files) + Admin endpoints (global files)
- ✅ **Static File Serving**: Direct file access via public URLs
- ✅ **Redis Caching**: 7-day tenant config cache with automatic invalidation
- ✅ **Background Jobs**: Automatic temp file cleanup
- ✅ **Service-to-Service**: HTTP client for internal service calls
- ✅ **Security**: File size limits, extension validation, access control
- ✅ **Usage Tracking**: `FileManagerUsage` table prevents premature cleanup of shared files

---

## Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│              FILE MANAGER SERVICE (Port 5005)                │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │  Tenant API  │  │  Admin API   │  │  Static Files    │  │
│  │  Endpoints   │  │  Endpoints   │  │  Middleware      │  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────────┘  │
│         │                  │                  │              │
│         └──────────────────┴──────────────────┘              │
│                             │                                 │
│         ┌───────────────────┴───────────────────┐            │
│         │                                       │            │
│    ┌────▼──────────┐                     ┌─────▼──────┐     │
│    │  Tenant DB    │                     │ Global DB  │     │
│    │  (Per Tenant) │                     │ (Fallback) │     │
│    └───────┬───────┘                     └─────┬──────┘     │
│            │                                   │            │
│            └──────────┬────────────────────────┘            │
│                       │                                     │
│              ┌────────▼────────┐                            │
│              │  File Storage   │                            │
│              │  (Local/Cloud)  │                            │
│              └─────────────────┘                            │
└─────────────────────────────────────────────────────────────┘
```

### Layer Architecture

```
FileManager/
├── FileManager.API/           # Minimal APIs, endpoints, Program.cs
│   ├── Endpoints/             # Tenant + Admin endpoint groups
│   ├── Program.cs             # Service configuration
│   └── appsettings.json       # Configuration
│
├── FileManager.Application/   # CQRS handlers, DTOs, validators
│   ├── Commands/              # SaveFile, UpdateFile, DeleteFile
│   ├── Queries/               # GetFileById, GetAllFiles
│   ├── Handlers/              # MediatR request handlers
│   └── DTOs/                  # FileManagerResponse, mapping
│
├── FileManager.Domain/        # Entities, enums, repository interfaces
│   ├── Entities/              # FileManagerEntity
│   ├── Enums/                 # FileGroup, FileType
│   └── Repositories/          # IFileManagerRepository
│
└── FileManager.Infrastructure/ # EF Core, storage, services
    ├── Persistence/           # DbContext, repositories
    ├── Storage/               # LocalFileStorage, IFileStorage
    ├── Services/              # FileManagerService, business logic
    └── BackgroundJobs/        # TempFileCleanupService
```

---

## Configuration

### appsettings.json

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=filemanager;Username=postgres;Password=postgres;",
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": false,
    "CommandTimeout": 30,
    "MaxRetryCount": 3,
    "MaxRetryDelay": 30
  },

  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant",
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  },

  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 21600
  },

  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  },

  "ServiceCommunication": {
    "Enabled": true,
    "ServiceName": "FileManagerService",
    "SharedSecret": "your-shared-secret-here",
    "AllowedServices": ["IdentityService", "NotificationService"]
  },

  "FileManagerOptions": {
    "RootStoragePath": "C:\\FileStorage",
    "FilesSavePath": "uploads",
    "MaxFileSizeInMB": 50,
    "AllowedExtensions": [".jpg", ".png", ".pdf", ".docx", ".xlsx", ".zip"],
    "TempFileRetentionDays": 30
  },

  "BlobStorage": {
    "Provider": "CloudflareR2",
    "CloudflareR2": {
      "AccountId": "your-cloudflare-account-id",
      "AccessKeyId": "your-r2-access-key-id",
      "SecretAccessKey": "your-r2-secret-access-key",
      "BucketName": "your-bucket-name",
      "PublicDomain": "https://pub-xxx.r2.dev"
    }
  }
}
```

### Environment Variables

**Development:**

```bash
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://localhost:5005;http://localhost:5004
```

**Production:**

```bash
ASPNETCORE_ENVIRONMENT=Production
DatabaseSettings__ConnectionString="Server=prod-db.azure.com;..."
Redis__ConnectionString="prod-redis.azure.com:6380,ssl=true,password=xxx"
FileManagerOptions__RootStoragePath="/var/filemanager/storage"
```

---

## Blob Storage (Third-Party)

Files can optionally be uploaded to a third-party blob provider (e.g. Cloudflare R2). The `ExternalUrl` field on each file record stores the public blob URL after upload.

### How It Works

1. Upload a file normally via `POST /files` — file is stored locally, `ExternalUrl` is `null`.
2. Call `POST /files/{id}/upload-to-blob` — file is streamed to blob storage, `ExternalUrl` is set to the public URL.
3. Call `DELETE /files/{id}/remove-from-blob` — file is removed from blob, `ExternalUrl` is cleared.
4. When a file is deleted via `DELETE /files/{id}`, the blob copy is also automatically deleted if `ExternalUrl` is set.
5. Background temp-file cleanup also removes blob copies automatically.

### Configuration Priority

Blob settings follow the same global/per-tenant pattern as `Cors`:

1. **Per-tenant** — set `BlobStorage` in the tenant's `Configuration` JSON in the Tenant Service.
2. **Global** — set `BlobStorage` in `appsettings.json` of the FileManager service.
3. **None** — blob operations are silently skipped (no-op fallback).

### Supported Providers

| Provider key           | Description                   |
| ---------------------- | ----------------------------- |
| `CloudflareR2` or `R2` | Cloudflare R2 (S3-compatible) |

### Per-Tenant Configuration (Tenant Service JSON)

In the tenant's `Configuration` field, add a `BlobStorage` block following the same pattern as `Cors`:

```json
{
  "Cors": { ... },
  "BlobStorage": {
    "Provider": "CloudflareR2",
    "CloudflareR2": {
      "AccountId": "abc123",
      "AccessKeyId": "key",
      "SecretAccessKey": "secret",
      "BucketName": "my-bucket",
      "PublicDomain": "https://pub-xxx.r2.dev"
    }
  }
}
```

---

## API Endpoints

### Tenant Endpoints (`/api/filemanager/*`)

**Authentication:** Requires JWT + `x-tenant-id` header  
**Authorization:** User, Admin, SuperAdmin roles

| Endpoint                       | Method | Description                                                              |
| ------------------------------ | ------ | ------------------------------------------------------------------------ |
| `/files`                       | POST   | Upload file to tenant database                                           |
| `/files/{id}`                  | GET    | Get file metadata                                                        |
| `/files`                       | GET    | List files (paginated, filtered)                                         |
| `/files/{id}`                  | PUT    | Update file metadata                                                     |
| `/files/{id}`                  | DELETE | Delete file (soft delete)                                                |
| `/files/{id}/download`         | GET    | Download file (anonymous)                                                |
| `/files/{id}/upload-to-blob`   | POST   | Upload file to third-party blob (e.g. Cloudflare R2), sets `ExternalUrl` |
| `/files/{id}/remove-from-blob` | DELETE | Remove file from blob storage, clears `ExternalUrl`                      |

**Upload File Example:**

```http
POST https://localhost:5005/api/filemanager/files
Authorization: Bearer {tenant-jwt}
x-tenant-id: ihsandev
Content-Type: multipart/form-data

Form Data:
  file: [binary file data]
  name: "invoice.pdf"
  group: 1
  type: 3
  temp: false
  userId: 123
```

**Response:**

```json
{
  "id": 456,
  "name": "invoice.pdf",
  "extension": ".pdf",
  "size": 1048576,
  "path": "ihsandev/123/shared/abc-123-def.pdf",
  "url": "https://localhost:5005/ihsandev/123/shared/abc-123-def.pdf",
  "externalUrl": null,
  "group": 1,
  "type": 3,
  "temp": false,
  "status": true,
  "isArchived": false,
  "userId": 123,
  "created": "2026-01-27T10:30:00Z"
}
```

### Admin Endpoints (`/api/filemanager/admin/*`)

**Authentication:** Requires JWT (no `x-tenant-id` header)  
**Authorization:** Service, SuperAdmin roles

| Endpoint                                    | Method | Description                                 |
| ------------------------------------------- | ------ | ------------------------------------------- |
| `/files?tenantId=xxx`                       | POST   | Upload file to specific tenant or global DB |
| `/files/{id}`                               | GET    | Get file from any tenant                    |
| `/files`                                    | GET    | List files across tenants                   |
| `/files/{id}`                               | PUT    | Update file in any tenant                   |
| `/files/{id}`                               | DELETE | Delete file from any tenant                 |
| `/files/temp/all`                           | DELETE | Delete all temp files (cross-tenant)        |
| `/files/temp/old?days=30`                   | DELETE | Delete old temp files                       |
| `/files/{id}/upload-to-blob?tenantId=xxx`   | POST   | Upload file to blob for any tenant          |
| `/files/{id}/remove-from-blob?tenantId=xxx` | DELETE | Remove file from blob for any tenant        |

**Upload to Global Database:**

```http
POST https://localhost:5005/api/filemanager/admin/files
Authorization: Bearer {global-jwt}
Content-Type: multipart/form-data

Form Data:
  file: [binary file data]
  name: "system-logo.png"

# No tenantId = Uses global database
```

**Upload to Specific Tenant:**

```http
POST https://localhost:5005/api/filemanager/admin/files?tenantId=ihsandev
Authorization: Bearer {global-jwt}

# SuperAdmin uploads to tenant "ihsandev"
```

### Static File Access

**Direct File Serving (No Authentication):**

```http
GET https://localhost:5005/{tenantId}/{userId|system}/{group}/{filename}

Example: https://localhost:5005/ihsandev/123/personal/abc-123.jpg
```

**Features:**

- ✅ No authentication required (public access)
- ✅ Served via PhysicalFileProvider middleware
- ✅ CORS enabled for cross-origin access
- ✅ Automatic MIME type detection
- ✅ Direct streaming (no API overhead)

---

## Database Schema

### FileManager Table

| Column         | Type           | Description                                     | Indexed |
| -------------- | -------------- | ----------------------------------------------- | ------- |
| Id             | int            | Primary key                                     | ✅ (PK) |
| Name           | varchar(255)   | File name                                       | ❌      |
| Extension      | varchar(10)    | File extension (.pdf, .jpg)                     | ❌      |
| Size           | bigint         | Size in bytes                                   | ❌      |
| Path           | varchar(500)   | Storage path (relative)                         | ❌      |
| Group          | int            | FileGroup enum (1-6)                            | ✅      |
| Type           | int            | FileType enum (0-3)                             | ✅      |
| Temp           | bool           | Temporary flag (auto-managed by usage tracking) | ✅      |
| UserId         | int (nullable) | Owner user ID                                   | ✅      |
| IsArchived     | bool           | Soft delete flag                                | ✅      |
| Status         | bool           | Active/Inactive                                 | ✅      |
| Created        | timestamptz    | Creation timestamp (UTC)                        | ✅      |
| CreatedBy      | varchar        | Creator identifier                              | ❌      |
| LastModified   | timestamptz    | Last update timestamp                           | ❌      |
| LastModifiedBy | varchar        | Last updater identifier                         | ❌      |

**Indexes:** Id (PK), UserId, Group, Type, Temp, IsArchived, Status, Created

### FileManagerUsage Table

> **Added in v3.1.0** — Tracks which entities reference a file to prevent premature cleanup.

| Column    | Type         | Description                                        | Indexed        |
| --------- | ------------ | -------------------------------------------------- | -------------- |
| Id        | int          | Primary key                                        | ✅ (PK)        |
| FileId    | int          | Foreign key to FileManager.Id                      | ✅             |
| UsageArea | varchar(100) | Entity type using the file (e.g. "User", "Artist") | ✅ (composite) |
| RowId     | varchar(100) | Entity identifier as string (e.g. "42")            | ✅ (composite) |

**Unique Index:** `(FileId, UsageArea, RowId)` — prevents duplicate usage rows.

**Business Rule:** `FileManager.Temp` is automatically recalculated after every usage change:

- `COUNT(*) == 0` → `Temp = true` (eligible for background cleanup)
- `COUNT(*) > 0` → `Temp = false` (protected from cleanup)

### Enums

**FileGroup:**

```csharp
public enum FileGroup
{
    Personal = 1,  // User's private files
    Shared = 2,    // Shared within tenant
    System = 3,    // System files (logos, templates)
    Project = 4,   // Project-specific files
    Archive = 5,   // Archived files
    AI = 6         // AI-generated or AI-related files
}
```

**FileType (Auto-detected from extension):**

```csharp
public enum FileType
{
    Music = 0,   // .mp3, .wav, .flac, .aac
    Video = 1,   // .mp4, .avi, .mov, .mkv
    Image = 2,   // .jpg, .png, .gif, .bmp, .svg
    Other = 3    // All other extensions
}
```

---

## Multi-Tenancy

### Database-Per-Tenant Architecture

Each tenant gets their own isolated database. **No TenantId column** in FileManager table - database boundary provides isolation.

**Request Flow:**

```
1. Client Request → x-tenant-id: ihsandev
2. TenantMiddleware → Resolves tenant config from Tenant Service
3. TenantContext → Populates with ihsandev configuration
4. DbContext → Uses ihsandev database connection string
5. Query → Executes against ihsandev database only
```

**Modes:**

| Mode         | Configuration                | Behavior                                                                               |
| ------------ | ---------------------------- | -------------------------------------------------------------------------------------- |
| **Enabled**  | `MultiTenancy:Enabled=true`  | Requires `x-tenant-id` header, fetches config from Tenant Service, database-per-tenant |
| **Disabled** | `MultiTenancy:Enabled=false` | Uses `appsettings.json` config, single database, no `x-tenant-id` needed               |

### JWT Mode Configuration

**CRITICAL:** Must match Identity Service configuration!

```json
{
  "MultiTenancy": {
    "JwtMode": "PerTenant" // Must match Identity Service
  }
}
```

**Options:**

- **`Shared`**: All services use same JWT secret from appsettings.json
- **`PerTenant`**: Each tenant has own JWT secret (stored in Tenant Service)

**Pitfall:** Mismatched JWT mode causes 401 Unauthorized for tenant users!

### Optional Tenant Context

Admin endpoints support **both** global and tenant-specific operations:

- **Without `tenantId` query parameter**: Uses global database (from appsettings.json)
- **With `tenantId=xxx` query parameter**: Uses tenant's database

**Implementation:**

```csharp
// DbContext fallback pattern
if (_tenantContext?.HasTenant != true ||
    _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
{
    // Use global database from appsettings.json
    connectionString = _configuration["DatabaseSettings:ConnectionString"];
}
else
{
    // Use tenant-specific database
    connectionString = _tenantContext.CurrentTenant.Configuration
        .DatabaseSettings.ConnectionString;
}
```

---

## File Storage

### Storage Structure

```
{RootStoragePath}/
├── {sanitized-tenant-id}/
│   └── {FilesSavePath}/
│       ├── {userId}/
│       │   ├── Personal/
│       │   │   └── {guid}.jpg
│       │   ├── Shared/
│       │   ├── Project/
│       │   └── Archive/
│       └── system/
│           └── System/
└── {another-tenant}/
    └── ...
```

**Path Pattern:**  
`{RootStoragePath}/{sanitized-tenant-id}/{FilesSavePath}/{userId|system}/{category}/{guid.ext}`

**Example:**  
`C:\FileStorage\ihsandev\uploads\123\personal\abc-123-def-456.pdf`

### Response Fields

```json
{
  "path": "ihsandev/123/personal/abc-123.pdf", // Storage path (backend)
  "url": "https://localhost:5005/ihsandev/123/personal/abc-123.pdf" // Public URL (frontend)
}
```

- **Path**: Relative path stored in database (forward slashes normalized)
- **Url**: Full public URL constructed from configuration (for direct access)

---

## Caching Strategy

### Redis Distributed Cache

**Tenant Configuration Caching:**

- **TTL**: 7 days (604,800 seconds)
- **Cache Keys**:
  - Individual: `tenant_config_{tenantId}`
  - Paginated: `all_active_tenants_with_config_page_{n}_size_{m}`
- **Invalidation**: Automatic on tenant Create/Update/Delete
- **Fallback**: Automatic fallback to `IMemoryCache` if Redis unavailable

**Benefits:**

- ✅ 95% reduction in Tenant Service API calls
- ✅ ~100ms → ~5ms for tenant config retrieval
- ✅ Cache shared across all service instances
- ✅ Cache survives service restarts

**Configuration:**

```json
{
  "Redis": {
    "Enabled": true, // false = automatic MemoryCache fallback
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  }
}
```

---

## Background Jobs

### TempFileCleanupService

**Purpose:** Automatically delete temporary files older than configured retention period.

**Schedule:** Daily at 2:00 AM

**Features:**

- ✅ **Parallel Processing**: 5-10x faster than sequential cleanup
- ✅ **Batch Operations**: Processes files in configurable batches
- ✅ **Physical Deletion**: Removes files from both database and disk storage
- ✅ **Error Handling**: Continues on individual file failures
- ✅ **Logging**: Detailed cleanup metrics

**Configuration:**

```json
{
  "FileManagerOptions": {
    "TempFileRetentionDays": 30 // Delete temp files older than 30 days
  }
}
```

**Manual Trigger (Admin Endpoint):**

```http
DELETE https://localhost:5005/api/filemanager/admin/files/temp/old?days=30
Authorization: Bearer {global-jwt}
```

---

## Service-to-Service Integration

### Using FileManager in Other Services

**1. Register Client in Program.cs:**

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;

builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "YourServiceName",
    builder.Environment.IsDevelopment());
```

**2. Configure in appsettings.json:**

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005",
      "Timeout": 5
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "your-shared-secret-here",
    "Enabled": true
  }
}
```

**3. Inject and Use in Handlers:**

```csharp
using IhsanDev.Shared.Application.Common.Interfaces;

public class YourCommandHandler
{
    private readonly IFileManagerServiceClient _fileManagerClient;

    public YourCommandHandler(IFileManagerServiceClient fileManagerClient)
    {
        _fileManagerClient = fileManagerClient;
    }

    public async Task<YourResponse> Handle(YourCommand request, CancellationToken ct)
    {
        var file = await _fileManagerClient.GetFileByIdAsync(
            request.FileId,
            tenantId: "optional-tenant-id",
            ct);

        return new YourResponse
        {
            FileUrl = file?.Url,
            FileName = file?.Name
        };
    }
}
```

### File Usage Tracking (v3.1.0+)

> **Important:** Do NOT manually set `Temp=true/false`. Use `ChangeTempStatusAsync` exclusively.

`ChangeTempStatusAsync` explicitly adds or removes a usage row in `FileManagerUsage` via the `isNew` flag, then auto-recalculates `Temp` on `FileManager`:

| Scenario                 | `isNew` | Effect                                                    |
| ------------------------ | ------- | --------------------------------------------------------- |
| Entity created with file | `true`  | Adds usage row → `Temp=false`                             |
| Entity deleted           | `false` | Removes usage row → `Temp=true` if no other usages remain |
| Update — remove old file | `false` | Old file may become `Temp=true` if nothing else uses it   |
| Update — add new file    | `true`  | New file becomes `Temp=false`                             |

**Why explicit add/remove instead of a toggle?**  
A toggle would go wrong if the same endpoint is called twice (e.g. retry logic). An explicit `isNew` flag makes each call idempotent and intent-clear.

**Example — Create entity:**

```csharp
// isNew=true → add usage row, sets Temp=false
await _fileManagerClient.ChangeTempStatusAsync(
    fileId: request.ImageFileId,
    usageArea: "Artist",
    rowId: entity.Id.ToString(),
    isNew: true,
    tenantId: _tenantId,
    cancellationToken: ct);
```

**Example — Delete entity:**

```csharp
// isNew=false → remove usage row, sets Temp=true if no other usages remain
await _fileManagerClient.ChangeTempStatusAsync(
    fileId: entity.ImageFileId,
    usageArea: "Artist",
    rowId: entity.Id.ToString(),
    isNew: false,
    tenantId: _tenantId,
    cancellationToken: ct);
```

**Example — Update entity (file changed):**

```csharp
// Remove usage for old file
await _fileManagerClient.ChangeTempStatusAsync(oldFileId, "Artist", entity.Id.ToString(), isNew: false, _tenantId, ct);
// Add usage for new file
await _fileManagerClient.ChangeTempStatusAsync(newFileId, "Artist", entity.Id.ToString(), isNew: true, _tenantId, ct);
```

**Supported usage areas (convention):**

| UsageArea  | Entity type            |
| ---------- | ---------------------- |
| `"User"`   | Identity service users |
| `"Artist"` | Nasheed artists        |
| `"Song"`   | Nasheed songs          |

**See:** `SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md` for shared .NET client registration patterns and `PYTHON_SHARED_LIBRARY_GUIDE.md` for Python shared client usage.

---

## Security & Validation

### File Size Limits

**Configuration:**

```json
{
  "FileManagerOptions": {
    "MaxFileSizeInMB": 50
  }
}
```

**Validation:**

```csharp
// Automatic validation in SaveFileCommandValidator
if (file.Length > maxSizeBytes)
{
    throw new FileSizeExceededException($"File exceeds {maxSizeMB} MB limit");
}
```

### Allowed Extensions

**Configuration:**

```json
{
  "FileManagerOptions": {
    "AllowedExtensions": [".jpg", ".png", ".pdf", ".docx", ".xlsx", ".zip"]
  }
}
```

**Validation:**

```csharp
// Automatic validation in SaveFileCommandValidator
var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!allowedExtensions.Contains(extension))
{
    throw new FileExtensionNotAllowedException($"Extension {extension} not allowed");
}
```

### Access Control

**File Access Levels:**

- **Private**: Only owner can access
- **TenantWide**: Anyone in same tenant
- **Public**: Anyone with the link (no auth)

**System File Protection:**

```csharp
// Cannot delete system files
if (userId == null || userId == 0)
{
    throw new FileDeletionException("Cannot delete system files");
}
```

---

## Error Handling

### HTTP Status Codes

| Code                      | Scenario                           | Example                     |
| ------------------------- | ---------------------------------- | --------------------------- |
| 200 OK                    | File retrieved successfully        | GET /files/{id}             |
| 201 Created               | File uploaded successfully         | POST /files                 |
| 204 No Content            | File deleted successfully          | DELETE /files/{id}          |
| 400 Bad Request           | Validation error (size, extension) | File too large              |
| 401 Unauthorized          | Missing/invalid JWT token          | No Authorization header     |
| 403 Forbidden             | Insufficient permissions           | Not file owner              |
| 404 Not Found             | File does not exist                | GET /files/99999            |
| 500 Internal Server Error | Unexpected error                   | Database connection failure |

### Custom Exceptions

```csharp
// FileManager.Domain/Exceptions/
public class FileSizeExceededException : Exception { }
public class FileExtensionNotAllowedException : Exception { }
public class FileDeletionException : Exception { }
```

**Delete Operations:**

- Returns `false` for non-existent files (results in 404 HTTP response)
- No exceptions thrown for missing files (graceful handling)

---

## Testing

### Quick Start

**1. Run Service:**

```bash
cd src\Services\FileManager\FileManager.API
dotnet run
```

**2. Upload File (Postman):**

```http
POST https://localhost:5005/api/filemanager/files
Authorization: Bearer {token-from-identity-service}
x-tenant-id: ihsandev
Content-Type: multipart/form-data

Form Data:
  file: [select file]
  name: "test.pdf"
  group: 1
  temp: false
  userId: 123
```

**3. Access File Directly:**

```http
GET https://localhost:5005/ihsandev/123/shared/abc-123.pdf
# No authentication required (static file serving)
```

### Integration Tests

**Location:** `FileManager.API.Tests/`

**Key Test Classes:**

- `FileManagerEndpointsTests` - API endpoint testing
- `CustomWebApplicationFactory` - Test server setup
- `TenantTestHelper` - Multi-tenancy test utilities

**Run Tests:**

```bash
cd src\Services\FileManager\FileManager.API.Tests
dotnet test
```

---

## Common Issues

### Issue: Database not created

**Solution:** Automatic migration creates DB on first request. Ensure middleware is registered:

```csharp
if (multiTenancyEnabled)
    app.UseTenantDatabaseMigration<FileManagerDbContext>(builder.Configuration);
else
    app.UseDefaultDatabaseMigration<FileManagerDbContext>();
```

### Issue: 401 Unauthorized (Tenant User)

**Solution:** Verify JWT mode matches Identity Service:

```json
// Both services must have same JwtMode
{
  "MultiTenancy": {
    "JwtMode": "PerTenant" // Must match!
  }
}
```

### Issue: File URL returns 404

**Solution:**

- Verify `FileManagerOptions:RootStoragePath` exists and is accessible
- Check file path in database matches physical location
- Ensure static files middleware is registered: `app.UseStaticFiles()`

### Issue: Cache not working

**Solution:**

- Check Redis connection if `Redis:Enabled = true`
- System automatically falls back to MemoryCache if Redis unavailable
- Verify `Redis:ConnectionString` is correct

### Issue: Admin endpoint requires x-tenant-id

**Solution:**

- Use admin endpoints: `/api/filemanager/admin/files`
- Ensure `[BypassTenant]` attribute is applied
- Admin endpoints work WITHOUT `x-tenant-id` header

---

## Best Practices

### DO ✅

- Use `async/await` for all file operations
- Handle `null` returns gracefully (file may not exist)
- Pass `tenantId` when working with tenant-specific files
- Use cancellation tokens for long operations
- Log file operations for audit trail
- Validate file size and extensions
- Use service-to-service auth for internal calls
- Clean up temp files regularly

### DON'T ❌

- Assume file always exists (check for null)
- Call FileManager for every list item (performance impact)
- Ignore null returns from GetFileByIdAsync
- Use public endpoints for service-to-service calls
- Hardcode file URLs (use dynamic URL from response)
- Store large files in database (use file system/cloud storage)
- Mix tenant and admin endpoints (use correct endpoint group)

---

## Migration Guide

### Adding New Migration

```bash
cd src\Services\FileManager\FileManager.Infrastructure
dotnet ef migrations add MigrationName --startup-project ..\FileManager.API
```

### Manual Database Update (Development Only)

```bash
cd src\Services\FileManager\FileManager.API
dotnet ef database update
```

**Note:** Production databases are auto-migrated on first request.

---

## Related Documentation

- **SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md** - Using shared .NET service clients
- **PYTHON_SHARED_LIBRARY_GUIDE.md** - Python service client integration
- **MULTI_TENANCY_GUIDE.md** - Multi-tenancy configuration
- **BYPASS_TENANT_ENDPOINTS_GUIDE.md** - Creating admin/global endpoints
- **AUTOMATIC_DATABASE_MIGRATION.md** - Database auto-migration
- **CACHING_STRATEGY_COMPARISON.md** - Redis caching strategy guidance
- **SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md** - Service communication

---

**Last Updated:** January 27, 2026  
**Version:** 2.0.0  
**Status:** ✅ Production Ready
