# File Manager Service

**Purpose:** Complete guide to the File Manager Service - handles file upload, storage, retrieval, and management with multi-tenancy support.  
**Last Updated:** August 13, 2026  
**Status:** ✅ Production Ready (v3.1.0)

---

## Overview

The File Manager Service is a centralized file storage microservice using Clean Architecture, DDD, and CQRS patterns. It provides secure file operations with tenant isolation, Redis caching, static file serving, and automatic cleanup.

**Port:** 5005 (Development)  
**Database:** PostgreSQL (multi-provider support)  
**Storage:** Local file system (production: Azure Blob, AWS S3, MinIO)  
**Caching:** Redis distributed cache (30-min TTL by default, `MultiTenancy:CacheExpirationMinutes`) with MemoryCache fallback

### Key Features

- ✅ **Multi-Tenancy**: Database-per-tenant isolation
- ✅ **Dual Endpoints**: Tenant endpoints (user files) + Admin endpoints (global files)
- ✅ **Static File Serving**: Direct file access via public URLs
- ✅ **Redis Caching**: 30-min tenant config cache with automatic invalidation (requires Redis enabled and aligned with Tenant Service — see Caching Strategy below)
- ✅ **Background Jobs**: Automatic temp file cleanup
- ✅ **Service-to-Service**: HTTP client for internal service calls
- ✅ **Security**: File size limits, extension validation, access control
- ✅ **Audio Normalization**: `.webm` uploads are converted to `.mp3` before persistence
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
    "RootStoragePath": "http://localhost:5005",
    "FilesSavePath": "C:/FileStorage",
    "FfmpegPath": "ffmpeg",
    "FfmpegTimeoutSeconds": 60,
    "MaxFileSizeBytes": 104857600,
    "AllowedExtensions": [".jpg", ".png", ".pdf", ".docx", ".xlsx", ".zip"],
    "ExtensionToTypeMapping": {
      ".jpg": "Image",
      ".png": "Image",
      ".pdf": "Other"
    }
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

> **`RootStoragePath` vs `FilesSavePath` — these are NOT nested.** `RootStoragePath` is the **public URL prefix** used to build the `url` field returned in every response (`FileManagerService` reads it as `_urlPrefix` and passes it to `FileManagerResponse.MapFrom`) — it is a URL like `http://localhost:5005`, never a filesystem path. `FilesSavePath` is the **actual physical disk root** where files are written and read (`LocalFileStorage`, the static-file middleware, and the `/files/{id}/download` endpoint all resolve the physical path as `Path.Combine(FilesSavePath, relativePath)`) — e.g. `C:/FileStorage` in dev, a container-mounted path in Docker. See **File Storage** below for the exact on-disk layout.

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
FileManagerOptions__FilesSavePath="/var/filemanager/storage"
FileManagerOptions__RootStoragePath="https://files.example.com"
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

> **Local file handle discipline:** `UploadToBlobAsync` reads the local file via `IFileStorage.GetAsync` and disposes the stream itself after the blob upload completes (`await using`) — `CloudflareR2Storage` sets `PutObjectRequest.AutoCloseStream = false` so the S3 SDK never closes it out from under a transient-failure retry, which means the caller must. A previous version of `UploadToBlobAsync` never disposed this stream at all, permanently leaking an open file handle on every blob upload (manual or automatic) and causing `DELETE /files/{id}` on that same file to fail with `IOException: ... being used by another process`. Fixed August 2026 — see Dotnet.instructions.md pitfall #30.
>
> **Auto-upload-on-save and EF Core tracking:** because the automatic on-save upload runs inline in the same request/`DbContext` scope as the original file save, `UploadToBlobAsync`'s own entity fetch (`GetByIdWithArchivedAsync`, no-tracking) could return a second, untracked instance of an entity the DbContext was already tracking from the save moments earlier — throwing an EF Core "already tracked" `InvalidOperationException` that `SaveFileCommandHandler` silently swallowed as an expected "blob not configured" skip, even though the blob upload to R2 had already succeeded. The file ended up orphaned in blob storage with `ExternalUrl` never persisted. Fixed August 2026 in the shared `Repository<T>` base class — see Dotnet.instructions.md pitfall #31.

### Automatic Upload on Save (Feature Flag)

Manually calling `POST /files/{id}/upload-to-blob` is not required when the tenant's
`autoUploadToExternalStorageEnabled` feature flag is turned on (see `Doc/FEATURE_FLAGS_GUIDE.md`).
When enabled, `SaveFileCommandHandler` (`FileManager.Application/Handlers/SaveFile/`) calls the
same `IFileManagerService.UploadToBlobAsync` logic immediately after every successful local
save — `ExternalUrl` is populated automatically, with no separate client call needed. The
auto-upload is awaited before the `POST /files` response is returned, so on success the
response body's `externalUrl` is already populated — the client never needs to re-fetch the
file to see it.

- **Gated by `IFeatureFlagService.IsEnabled(FeatureFlags.AutoUploadToExternalStorageEnabled, defaultValue: false)`** — off by default, so tenants without R2/blob configured are unaffected.
- **Never fails the upload request.** The local save has already succeeded by the time the auto-upload runs, so it's treated as a best-effort side effect:
  - `InvalidOperationException` ("blob storage is not configured") is logged at `LogInformation` and swallowed — this is the expected, common state for tenants without a `BlobStorage` configuration.
  - Any other exception is logged at `LogWarning` and swallowed — a blob outage must not turn into a failed file upload.
- The manual `POST /files/{id}/upload-to-blob` / `DELETE /files/{id}/remove-from-blob` endpoints continue to work unchanged regardless of the flag — the flag only controls whether the upload also happens automatically on save.
- Requires `FileManager.API`'s `Program.cs` to call `builder.Services.AddFeatureFlagService()` (registered alongside `AddMultiTenancy`/`AddInfrastructureServices`).

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

### Tenant Endpoints (`/api/v1/filemanager/*`)

**Authentication:** Requires JWT + `x-tenant-id` header  
**Authorization:** User, Admin, SuperAdmin roles

| Endpoint                       | Method | Description                                                              |
| ------------------------------ | ------ | ------------------------------------------------------------------------ |
| `/files`                       | POST   | Upload file to tenant database                                           |
| `/files/{id}`                  | GET    | Get file metadata                                                        |
| `/files`                       | GET    | List files (paginated, filtered)                                         |
| `/files/{id}`                  | PUT    | Update file metadata                                                     |
| `/files/{id}`                  | DELETE | Delete file (**hard delete** — removed from DB, local disk, and blob if `ExternalUrl` is set) |
| `/files/{id}/download`         | GET    | Download file (anonymous)                                                |
| `/files/{id}/upload-to-blob`   | POST   | Upload file to third-party blob (e.g. Cloudflare R2), sets `ExternalUrl` |
| `/files/{id}/remove-from-blob` | DELETE | Remove file from blob storage, clears `ExternalUrl`                      |

**Upload File Example:**

```http
POST https://localhost:5005/api/v1/filemanager/files
Authorization: Bearer {tenant-jwt}
x-tenant-id: ihsandev
Content-Type: multipart/form-data

Form Data:
  file: [binary file data]
  group: 1
  userId: 123   # only honored for Service/Admin/SuperAdmin callers — see note below
```

> **Only `file`, `group`, and `userId` are accepted form fields** — `FileManagerApiHandlers.SaveFile` binds exactly `IFormFile file`, `[FromForm] int? group` (defaults to `1` if omitted), and `[FromForm] int? userId`. There is no `name`, `type`, or `temp` form field:
> - **`name`** is always server-derived from the uploaded file's own filename (`Path.GetFileNameWithoutExtension`) — a client cannot rename the stored file via this field.
> - **`type`** is always server-computed from the extension/content-type (`FileManagerService.MapExtensionToFileType`, using `FileManagerOptions:ExtensionToTypeMapping` with a built-in fallback) — never taken from the client.
> - **`temp`** is always initialized to `true` on save; it only changes afterward through the usage-tracking mechanism (`ChangeTempStatusAsync` / `PATCH /internal/files/{id}/temp-status`), never as a direct upload field.
>
> **Uploader identity:** the `userId` form field is only trusted when the authenticated caller has the `Service`, `Admin`, or `SuperAdmin` role (service-to-service uploads on behalf of another user). For a plain `User`-role caller, the form field is ignored entirely — the file's owner is always the JWT-authenticated caller's own ID (via `ICurrentUserService.UserId`), so one user can never tag an upload as belonging to another user. See `FileManager.API/Handlers/FileManagerApiHandlers.cs`.

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

### WebM Upload Conversion

When an uploaded file extension is `.webm`, FileManager converts it to `.mp3` before saving metadata and storage content.

- Conversion flow: write `IFormFile` to a temporary `.webm` file, run FFmpeg, load converted `.mp3`, then delete temp files in `finally`.
- Stored metadata uses `.mp3` extension and `audio/mpeg` content type after conversion.
- Non-WebM uploads skip conversion and follow the standard save pipeline.
- FFmpeg executable resolution order: `FileManagerOptions:FfmpegPath` → system `PATH` → common Windows install paths.
- **Timeout protection:** the conversion is bounded by `FileManagerOptions:FfmpegTimeoutSeconds` (default 60s). If ffmpeg doesn't exit in time, the entire process tree is killed (`Process.Kill(entireProcessTree: true)`) and the upload fails with an internal-server-error response — a crafted file designed to make ffmpeg hang can no longer tie up a worker indefinitely.

> Runtime dependency: `ffmpeg` must be installed and available in `PATH` on environments running FileManager.
> Optional override: set `FileManagerOptions:FfmpegPath` to a full executable path, for example `C:/ffmpeg/bin/ffmpeg.exe`.

### FFmpeg Installation (Windows)

Install FFmpeg locally before using `.webm` uploads.

1. Install with `winget`:

```powershell
winget install --id Gyan.FFmpeg -e
```

2. Verify installation:

```powershell
ffmpeg -version
```

3. Configure FileManager if `ffmpeg` is not on `PATH`:

```json
"FileManagerOptions": {
  "FfmpegPath": "C:/ffmpeg/bin/ffmpeg.exe"
}
```

4. Restart `FileManager.API` after changing configuration.

### FFmpeg in Docker

When running FileManager in Docker, FFmpeg must be installed inside the container image, then `FfmpegPath` should point to the Linux binary path.

Example Dockerfile snippet:

```dockerfile
RUN apt-get update \
    && apt-get install -y ffmpeg \
    && rm -rf /var/lib/apt/lists/*
```

Example container configuration:

```json
"FileManagerOptions": {
  "FfmpegPath": "/usr/bin/ffmpeg"
}
```

If FFmpeg is missing in the container, `.webm` upload conversion will fail.

### Admin Endpoints (`/api/v1/filemanager/admin/*`)

**Authentication:** Requires JWT (no `x-tenant-id` header)  
**Authorization:** Service, SuperAdmin roles

> **Only five admin endpoints actually exist** (`FileManager.API/Endpoints/FileManagerEndpoints.cs`, `adminGroup`). There is **no** admin `POST /files` (upload), `GET /files/{id}`, `GET /files` (list), or `PUT /files/{id}` — the source file has a literal comment noting those were deferred (`// ... (Keep admin endpoints logic here or move to handlers if you wish, but for now focus on tenant)`) and never implemented. Admin/cross-tenant file uploads, reads, and updates do not exist today — only delete and blob operations do.

| Endpoint                                     | Method | Description                                                              |
| --------------------------------------------- | ------ | ------------------------------------------------------------------------- |
| `/files/{id}?tenantId=xxx`                    | DELETE | Delete file from any tenant (or global DB if `tenantId` omitted)         |
| `/files/temp/all`                             | DELETE | Delete all temp files (cross-tenant)                                     |
| `/files/temp/old?olderThanDays=7`              | DELETE | Delete old temp files (default `olderThanDays=7` if omitted)             |
| `/files/{id}/upload-to-blob?tenantId=xxx`     | POST   | Upload file to blob for any tenant (or global DB if `tenantId` omitted)  |
| `/files/{id}/remove-from-blob?tenantId=xxx`   | DELETE | Remove file from blob for any tenant (or global DB if `tenantId` omitted) |

**Delete a file from a specific tenant:**

```http
DELETE https://localhost:5005/api/v1/filemanager/admin/files/456?tenantId=ihsandev
Authorization: Bearer {global-jwt}
```

**Delete a file from the global database:**

```http
DELETE https://localhost:5005/api/v1/filemanager/admin/files/456
Authorization: Bearer {global-jwt}

# No tenantId = targets the global database
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
- ✅ **Content-Disposition hardening:** any extension other than a standard raster image (`.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.bmp`) or a sanitized `.svg` (see **SVG Sanitization** below) is served with `Content-Disposition: attachment` — e.g. PDF, Office docs, archives. `<img>`/`<audio>`/`<video>`/`fetch` subresource loads are unaffected either way — the header only changes behavior for top-level navigation. The same policy is applied when uploading to Cloudflare R2 (`ContentDisposition` object metadata set at upload time) via the shared `FileContentDispositionPolicy` helper in `FileManager.Infrastructure/Services/FileManagerService.cs`.

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
| Type           | int            | FileType enum (1-4)                             | ✅      |
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

`.webm` is the one special case: the service uses the uploaded MIME type to distinguish `audio/webm` as `Music` and `video/webm` as `Video`.

```csharp
public enum FileType
{
    Music = 1,   // .mp3, .wav, .flac, .aac
    Video = 2,   // .mp4, .avi, .mov, .mkv
    Image = 3,   // .jpg, .png, .gif, .bmp, .svg
    Other = 4    // All other extensions
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

> **`FilesSavePath` is the physical disk root — `RootStoragePath` is only a URL prefix, never a folder.** `LocalFileStorage.GetFullPath`/`DeleteAsync`/`ExistsAsync`/`GetAsync` all resolve the on-disk path as `Path.Combine(FilesSavePath, sanitized-tenant-id, relativePath)`; `RootStoragePath` never touches the filesystem — it's only concatenated onto the stored relative path to build the `url` field in `FileManagerService`'s `_urlPrefix` (`FileManagerResponse.MapFrom(entity, _urlPrefix)`).

### Storage Structure (on disk, under `FilesSavePath`)

```
{FilesSavePath}/
├── {sanitized-tenant-id}/
│   ├── {userId}/
│   │   ├── music/
│   │   ├── video/
│   │   ├── image/
│   │   │   └── {guid}.jpg
│   │   └── other/
│   └── system/
│       ├── music/
│       ├── video/
│       ├── image/
│       └── other/
└── {another-tenant}/
    └── ...
```

Category folders come from `FileType.ToString().ToLowerInvariant()` (`music`/`video`/`image`/`other`), not from `FileGroup` — `FileGroup` (Personal/Shared/System/Project/Archive/AI) is stored as a column on the entity but is not part of the physical folder path.

**Physical path pattern:**  
`{FilesSavePath}/{sanitized-tenant-id}/{userId|system}/{fileType}/{guid.ext}`

**Physical path example:**  
`C:/FileStorage/ihsandev/123/image/abc-123-def-456.jpg`

**Stored `Path` value (relative, forward slashes, includes tenant prefix):**  
`ihsandev/123/image/abc-123-def-456.jpg`

**Public `Url` value (`RootStoragePath` + stored `Path`):**  
`http://localhost:5005/ihsandev/123/image/abc-123-def-456.jpg`

### Response Fields

```json
{
  "path": "ihsandev/123/image/abc-123.jpg", // Storage path (backend) — relative, tenant-prefixed
  "url": "http://localhost:5005/ihsandev/123/image/abc-123.jpg" // Public URL = RootStoragePath + Path
}
```

- **Path**: Relative path stored in database (forward slashes normalized, includes the sanitized tenant folder)
- **Url**: `RootStoragePath` concatenated with `Path` (for direct access) — `RootStoragePath` is a URL, not a filesystem location

---

## Caching Strategy

### Redis Distributed Cache

**Tenant Configuration Caching:**

- **TTL**: `MultiTenancy:CacheExpirationMinutes` (30 min by default — not 7 days; see below)
- **Cache Keys**:
  - Individual: `tenant_config_{tenantId}`
  - Paginated: `all_active_tenants_with_config_page_{n}_size_{m}`
- **Invalidation**: Automatic on tenant Create/Update/Delete — **only when this service's `Redis` config actually shares the same Redis instance, connection, and `InstanceName` (`"MicroservicesApp:"`) as Tenant Service.** `UpdateTenantCommandHandler`/`ToggleTenantArchivedStatusCommandHandler`/`DeleteTenantCommandHandler` invalidate `tenant_config_{tenantId}` directly in Redis — any service plugged into that same keyspace sees the change on its very next request. A service with `Redis:Enabled: false`, a wrong config key name, or a mismatched `InstanceName` is silently isolated from this and only picks up tenant changes via the per-entry TTL expiry, the periodic `TenantConfigCacheRefreshService` (6 hours by default), or a restart. See Dotnet.instructions.md pitfall #29 — this exact misconfiguration (Redis disabled + `Configuration` instead of `ConnectionString` + `InstanceName: "FileManager_"`) previously caused newly-toggled tenant feature flags (e.g. `autoUploadToExternalStorageEnabled`) and `BlobStorage` settings to not take effect for up to 30 minutes after being saved. Fixed August 2026 by aligning FileManager's `appsettings.Development.json`/`appsettings.Docker.json` Redis config with Tenant Service's.
- **Fallback**: Automatic fallback to `IMemoryCache` if Redis unavailable — but note the fallback is *isolated per-instance* and only expires via TTL, with no cross-service invalidation possible at all (there's no shared store to invalidate). Prefer keeping Redis enabled and correctly aligned over relying on this fallback.

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

### TempFileCleanupJob (Hangfire recurring job)

**Purpose:** Automatically delete temporary files older than a retention period.

> **This runs as a Hangfire recurring job (`FileManager.Infrastructure/Jobs/TempFileCleanupJob.cs`), not as a plain `BackgroundService` polling loop.** An older `TempFileCleanupService` (`FileManager.Infrastructure/BackgroundJobs/TempFileCleanupService.cs`, a 24-hour `Task.Delay` loop) still exists in the codebase but is **not registered anywhere** (`InfrastructureServiceExtensions.AddInfrastructureServices` only calls `AddFileManagerHangfire`) — it is dead code superseded by the Hangfire job, the same pattern as Dotnet.instructions.md pitfall #25's `TenantCacheRefreshService`. Registered via `HangfireExtensions.RegisterFileManagerRecurringJobs()`, dashboard at `/admin/jobs/filemanager` (Basic Auth via `Hangfire:Dashboard:Username`/`Password`).

**Schedule:** Daily at **02:00 UTC** — Hangfire cron `"0 2 * * *"` with `TimeZoneInfo.Utc`.

**Retention period:** **Hardcoded as constants in `TempFileCleanupJob`** — `OlderThanDays = 7` for normal files, `AiOlderThanDays = 30` for `FileGroup.AI` files. There is **no** `FileManagerOptions:TempFileRetentionDays` (or any other) config key controlling this — `FileManagerOptions` has no retention-related property at all. Changing the retention window requires editing the constants in code.

**Features:**

- ✅ **Parallel Processing**: cleans all tenants concurrently via `Task.WhenAll` (multi-tenant mode)
- ✅ **Physical Deletion**: removes files from the database, local disk (`IFileStorage.DeleteAsync`), and blob storage (if `ExternalUrl` is set)
- ✅ **Error Handling**: continues on individual tenant/file failures
- ✅ **Logging**: per-tenant success/failure counts

**Manual Trigger (Admin Endpoint):**

```http
DELETE https://localhost:5005/api/v1/filemanager/admin/files/temp/old?olderThanDays=7
Authorization: Bearer {global-jwt}
```

> Query parameter is **`olderThanDays`** (default `7` if omitted) — not `days`. This admin endpoint only accepts `olderThanDays`; the AI-specific 30-day window is not exposed as a query parameter (it's a separate hardcoded `aiOlderThanDays` argument on the underlying command, defaulted to `30`).

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

## DTO Enrichment Pattern (Returning File Metadata in Responses)

> **Standard pattern for ALL services.** Any time an entity has a `FileId` or `ImageFileId` foreign key that clients need to display, the handler must enrich the DTO with full file metadata from FileManager. Do NOT rely on separate frontend fetches.

### What it means

Entities store only a `FileId` (int) FK to FileManager. API responses must include the full `FileManagerDto` object (name, url, etc.) populated by calling FileManager before returning.

> **`FileManagerDto` (the shared client model used for this enrichment) has NO `ExternalUrl` property.** `IFileManagerServiceClient.GetFileByIdAsync`/`GetFilesByIdsAsync` (`IhsanDev.Shared.Application/Common/Interfaces/IFileManagerServiceClient.cs`) return `FileManagerDto`, whose fields are `Id, Name, Extension, Size, Path, Url, Group, Type, Temp, Status, IsArchived, UserId, Created, LastModified` — `ExternalUrl` is not one of them. Only FileManager's **own** internal response type, `FileManagerResponse` (returned directly by FileManager's `/files/*` endpoints), carries `ExternalUrl`. Any enrichment done through this DTO-enrichment pattern will therefore always leave `externalUrl` absent/null on the enriched DTO — do not build frontend logic that expects an enriched `file.externalUrl` to ever be populated. If a consuming service genuinely needs the blob URL, either extend `FileManagerDto` (and the client's mapping) to include it, or read `FileManagerResponse.ExternalUrl` from FileManager's own endpoints directly instead of through this shared enrichment path.

**DTO before enrichment (not what clients want):**

```json
{ "fileId": 42, "file": null }
```

**DTO after enrichment (correct — note no `externalUrl`, since `FileManagerDto` doesn't carry it):**

```json
{
  "fileId": 42,
  "file": {
    "id": 42,
    "name": "song.mp3",
    "url": "https://localhost:5005/ihsandev/1/music/abc.mp3",
    "extension": ".mp3",
    "size": 5242880
  }
}
```

### Backend — Step-by-step

#### 1. Add `FileManagerDto?` property to the DTO

```csharp
// In your XxxDto.cs
using IhsanDev.Shared.Application.Common.Interfaces;

public class SongDto : BaseDto
{
    public int FileId { get; set; }
    public FileManagerDto? File { get; set; }  // ← add this
    // ...
}

public class ArtistDto : BaseDto
{
    public int? ImageFileId { get; set; }
    public FileManagerDto? ImageFile { get; set; }  // ← add this
    // ...
}
```

Set it to `null` in `MapFrom()` — the handler populates it:

```csharp
// In MapFrom():
FileId = entity.FileId,
File = null,  // Populated by handler via FileManager service
```

#### 2. Create a service-scoped Helper class

Create a helper in `YourService.Application/Helpers/YourServiceFileManagerHelper.cs`:

```csharp
using IhsanDev.Shared.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class YourServiceFileManagerHelper
{
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ILogger<YourServiceFileManagerHelper> _logger;
    private readonly string _tenantId;

    public YourServiceFileManagerHelper(
        IFileManagerServiceClient fileManagerClient,
        IConfiguration configuration,
        ILogger<YourServiceFileManagerHelper> logger)
    {
        _fileManagerClient = fileManagerClient;
        // Nasheed uses fixed tenantId from config — adjust if your service is multi-tenant
        _tenantId = configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException("MultiTenancy:TenantId not configured.");
        _logger = logger;
    }

    // Single entity enrichment
    public async Task EnrichEntityWithFileAsync(YourDto dto, CancellationToken ct = default)
    {
        if (dto.FileId <= 0) return;
        try
        {
            dto.File = await _fileManagerClient.GetFileByIdAsync(dto.FileId, _tenantId, ct);
        }
        catch (Exception ex) { _logger.LogError(ex, "..."); }
    }

    // Batch enrichment — prevents N+1 queries on list endpoints
    public async Task EnrichEntitiesWithFilesAsync(IEnumerable<YourDto> dtos, CancellationToken ct = default)
    {
        var list = dtos.ToList();
        var ids = list.Where(d => d.FileId > 0).Select(d => d.FileId).Distinct().ToList();
        if (ids.Count == 0) return;
        try
        {
            var dict = await _fileManagerClient.GetFilesByIdsAsync(ids, _tenantId, ct);
            foreach (var dto in list.Where(d => d.FileId > 0))
                dict.TryGetValue(dto.FileId, out dto.File!);
        }
        catch (Exception ex) { _logger.LogError(ex, "..."); }
    }
}
```

> **CRITICAL:** Always use `GetFilesByIdsAsync` (batch) on list handlers. Never call `GetFileByIdAsync` in a loop — that's an N+1 query.

#### 3. Register the helper in Program.cs

```csharp
// After AddFileManagerServiceClient(...)
builder.Services.AddFileManagerServiceClient(builder.Configuration, "YourService", ...);
builder.Services.AddScoped<YourService.Application.Helpers.YourServiceFileManagerHelper>();
```

#### 4. Inject helper in every query/command handler that returns the DTO

```csharp
// GetByIdQueryHandler
public async Task<YourDto?> Handle(GetByIdQuery request, CancellationToken ct)
{
    var entity = await _repository.GetByIdAsync(request.Id, ct);
    if (entity == null) return null;

    var dto = YourDto.MapFrom(entity);
    await _helper.EnrichEntityWithFileAsync(dto, ct);  // ← single
    return dto;
}

// GetListQueryHandler
public async Task<PaginatedList<YourDto>> Handle(GetListQuery request, CancellationToken ct)
{
    var (items, total) = await _repository.GetAllAsync(..., ct);
    var dtos = items.Select(YourDto.MapFrom).ToList();
    await _helper.EnrichEntitiesWithFilesAsync(dtos, ct);  // ← batch
    return new PaginatedList<YourDto> { Items = dtos, ... };
}

// CreateCommandHandler / UpdateCommandHandler
public async Task<YourDto> Handle(CreateYourCommand request, CancellationToken ct)
{
    // ... create entity ...
    var dto = YourDto.MapFrom(entity);
    await _helper.EnrichEntityWithFileAsync(dto, ct);  // ← single
    return dto;
}
```

#### 5. Multi-tenant services (ITenantContext instead of fixed tenantId)

For services where the tenant is resolved from the request (e.g. Identity), use `ITenantContext`:

```csharp
// Identity uses ProfilePictureHelper pattern:
var tenantId = _tenantContext.CurrentTenant?.TenantId;  // nullable — null = global DB
userDto.ProfilePicture = await _fileManagerClient.GetFileByIdAsync(id, tenantId, ct);
```

### Frontend (Angular)

#### 1. Add the enriched field to the frontend model

```typescript
// In your-entity.model.ts
import { IFileManagerResponse } from "@ihsan/core";

export interface SongModel {
  fileId: number;
  file?: IFileManagerResponse | null; // ← add this
  // ...
}

export interface ArtistModel {
  imageFileId?: number;
  imageFile?: IFileManagerResponse | null; // ← add this
  // ...
}
```

#### 2. Display the file in templates

**For images (artist image, profile picture):**

```html
@if (artist.imageFile?.url) {
<img [src]="artist.imageFile.url" [alt]="artist.name" class="artist-image" />
}
```

**For audio files (songs):**

```html
@if (song.file?.url) {
<audio controls>
  <source [src]="song.file.url" />
</audio>
}
```

> **`externalUrl` is not available through this enrichment path.** As noted above, the backend's `FileManagerDto` (what powers `song.file`/`artist.imageFile` here) has no `ExternalUrl` field, so a frontend model that mirrors it (`IFileManagerResponse`) should not rely on `externalUrl ?? url` fallbacks for data that arrived via cross-service enrichment — only `url` is guaranteed to be populated. `externalUrl` is only meaningful when the data came directly from FileManager's own `/files/*` responses (`FileManagerResponse`), which do include it.

### Real implementations in this codebase

| Service  | Helper class               | Location                                                  |
| -------- | -------------------------- | --------------------------------------------------------- |
| Identity | `ProfilePictureHelper`     | `Identity.Application/Helpers/ProfilePictureHelper.cs`    |
| Nasheed  | `NasheedFileManagerHelper` | `Nasheed.Application/Helpers/NasheedFileManagerHelper.cs` |

---

## Security & Validation

### File Size Limits

**Configuration:** the key is **`MaxFileSizeBytes`** (a `long`, in bytes) — there is no `MaxFileSizeInMB` key.

```json
{
  "FileManagerOptions": {
    "MaxFileSizeBytes": 104857600
  }
}
```

**Validation:** happens inline in `FileManagerService.SaveFileAsync` (`FileManager.Infrastructure/Services/FileManagerService.cs`) — **not** in `SaveFileCommandValidator`. The FluentValidation validator only checks that `File` is non-null and `Group` is a valid enum value; size and extension checks are business logic performed directly in the service before the file is saved:

```csharp
// FileManagerService.SaveFileAsync
if (file.Length > _options.MaxFileSizeBytes)
{
    throw new Domain.Exceptions.FileValidationException(LocalizationKeys.Exceptions.FileSizeExceeded, _localizationService);
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

**Validation:** also inline in `FileManagerService.SaveFileAsync`, immediately after the size check:

```csharp
var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension))
{
    throw new Domain.Exceptions.FileValidationException(LocalizationKeys.Exceptions.InvalidFileType, _localizationService);
}
```

### Content Signature (Magic-Byte) Validation

Extension-only validation is not sufficient — a file can be renamed to claim any extension regardless of its actual bytes (this is what makes an SVG-labeled-as-image or an HTML file disguised as a PDF practical). After the extension allowlist check, `FileManagerService.SaveFileAsync` reads the first bytes of the raw upload and compares them against the known magic number for the claimed extension:

| Extension                  | Signature checked                       |
| --------------------------- | ---------------------------------------- |
| `.png`                      | PNG header (`89 50 4E 47 0D 0A 1A 0A`)   |
| `.jpg` / `.jpeg`             | JPEG SOI marker (`FF D8 FF`)             |
| `.gif`                       | `GIF87a` / `GIF89a`                      |
| `.bmp`                       | `BM`                                     |
| `.webp`                      | RIFF container + `WEBP` at offset 8      |
| `.pdf`                       | `%PDF`                                   |
| `.zip` / `.docx` / `.xlsx`   | ZIP local-file-header (`PK\x03\x04`) — `.docx`/`.xlsx` are ZIP containers under the hood |
| `.webm` / `.mkv`             | EBML header (`1A 45 DF A3`)              |

Extensions with no reliable, well-known signature (plain text, some legacy Office formats, SVG's XML text body, etc.) are intentionally **not** checked — the validation is lenient there rather than inventing brittle heuristics that could reject legitimate uploads. A mismatch throws the same `InvalidFileType` exception as an unsupported extension.

### SVG Sanitization / Non-Raster Content-Disposition

`SaveFileAsync` sanitizes every uploaded `.svg` before it's persisted: parses it as XML (with DTD processing prohibited and no `XmlResolver`, to block XXE from a crafted external-entity reference), then strips `<script>` elements, `<foreignObject>` elements (arbitrary embedded HTML), every event-handler attribute (`onload`, `onclick`, ...), and any `href`/`xlink:href` set to a `javascript:` URI. A non-well-formed SVG is rejected outright with the same `InvalidFileType` exception as an unsupported extension — see `FileManagerService.SanitizeSvgAsync`.

Because of that sanitization, `.svg` is treated as a **safe-inline** extension in `FileContentDispositionPolicy`, alongside the standard raster images (`.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.bmp`) — every path that serves a stored file back (static file middleware, download endpoint, Cloudflare R2) serves these inline. Every other extension (PDF, Office docs, archives, etc.) still forces `Content-Disposition: attachment`.

An earlier version of this fix forced `attachment` on SVG too, on the theory that SVG "renders as executable markup." That broke every place the Angular admin frontend previews an uploaded SVG inline via `<img>` (Category icons/images, user profile pictures, artist images all go through the shared File Manager, which explicitly allows `.svg` as an image type) — rendering an SVG through `<img>` was already safe from script execution (browsers disable scripting in that specific context), so forcing a download there fixed nothing while breaking the preview. Sanitizing on upload closes the actual risk — a direct navigation to or embed of the raw SVG URL — without that regression.

### Access Control

> **No file-level access-control or ownership enforcement exists today.** `FileManagerEntity` has no access-level field (no `Private`/`TenantWide`/`Public` enum, no equivalent column) and no owner-check logic exists anywhere in the delete path. `DeleteFileCommandHandler` calls `IFileManagerService.DeleteFileAsync(id)` directly, and `FileManagerService.DeleteFileAsync` only checks whether the entity exists — it never compares the entity's `UserId` against the caller's identity, and there is no "system file" (`UserId == null`) protection that blocks deletion. Any authenticated caller with the `User`, `Admin`, or `SuperAdmin` role (and, in multi-tenant mode, a JWT whose `tenant_id` matches the `x-tenant-id` header) can delete **any** file ID in that tenant's database, including files owned by another user or files with no owner (`UserId == null`, e.g. system files uploaded via `system/{category}/...`).
>
> Actual access boundaries today are: (1) the tenant JWT/header cross-check (`JwtTenantVerificationMiddleware`), which prevents cross-**tenant** access but does nothing within a tenant, and (2) the role check on each endpoint (`User`/`Admin`/`SuperAdmin` for tenant endpoints, `Service`/`SuperAdmin` for admin endpoints). There is no per-file, per-owner authorization layer. If per-file access control is ever required, it does not exist yet and would need to be designed and added — do not assume it's already enforced.
>
> **Static file serving is fully anonymous by design** (see Static File Access above) — any file's public URL is accessible to anyone who has or guesses it, regardless of `FileGroup` or `UserId`.

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

The actual exception types defined in `FileManager.Domain/Exceptions/FileManagerExceptions.cs` are:

```csharp
// FileManager.Domain/Exceptions/FileManagerExceptions.cs
public class FileNotFoundException : NotFoundException      // LocalizationKeys.Exceptions.FileNotFound; carries FileId
public class FileValidationException : BadRequestException  // used for size, extension, magic-byte, and SVG-sanitization failures
public class FileStorageException : Exception                // physical save/read/delete failures in LocalFileStorage
```

There is **no** `FileSizeExceededException`, `FileExtensionNotAllowedException`, or `FileDeletionException` — those classes do not exist. Size limit, disallowed extension, magic-byte mismatch, and invalid SVG all throw the same `FileValidationException` (a `BadRequestException` subclass), distinguished only by which `LocalizationKeys.Exceptions.*` key is passed in (`FileSizeExceeded`, `InvalidFileType`, etc.) — see **Security & Validation** above.

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
POST https://localhost:5005/api/v1/filemanager/files
Authorization: Bearer {token-from-identity-service}
x-tenant-id: ihsandev
Content-Type: multipart/form-data

Form Data:
  file: [select file]
  group: 1
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

- Verify `FileManagerOptions:FilesSavePath` (the physical disk root — **not** `RootStoragePath`, which is only the URL prefix) exists and is accessible
- Check file path in database matches physical location under `FilesSavePath`
- Ensure static files middleware is registered: `app.UseStaticFiles()`

### Issue: Cache not working

**Solution:**

- Check Redis connection if `Redis:Enabled = true`
- System automatically falls back to MemoryCache if Redis unavailable
- Verify `Redis:ConnectionString` is correct

### Issue: Admin endpoint requires x-tenant-id

**Solution:**

- Use admin endpoints: `/api/v1/filemanager/admin/files`
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

**Last Updated:** August 13, 2026  
**Version:** 3.1.0  
**Status:** ✅ Production Ready
