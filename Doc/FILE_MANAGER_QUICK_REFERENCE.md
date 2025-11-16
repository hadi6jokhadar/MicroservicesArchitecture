# File Manager Service - Quick Reference

## Service Information

- **Port**: 5005 (Development)
- **Base URL**: `https://localhost:5005`
- **Swagger**: `https://localhost:5005/swagger`
- **Database**: Multi-provider support (PostgreSQL, SQL Server, MySQL, SQLite)
- **Architecture**: Clean Architecture with DDD + CQRS
- **Caching**: Redis distributed cache with 7-day TTL for tenant configs
- **Static Files**: Direct file serving from storage root with tenant isolation

## Quick Start

```bash
# Navigate to API project
cd src\Services\FileManager\FileManager.API

# Run the service
dotnet run
```

The database will be automatically created and migrated on first request.

## API Endpoints

### Upload File

```http
POST /api/filemanager/files
Content-Type: multipart/form-data
Authorization: Bearer {token}
x-tenant-id: {tenant-id}

Form Data:
  Name: string (required)
  File: IFormFile (required)
  Group: int (0-4, required) - Personal=0, Shared=1, System=2, Project=3, Archive=4
  Temp: bool (optional, default: true)
  UserId: int (optional, null for system files)

Response:
{
  "id": 1,
  "name": "document.pdf",
  "path": "ihsandev/1/personal/abc-123.pdf",  // Storage path (backend use)
  "url": "https://localhost:5005/ihsandev/1/personal/abc-123.pdf",  // Public URL (frontend use)
  "size": 1024576,
  "extension": ".pdf",
  "type": 3,  // Other
  "group": 0,  // Personal
  "created": "2025-01-15T10:30:00Z"
}
```

### Get File by ID

```http
GET /api/filemanager/files/{id}
Authorization: Bearer {token}
x-tenant-id: {tenant-id}

Response: Same as Upload response
```

### Download File

```http
GET /api/filemanager/files/{id}/download
Authorization: Bearer {token}
x-tenant-id: {tenant-id}

Response: Binary file stream
```

### Access File via Static URL

```http
GET /{tenantId}/{userId|system}/{group}/{filename}

Example: https://localhost:5005/ihsandev/1/personal/abc-123.pdf
Note: No authentication required (use with caution)
```

### List Files (Paginated)

```http
GET /api/filemanager/files?pageNumber=1&pageSize=10&userId=1&group=0&type=2&isTemp=false&searchTerm=photo
Authorization: Bearer {token}
x-tenant-id: {tenant-id}
```

### Update File Metadata

```http
PUT /api/filemanager/files/{id}
Content-Type: application/json
Authorization: Bearer {token}
x-tenant-id: {tenant-id}

{
  "name": "updated-name.pdf",
  "group": 3,
  "temp": false
}
```

### Delete File

```http
DELETE /api/filemanager/files/{id}
Authorization: Bearer {token}
x-tenant-id: {tenant-id}

Responses:
  204 No Content - File deleted successfully
  404 Not Found - File does not exist
```

### Delete All Temp Files

```http
DELETE /api/filemanager/files/temp
Authorization: Bearer {token}
x-tenant-id: {tenant-id}
```

### Delete Old Temp Files

```http
DELETE /api/filemanager/files/temp/old?days=30
Authorization: Bearer {token}
x-tenant-id: {tenant-id}
```

## Configuration

### appsettings.json Structure

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "Shared",
    "TenantServiceUrl": "https://localhost:5002"
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=filemanager_db;..."
  },
  "Jwt": {
    "Secret": "your-secret-minimum-32-chars",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp"
  },
  "FileManagerOptions": {
    "RootStoragePath": "C:\\FileStorage",
    "FilesSavePath": "uploads",
    "MaxFileSizeInMB": 50,
    "AllowedExtensions": [".jpg", ".pdf", ".docx", "..."]
  },
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379",
    "InstanceName": "FileManager:"
  }
}
```

## Enums

### FileGroup

- `Personal = 0`
- `Shared = 1`
- `System = 2`
- `Project = 3`
- `Archive = 4`

### FileType (Auto-detected from extension)

- `Music = 0` (.mp3, .wav, .flac, .aac)
- `Video = 1` (.mp4, .avi, .mov, .mkv)
- `Image = 2` (.jpg, .png, .gif, .bmp, .svg)
- `Other = 3` (all other extensions)

## File Storage Structure

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

**Path Pattern**: `{RootStoragePath}/{sanitized-tenant-id}/{FilesSavePath}/{userId|system}/{category}/{guid.ext}`

## Database Schema

**Table**: FileManager

| Column       | Type           | Description                    |
| ------------ | -------------- | ------------------------------ |
| Id           | int (PK)       | Auto-generated                 |
| Name         | varchar(255)   | File name                      |
| Extension    | varchar(10)    | File extension                 |
| Size         | bigint         | Size in bytes                  |
| Path         | varchar(500)   | Storage path                   |
| Group        | int            | FileGroup enum                 |
| Type         | int            | FileType enum                  |
| Temp         | bool           | Temporary flag (default: true) |
| UserId       | int (nullable) | Owner user ID                  |
| IsArchived   | bool           | Soft delete                    |
| Status       | bool           | Active/Inactive                |
| Created      | timestamptz    | UTC timestamp                  |
| LastModified | timestamptz    | UTC timestamp                  |

**Indexes**: UserId, Group, Type, Temp, IsArchived, Status, Created

## Multi-Tenancy

### Database-Per-Tenant

Each tenant gets their own isolated database. No TenantId column needed in the FileManager table.

**Request Flow**:

```
Client → x-tenant-id header → TenantMiddleware → TenantContext → Dynamic DbContext → Tenant's DB
```

**Modes**:

- **MultiTenancy:Enabled=true**: Requires `x-tenant-id` header, fetches config from Tenant Service
- **MultiTenancy:Enabled=false**: Uses `appsettings.json`, single database

## Testing with Postman

1. **Get JWT token** from Identity Service:

   ```http
   POST https://localhost:5001/api/auth/login
   {
     "email": "admin@example.com",
     "password": "Admin@123"
   }
   ```

2. **Upload file**:

   ```http
   POST https://localhost:5005/api/filemanager/files
   Authorization: Bearer {token-from-step-1}
   x-tenant-id: acme-corp

   Body (form-data):
     Name: my-document.pdf
     File: [select file]
     Group: 0
     Temp: true
     UserId: 1

   Response:
   {
     "id": 1,
     "path": "acme-corp/1/personal/abc-123.pdf",
     "url": "https://localhost:5005/acme-corp/1/personal/abc-123.pdf"
   }
   ```

3. **Access file directly**:
   ```http
   GET https://localhost:5005/acme-corp/1/personal/abc-123.pdf
   (Browser can display directly - no authentication needed)
   ```

4. **List files**:
   ```http
   GET https://localhost:5005/api/filemanager/files?pageNumber=1&pageSize=10
   Authorization: Bearer {token}
   x-tenant-id: acme-corp
   ```

## Caching Strategy

**Tenant Configuration Caching:**
- **7-day TTL**: Tenant configs cached for 7 days
- **Cache Keys**: `tenant_config_{tenantId}`, `all_active_tenants_with_config_page_{n}_size_{m}`
- **Invalidation**: Automatic on tenant Create/Update/Delete
- **Fallback**: Automatic fallback to in-memory cache if Redis unavailable

## Static File Serving

**Direct File Access:**
- Files served directly from `{RootStoragePath}` via PhysicalFileProvider
- URL Pattern: `/{tenantId}/{userId|system}/{group}/{filename}`
- No authentication required (consider security implications)
- CORS enabled for cross-origin access
- TenantMiddleware skips file extension requests

## Validation Rules

- **File Size**: Max 50 MB (configurable via `FileManagerOptions:MaxFileSizeInMB`)
- **Allowed Extensions**: Configurable via `FileManagerOptions:AllowedExtensions`
- **Required Fields**: Name, File
- **Business Rules**:
  - Extension must be in allowed list
  - File size must not exceed limit
  - Temp files auto-cleanup via background job

## Error Handling

**HTTP Status Codes**:

- `200 OK` - File retrieved successfully
- `201 Created` - File uploaded successfully
- `204 No Content` - File deleted successfully
- `404 Not Found` - File does not exist (returns false instead of throwing)
- `400 Bad Request` - Validation errors (size, extension, etc.)

**Custom Exceptions**:

- `FileSizeExceededException` - File too large (400)
- `FileExtensionNotAllowedException` - Extension not allowed (400)
- `FileDeletionException` - Cannot delete file (400)

**Note**: Delete operations return `false` for non-existent files instead of throwing exceptions, resulting in proper 404 HTTP responses.

## Project Structure

```
FileManager/
├── FileManager.API/           # Minimal APIs, Program.cs
├── FileManager.Application/   # Commands, Queries, Handlers, Validators
├── FileManager.Domain/        # Entities, Enums, Repository Interfaces
└── FileManager.Infrastructure/ # DbContext, Repositories, Storage, Services
```

## Development Tips

### Adding New Migration

```bash
cd src\Services\FileManager\FileManager.Infrastructure
dotnet ef migrations add MigrationName --startup-project ..\FileManager.API
```

### Manual Database Update (usually not needed)

```bash
cd src\Services\FileManager\FileManager.API
dotnet ef database update
```

### Running Tests

```bash
cd src\Services\FileManager\FileManager.API.Tests
dotnet test
```

## Common Issues

### Issue: Database not created

**Solution**: Automatic migration middleware creates DB on first request. Ensure middleware is registered:

```csharp
if (multiTenancyEnabled)
    app.UseTenantDatabaseMigration();
else
    app.UseDefaultDatabaseMigration();
```

### Issue: File not found after upload

**Solution**: Check `FileManagerOptions:RootStoragePath` exists and has write permissions

### Issue: 401 Unauthorized

**Solution**: Ensure JWT token is valid and matches `Jwt:Secret` from Identity Service

### Issue: Tenant not found

**Solution**: Ensure Tenant Service is running and `MultiTenancy:TenantServiceUrl` is correct

### Issue: File URL returns 404

**Solution**: 
- Verify `FileManagerOptions:RootStoragePath` exists and is accessible
- Check file path in database matches physical location
- Ensure static files middleware is registered: `app.UseStaticFiles()`

### Issue: Cache not working

**Solution**:
- Check Redis connection if `Redis:Enabled = true`
- System automatically falls back to MemoryCache if Redis unavailable
- Verify cache expiration settings (7 days for tenant configs)

### Issue: Delete returns 500 instead of 404

**Solution**: This was fixed - delete now returns `false` for non-existent files, resulting in proper 404 HTTP status

## Related Documentation

- **Complete Guide**: `Doc/FILE_MANAGER_SERVICE_GUIDE.md` - Architecture patterns, security, best practices
- **Multi-Tenancy**: `Doc/MULTI_TENANCY_GUIDE.md` - Multi-tenancy configuration
- **Service Integration**: `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md` - Creating new services
- **Project Isolation**: `Doc/PROJECT_ISOLATION_STRATEGY_GUIDE.md` - User isolation strategy

---

**Version**: 2.0.0  
**Last Updated**: November 2025

## Recent Updates (v2.0.0)

- ✅ **Redis Caching**: 7-day TTL for tenant configurations
- ✅ **Static File Serving**: Direct file access via public URLs
- ✅ **Path/URL Separation**: Separate fields for storage path and public URL
- ✅ **Improved Error Handling**: Delete returns 404 instead of 500 for missing files
- ✅ **Cache Invalidation**: Automatic cache refresh on tenant CRUD operations
- ✅ **Tenant Isolation**: TenantMiddleware skips static file requests
- ✅ **Physical File Deletion**: Fixed file cleanup from disk
