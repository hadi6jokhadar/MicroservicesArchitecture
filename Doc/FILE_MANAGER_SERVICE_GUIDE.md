# 📁 File Manager Service Architecture Guide

## Should You Use One File Manager Service or Multiple?

**TL;DR: YES, use ONE shared File Manager Service across all your projects (A, B, C, etc.). However, the implementation differs from Identity/Tenant services - this is a STORAGE service, not a configuration service.**

---

## 📋 Table of Contents

1. [Quick Answer](#quick-answer)
2. [File Manager vs Identity/Tenant Services](#file-manager-vs-identitytenant-services)
3. [Why Shared File Manager Service?](#why-shared-file-manager-service)
4. [Architecture Patterns](#architecture-patterns)
5. [Implementation Options](#implementation-options)
6. [Security & Access Control](#security--access-control)
7. [Storage Strategy](#storage-strategy)
8. [Multi-Tenancy Considerations](#multi-tenancy-considerations)
9. [**Project Isolation Strategy (IMPORTANT)**](#project-isolation-strategy)
10. [Best Practices](#best-practices)
11. [Example Implementation](#example-implementation)

---

## Quick Answer

### ✅ **Recommended: Single Shared File Manager Service**

```
┌─────────────────────────────────────────────────────────────────┐
│                   SHARED FILE MANAGER SERVICE                    │
│                         (Port 5005)                              │
│                                                                   │
│  • File Upload/Download API                                      │
│  • Azure Blob Storage / S3 / File System                         │
│  • Access Control (User + Tenant based)                          │
│  • File Metadata Database (PostgreSQL)                           │
│  • Virus Scanning, Size Limits, Type Validation                  │
└────────────────────┬──────────────────┬─────────────────────────┘
                     │                  │
         ┌───────────▼──────┐  ┌────────▼───────────┐
         │   PROJECT A      │  │   PROJECT B        │
         │   (CRM)          │  │   (Inventory)      │
         │                  │  │                    │
         │  Uploads:        │  │  Uploads:          │
         │  • Invoices      │  │  • Product Images  │
         │  • Contracts     │  │  • Manuals         │
         │                  │  │                    │
         │  Calls API:      │  │  Calls API:        │
         │  POST /files     │  │  POST /files       │
         │  GET /files/{id} │  │  GET /files/{id}   │
         └──────────────────┘  └────────────────────┘
```

### **Key Characteristics:**

| Aspect              | File Manager Service                                       |
| ------------------- | ---------------------------------------------------------- |
| **Type**            | Storage/Media Service                                      |
| **Purpose**         | Store, retrieve, manage files for all projects             |
| **Deployment**      | ONE shared service                                         |
| **Storage Backend** | Azure Blob Storage, AWS S3, MinIO, or local file system    |
| **Access Control**  | User ID + Tenant ID + File Permissions                     |
| **Metadata**        | File info stored in PostgreSQL (name, size, owner, tenant) |
| **Scaling**         | Horizontally scalable (stateless API + cloud storage)      |

---

## File Manager vs Identity/Tenant Services

### **Comparison Table**

| Aspect              | Identity Service          | Tenant Service            | **File Manager Service**          |
| ------------------- | ------------------------- | ------------------------- | --------------------------------- |
| **Data Type**       | Users, passwords, roles   | Tenant configs, settings  | **Files, documents, images**      |
| **Data Size**       | Small (~1KB per user)     | Small (~1KB per tenant)   | **Large (MB to GB per file)**     |
| **Access Pattern**  | Login → Cache for session | Per request → 5-min cache | **Direct download (streaming)**   |
| **Caching**         | JWT (stateless)           | IMemoryCache              | **CDN / Browser cache**           |
| **Storage**         | PostgreSQL                | PostgreSQL                | **Blob Storage + Metadata DB**    |
| **API Calls**       | Low (only login)          | Medium (cached)           | **High (uploads/downloads)**      |
| **Bandwidth**       | Low                       | Low                       | **Very High**                     |
| **Multi-Tenancy**   | No (users are global)     | Yes (tenant-specific)     | **Yes (files scoped to tenants)** |
| **Shared Service?** | ✅ YES                    | ✅ YES                    | **✅ YES**                        |

### **Key Difference: Storage Service Pattern**

**Identity/Tenant Services:**

- Return **small JSON responses** (user data, tenant config)
- Cache aggressively (5 minutes, in-memory)
- Low bandwidth usage

**File Manager Service:**

- Returns **large binary files** (images, PDFs, videos)
- Stream directly from storage (no caching in API)
- High bandwidth usage
- Use CDN for caching (not IMemoryCache)

---

## Why Shared File Manager Service?

### **✅ Advantages of Shared File Manager**

#### **1. Centralized Storage Management**

```
Shared File Manager:
└─ Azure Blob Storage
   ├─ tenant_123/
   │  ├─ project_a/invoices/invoice_001.pdf
   │  └─ project_b/products/widget.png
   └─ tenant_456/
      ├─ project_a/contracts/agreement.pdf
      └─ project_b/manuals/guide.pdf

✅ Single storage account
✅ Unified backup strategy
✅ Consistent access policies
```

**vs Separate File Managers (WRONG):**

```
Project A File Manager:
└─ Storage A
   └─ tenant_123/invoice_001.pdf

Project B File Manager:
└─ Storage B
   └─ tenant_123/widget.png

❌ Same tenant has files scattered across storages
❌ Multiple storage accounts = higher cost
❌ Backup complexity (2 systems to backup)
```

#### **2. Unified Access Control**

**Shared Service:**

```csharp
// One permission system for all files
if (!await _authService.CanAccessFile(fileId, userId, tenantId))
    return Forbid();

// Works consistently across all projects
```

**Separate Services:**

```csharp
// Project A has permission rules
// Project B has DIFFERENT permission rules
// User confused about why they can access file in A but not in B
```

#### **3. Cost Efficiency**

| Item            | Shared File Manager | Separate (2 Projects) |
| --------------- | ------------------- | --------------------- |
| Storage Account | 1 × $20/month       | 2 × $20/month = $40   |
| Bandwidth       | $0.05/GB            | $0.05/GB × 2 sources  |
| Compute (API)   | 1 × App Service     | 2 × App Service       |
| **Total**       | **~$50/month**      | **~$100/month**       |

**Savings: 50% cost reduction**

#### **4. Simplified File Sharing**

**Scenario:** User uploads invoice in Project A (CRM), wants to attach it to product in Project B (Inventory).

**With Shared Service:**

```
1. Upload invoice.pdf → File ID: abc-123
2. In Project A: Link order to file abc-123
3. In Project B: Link product to SAME file abc-123
✅ One file, multiple references, no duplication
```

**With Separate Services:**

```
1. Upload invoice.pdf to Project A storage
2. Want to use in Project B? Must download and re-upload
❌ File duplicated (wastes storage)
❌ Two versions can get out of sync
```

#### **5. Cross-Project Analytics**

```csharp
// Shared File Manager: Easy to query all files
var totalStorage = await _db.Files
    .Where(f => f.TenantId == tenantId)
    .SumAsync(f => f.SizeInBytes);

// Report: "Tenant 123 uses 5.2 GB across all projects"
```

**With separate services:** Must query multiple databases and aggregate manually.

---

## Architecture Patterns

### **Pattern 1: Shared File Manager + Cloud Storage (RECOMMENDED)**

```
┌─────────────────────────────────────────────────────────────────┐
│                    FILE MANAGER SERVICE                          │
│                                                                   │
│  ┌──────────────────┐          ┌──────────────────────┐         │
│  │   API Layer      │          │  Metadata Database   │         │
│  │                  │          │  (PostgreSQL)        │         │
│  │  • Upload        │◄────────►│                      │         │
│  │  • Download      │          │  Files Table:        │         │
│  │  • Delete        │          │  • Id, FileName      │         │
│  │  • List          │          │  • TenantId, UserId  │         │
│  │  • Permissions   │          │  • StoragePath       │         │
│  └────────┬─────────┘          │  • Size, MimeType    │         │
│           │                     └──────────────────────┘         │
│           │                                                       │
│           ▼                                                       │
│  ┌──────────────────────────────────────────────────┐           │
│  │         STORAGE ABSTRACTION LAYER                │           │
│  │  (IFileStorageProvider interface)                │           │
│  └───────────────────┬──────────────────────────────┘           │
└────────────────────┬─┴───────────────────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ Azure Blob   │ │   AWS S3     │ │   MinIO      │
│  Storage     │ │              │ │ (Self-hosted)│
│              │ │              │ │              │
│ (Production) │ │ (Alternative)│ │ (Development)│
└──────────────┘ └──────────────┘ └──────────────┘
```

**Benefits:**

- ✅ Cloud-native (Azure/AWS handles scalability)
- ✅ 99.99% availability SLA
- ✅ Automatic redundancy and backup
- ✅ CDN integration for fast downloads
- ✅ Pay-per-use pricing

### **Pattern 2: Direct Storage Access (NOT Recommended)**

```
┌──────────────┐                    ┌──────────────────┐
│  Project A   │───────────────────▶│  Azure Blob      │
│              │  Direct upload     │  Storage         │
└──────────────┘                    │                  │
                                    │  ❌ No metadata  │
┌──────────────┐                    │  ❌ No access    │
│  Project B   │───────────────────▶│     control      │
│              │  Direct upload     │  ❌ No audit log │
└──────────────┘                    └──────────────────┘
```

**Problems:**

- ❌ No centralized access control
- ❌ No file metadata (who uploaded, when, tenant)
- ❌ No virus scanning
- ❌ No file type validation
- ❌ Hard to track storage usage per tenant

---

## Implementation Options

### **Option 1: Separate File Manager Microservice (RECOMMENDED)**

**Create a dedicated service:**

```
src/Services/FileManager/
├── FileManager.API/
│   ├── Controllers/
│   │   └── FilesController.cs
│   ├── Services/
│   │   ├── IFileStorageProvider.cs
│   │   ├── AzureBlobStorageProvider.cs
│   │   ├── LocalFileStorageProvider.cs
│   │   └── FileAccessControlService.cs
│   └── appsettings.json
├── FileManager.Application/
│   ├── Commands/
│   │   ├── UploadFileCommand.cs
│   │   └── DeleteFileCommand.cs
│   └── Queries/
│       └── GetFileQuery.cs
├── FileManager.Domain/
│   └── Entities/
│       └── FileMetadata.cs
└── FileManager.Infrastructure/
    ├── Persistence/
    │   └── FileManagerDbContext.cs
    └── Storage/
        └── AzureBlobStorageProvider.cs
```

**API Endpoints:**

```
POST   /api/files                 - Upload file
GET    /api/files/{id}            - Download file
GET    /api/files/{id}/metadata   - Get file info
DELETE /api/files/{id}            - Delete file
GET    /api/files                 - List files (with filters)
PUT    /api/files/{id}/permissions - Update file permissions
```

### **Option 2: Shared Library (NOT Recommended for Files)**

```csharp
// ❌ Don't do this for file storage
IhsanDev.Shared.Storage/
└── Services/
    └── FileStorageService.cs

// Each project includes this library and uploads directly to storage
// Problems:
// - No centralized access control
// - Duplicate code in each project
// - Hard to audit file access
```

**Why this doesn't work:**

- Each project would need storage credentials (security risk)
- No single source of truth for file metadata
- Can't track which project uploaded which file

---

## Security & Access Control

### **File Access Control Levels**

#### **Level 1: User-Based Access**

```csharp
public class FileMetadata
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public Guid OwnerId { get; set; }  // User who uploaded
    public DateTime UploadedAt { get; set; }

    // Access control
    public FileAccessLevel AccessLevel { get; set; }
}

public enum FileAccessLevel
{
    Private,      // Only owner can access
    TenantWide,   // Anyone in the same tenant
    Public        // Anyone with the link (no auth required)
}
```

**Example:**

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> Download(Guid id)
{
    var file = await _db.Files.FindAsync(id);
    if (file == null) return NotFound();

    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var tenantId = _tenantContext.CurrentTenant?.TenantId;

    // Check permissions
    if (file.AccessLevel == FileAccessLevel.Private && file.OwnerId.ToString() != userId)
        return Forbid("You don't own this file");

    if (file.AccessLevel == FileAccessLevel.TenantWide && file.TenantId != tenantId)
        return Forbid("File belongs to different tenant");

    // Download from storage
    var stream = await _storageProvider.DownloadAsync(file.StoragePath);
    return File(stream, file.MimeType, file.FileName);
}
```

#### **Level 2: Tenant-Based Isolation**

```csharp
// Files are ALWAYS scoped to tenants
public class FileMetadata
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }  // ← Required
    public Guid OwnerId { get; set; }

    // Storage path includes tenant ID
    // tenant_123/invoices/file.pdf
    public string StoragePath { get; set; }
}

// Automatic tenant filtering
[Authorize]
[HttpGet]
public async Task<IActionResult> ListFiles()
{
    var tenantId = _tenantContext.CurrentTenant?.TenantId;

    var files = await _db.Files
        .Where(f => f.TenantId == tenantId)  // ← Automatic tenant filter
        .ToListAsync();

    return Ok(files);
}
```

#### **Level 3: Role-Based Permissions**

```csharp
public class FileMetadata
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }
    public Guid OwnerId { get; set; }

    // Advanced permissions
    public List<FilePermission> Permissions { get; set; }
}

public class FilePermission
{
    public Guid FileId { get; set; }
    public Guid? UserId { get; set; }      // Specific user (optional)
    public string? Role { get; set; }      // Or role (Admin, Editor, Viewer)
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
}

// Usage
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(Guid id)
{
    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

    var file = await _db.Files
        .Include(f => f.Permissions)
        .FirstOrDefaultAsync(f => f.Id == id);

    if (file == null) return NotFound();

    // Check if user has delete permission
    var hasPermission = file.Permissions.Any(p =>
        (p.UserId == userId || p.Role == userRole) && p.CanDelete
    );

    if (!hasPermission) return Forbid("No delete permission");

    await _storageProvider.DeleteAsync(file.StoragePath);
    _db.Files.Remove(file);
    await _db.SaveChangesAsync();

    return NoContent();
}
```

---

## Storage Strategy

### **Storage Path Structure**

#### **Option 1: Tenant-Scoped Paths (RECOMMENDED)**

```
Azure Blob Container: "files"
├─ tenant_123/
│  ├─ users/
│  │  ├─ user_456/
│  │  │  ├─ profile_photo.jpg
│  │  │  └─ documents/contract.pdf
│  ├─ projects/
│  │  ├─ project_a/
│  │  │  ├─ invoices/inv_001.pdf
│  │  │  └─ reports/report.xlsx
│  │  └─ project_b/
│  │     └─ products/widget_image.png
│  └─ shared/
│     └─ company_logo.png
└─ tenant_456/
   └─ ... (same structure)
```

**Benefits:**

- ✅ Easy to backup/restore all files for a tenant
- ✅ Easy to calculate storage usage per tenant
- ✅ Easy to delete all tenant data (GDPR compliance)

#### **Option 2: Flat Structure with Metadata**

```
Azure Blob Container: "files"
├─ abc-123-def-456.pdf   (metadata: tenantId=123, project=A)
├─ ghi-789-jkl-012.png   (metadata: tenantId=123, project=B)
└─ mno-345-pqr-678.jpg   (metadata: tenantId=456, project=A)
```

**Benefits:**

- ✅ Simpler path generation (just GUID)
- ✅ No path conflicts

**Drawbacks:**

- ❌ Can't easily browse files by tenant
- ❌ Harder to calculate usage per tenant

### **Recommended Implementation**

```csharp
public class AzureBlobStorageProvider : IFileStorageProvider
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName = "files";

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string tenantId,
        string projectName,
        Guid userId)
    {
        // Generate path: tenant_123/project_a/invoices/abc-def-ghi.pdf
        var fileExtension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        var blobPath = $"tenant_{tenantId}/{projectName}/{uniqueFileName}";

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        // Upload with metadata
        var metadata = new Dictionary<string, string>
        {
            { "tenantId", tenantId },
            { "userId", userId.ToString() },
            { "originalFileName", fileName },
            { "uploadedAt", DateTime.UtcNow.ToString("o") }
        };

        await blobClient.UploadAsync(fileStream, metadata: metadata);

        return blobPath;  // Store this in database
    }

    public async Task<Stream> DownloadAsync(string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var response = await blobClient.DownloadAsync();
        return response.Value.Content;
    }

    public async Task DeleteAsync(string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        await blobClient.DeleteIfExistsAsync();
    }
}
```

---

## Multi-Tenancy Considerations

### **Tenant Isolation Strategies**

#### **Strategy 1: Single Storage Account, Tenant-Prefixed Paths (RECOMMENDED)**

```
Storage Account: "myapp-files"
├─ Container: "files"
   ├─ tenant_123/... (Company A files)
   ├─ tenant_456/... (Company B files)
   └─ tenant_789/... (Company C files)
```

**Pros:**

- ✅ Simple to manage (one account)
- ✅ Lower cost (consolidated storage)
- ✅ Easy to implement

**Cons:**

- ❌ All tenants share same storage account (less isolation)
- ❌ Storage limits apply to all tenants combined

#### **Strategy 2: Separate Containers Per Tenant**

```
Storage Account: "myapp-files"
├─ Container: "tenant-123"
│  └─ files/...
├─ Container: "tenant-456"
│  └─ files/...
└─ Container: "tenant-789"
   └─ files/...
```

**Pros:**

- ✅ Better isolation (separate containers)
- ✅ Can set per-container access policies
- ✅ Easier to migrate tenant to different storage

**Cons:**

- ❌ More complex (dynamic container creation)
- ❌ Azure has container limits (5000 per account)

#### **Strategy 3: Separate Storage Accounts Per Tenant (Enterprise)**

```
Storage Account: "tenant123-files"
└─ Container: "files"
   └─ ... (only tenant 123 files)

Storage Account: "tenant456-files"
└─ Container: "files"
   └─ ... (only tenant 456 files)
```

**Pros:**

- ✅ Complete isolation (regulatory compliance)
- ✅ Per-tenant storage limits
- ✅ Can host in tenant's own Azure subscription

**Cons:**

- ❌ Much higher cost ($20/month × number of tenants)
- ❌ Complex management (N storage accounts)
- ❌ Only needed for enterprise/regulated industries

**Recommendation for most SaaS apps:** **Strategy 1** (tenant-prefixed paths)

---

## Project Isolation Strategy

### **Critical Question: Should Users Be Shared Across Projects?**

**Scenario:** You have multiple projects (A, B, C) using the same Identity Service. Should a user with email `john@example.com` have:

- **Option 1:** ONE user account that can access multiple projects? ✅ **RECOMMENDED**
- **Option 2:** SEPARATE user accounts per project? ❌ **NOT RECOMMENDED**

### **✅ RECOMMENDED: Shared Users with Project Memberships**

**Architecture:**

```
Identity Service Database:
├─ Users table (john@example.com → ONE record)
└─ UserProjects table (john can be in ProjectA, ProjectB, ProjectC)

File Manager Service:
├─ Files filtered by ProjectId (ProjectA files separate from ProjectB files)
└─ Same user (john@example.com) can upload files to any project they have access to
```

**Example:**

```
User: john@example.com (UserId: 123)
├─ Project A: Admin role
├─ Project B: Viewer role
└─ Project C: Editor role

Files uploaded by John:
├─ File 1: tenant_456/ProjectA/invoice.pdf (ProjectId: ProjectA)
├─ File 2: tenant_456/ProjectB/product.png (ProjectId: ProjectB)
└─ File 3: tenant_456/ProjectC/design.fig (ProjectId: ProjectC)
```

**Key Points:**

- ✅ John logs in ONCE with `john@example.com`
- ✅ JWT token includes list of projects John has access to
- ✅ John can upload files to ProjectA, B, or C (based on permissions)
- ✅ Each file has `ProjectId` column identifying which project it belongs to
- ✅ Files are isolated by project (ProjectA files separate from ProjectB files)
- ✅ Storage paths include project folder: `tenant_123/ProjectA/file.pdf`

**File Metadata Schema:**

```csharp
public class FileMetadata
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }       // Which tenant owns this file
    public Guid OwnerId { get; set; }          // Which user uploaded it (john@example.com)
    public string ProjectId { get; set; }      // Which project it belongs to (ProjectA, ProjectB, etc.)
    public string StoragePath { get; set; }    // tenant_123/ProjectA/invoice.pdf
}
```

**Why This Works:**

- ✅ **Users are shared** (one login across all projects)
- ✅ **Files are isolated by project** (ProjectId column + storage path)
- ✅ **Single sign-on** (login once, access multiple projects)
- ✅ **Better UX** (no need to remember multiple passwords)

### **Complete Implementation Guide**

For complete details on user isolation strategy, including:

- Database schema (Users, UserProjects tables)
- JWT token structure with project memberships
- Access control implementation
- Migration from isolated to shared users

**See: PROJECT_ISOLATION_STRATEGY_GUIDE.md**

---

## Best Practices

### **1. File Size Limits**

```csharp
[HttpPost]
public async Task<IActionResult> Upload(IFormFile file)
{
    // Limit: 10 MB
    const long maxSizeBytes = 10 * 1024 * 1024;

    if (file.Length > maxSizeBytes)
    {
        return BadRequest(new
        {
            error = "File too large",
            maxSizeMB = 10,
            fileSizeMB = file.Length / 1024.0 / 1024.0
        });
    }

    // Continue with upload...
}
```

### **2. File Type Validation**

```csharp
private static readonly string[] AllowedMimeTypes = new[]
{
    "image/jpeg",
    "image/png",
    "image/gif",
    "application/pdf",
    "application/vnd.ms-excel",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
};

[HttpPost]
public async Task<IActionResult> Upload(IFormFile file)
{
    if (!AllowedMimeTypes.Contains(file.ContentType))
    {
        return BadRequest(new
        {
            error = "File type not allowed",
            allowedTypes = AllowedMimeTypes
        });
    }

    // Continue...
}
```

### **3. Virus Scanning (Production)**

```csharp
public async Task<IActionResult> Upload(IFormFile file)
{
    // Scan with antivirus before storing
    var scanResult = await _antivirusService.ScanAsync(file.OpenReadStream());

    if (!scanResult.IsClean)
    {
        _logger.LogWarning("Virus detected in file upload: {FileName}", file.FileName);
        return BadRequest(new { error = "File failed security scan" });
    }

    // Continue with upload...
}
```

### **4. Generate Secure Download URLs (Temporary Access)**

```csharp
[HttpGet("{id}/download-url")]
public async Task<IActionResult> GetDownloadUrl(Guid id)
{
    var file = await _db.Files.FindAsync(id);
    if (file == null) return NotFound();

    // Check permissions...

    // Generate SAS token (valid for 1 hour)
    var sasUrl = await _storageProvider.GenerateSasUrlAsync(
        file.StoragePath,
        expiresIn: TimeSpan.FromHours(1)
    );

    return Ok(new { downloadUrl = sasUrl, expiresAt = DateTime.UtcNow.AddHours(1) });
}
```

**Azure Blob Storage SAS URL:**

```csharp
public async Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan expiresIn)
{
    var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
    var blobClient = containerClient.GetBlobClient(blobPath);

    var sasBuilder = new BlobSasBuilder
    {
        BlobContainerName = _containerName,
        BlobName = blobPath,
        Resource = "b",
        StartsOn = DateTimeOffset.UtcNow,
        ExpiresOn = DateTimeOffset.UtcNow.Add(expiresIn)
    };

    sasBuilder.SetPermissions(BlobSasPermissions.Read);

    var sasUri = blobClient.GenerateSasUri(sasBuilder);
    return sasUri.ToString();
}
```

### **5. Audit Logging**

```csharp
public async Task<IActionResult> Upload(IFormFile file)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var tenantId = _tenantContext.CurrentTenant?.TenantId;

    // Upload file...

    // Log the upload event
    _logger.LogInformation(
        "File uploaded: {FileName}, User: {UserId}, Tenant: {TenantId}, Size: {SizeBytes}",
        file.FileName,
        userId,
        tenantId,
        file.Length
    );

    // Store audit record in database
    await _db.AuditLogs.AddAsync(new AuditLog
    {
        Action = "FileUploaded",
        UserId = userId,
        TenantId = tenantId,
        Details = $"Uploaded {file.FileName} ({file.Length} bytes)",
        Timestamp = DateTime.UtcNow
    });

    await _db.SaveChangesAsync();

    return Ok(fileMetadata);
}
```

---

## Example Implementation

### **Complete File Manager Service**

#### **1. File Metadata Entity**

```csharp
// FileManager.Domain/Entities/FileMetadata.cs
public class FileMetadata
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;

    // Multi-tenancy
    public string TenantId { get; set; } = string.Empty;

    // Ownership
    public Guid OwnerId { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;

    // ✅ IMPORTANT: Project tracking
    // This identifies WHICH project the file belongs to (ProjectA, ProjectB, etc.)
    // Note: Users are NOT isolated per project - they can access multiple projects!
    // See PROJECT_ISOLATION_STRATEGY_GUIDE.md for details
    public string ProjectId { get; set; } = string.Empty;  // "ProjectA", "ProjectB", "ProjectC"

    // Access control
    public FileAccessLevel AccessLevel { get; set; }

    // Metadata
    public DateTime UploadedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Tags (optional)
    public string Tags { get; set; } = string.Empty;  // JSON array: ["invoice", "2024"]
}

public enum FileAccessLevel
{
    Private = 0,
    TenantWide = 1,
    Public = 2
}
```

#### **2. Files Controller**

```csharp
// FileManager.API/Controllers/FilesController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly FileManagerDbContext _db;
    private readonly IFileStorageProvider _storage;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<FilesController> _logger;

    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string projectName,
        [FromForm] FileAccessLevel accessLevel = FileAccessLevel.Private)
    {
        // Validation
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        const long maxSize = 10 * 1024 * 1024; // 10 MB
        if (file.Length > maxSize)
            return BadRequest($"File too large (max 10 MB)");

        // Get user and tenant info
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown";
        var tenantId = _tenantContext.CurrentTenant?.TenantId ?? throw new Exception("No tenant");

        // Upload to storage
        var storagePath = await _storage.UploadAsync(
            file.OpenReadStream(),
            file.FileName,
            tenantId,
            projectName,
            userId
        );

        // Save metadata
        var fileMetadata = new FileMetadata
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            MimeType = file.ContentType,
            SizeInBytes = file.Length,
            StoragePath = storagePath,
            TenantId = tenantId,
            OwnerId = userId,
            OwnerEmail = userEmail,
            ProjectName = projectName,
            AccessLevel = accessLevel,
            UploadedAt = DateTime.UtcNow
        };

        await _db.Files.AddAsync(fileMetadata);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "File uploaded: {FileId}, Tenant: {TenantId}, Project: {ProjectName}",
            fileMetadata.Id,
            tenantId,
            projectName
        );

        return Ok(new
        {
            fileId = fileMetadata.Id,
            fileName = fileMetadata.FileName,
            size = fileMetadata.SizeInBytes,
            uploadedAt = fileMetadata.UploadedAt
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Download(Guid id)
    {
        var file = await _db.Files.FindAsync(id);
        if (file == null || file.IsDeleted) return NotFound();

        // Access control
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = _tenantContext.CurrentTenant?.TenantId;

        if (file.AccessLevel == FileAccessLevel.Private && file.OwnerId.ToString() != userId)
            return Forbid("You don't own this file");

        if (file.TenantId != tenantId && file.AccessLevel != FileAccessLevel.Public)
            return Forbid("File belongs to different tenant");

        // Download from storage
        var stream = await _storage.DownloadAsync(file.StoragePath);

        _logger.LogInformation(
            "File downloaded: {FileId}, User: {UserId}",
            file.Id,
            userId
        );

        return File(stream, file.MimeType, file.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? projectName = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = _tenantContext.CurrentTenant?.TenantId;

        var query = _db.Files
            .Where(f => f.TenantId == tenantId && !f.IsDeleted);

        if (!string.IsNullOrEmpty(projectName))
            query = query.Where(f => f.ProjectName == projectName);

        var totalCount = await query.CountAsync();
        var files = await query
            .OrderByDescending(f => f.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                f.Id,
                f.FileName,
                f.SizeInBytes,
                f.MimeType,
                f.ProjectName,
                f.UploadedAt,
                f.OwnerEmail
            })
            .ToListAsync();

        return Ok(new
        {
            files,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var file = await _db.Files.FindAsync(id);
        if (file == null || file.IsDeleted) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        // Only owner or admin can delete
        if (file.OwnerId.ToString() != userId && userRole != "Admin")
            return Forbid("You don't have permission to delete this file");

        // Soft delete
        file.IsDeleted = true;
        file.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Background job: Actually delete from storage after 30 days
        // Or delete immediately if needed:
        // await _storage.DeleteAsync(file.StoragePath);

        _logger.LogInformation(
            "File deleted: {FileId}, User: {UserId}",
            file.Id,
            userId
        );

        return NoContent();
    }
}
```

#### **3. Configuration**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://tenant-api.azure.com"
  },

  "Jwt": {
    "Key": "<SAME_SECRET_KEY>",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp"
  },

  "FileStorage": {
    "Provider": "AzureBlob", // or "LocalFileSystem" for development
    "AzureBlob": {
      "ConnectionString": "<AZURE_STORAGE_CONNECTION_STRING>",
      "ContainerName": "files"
    },
    "LocalFileSystem": {
      "BasePath": "C:\\FileStorage"
    },
    "MaxFileSizeMB": 10,
    "AllowedMimeTypes": ["image/jpeg", "image/png", "application/pdf"]
  }
}
```

#### **4. How Projects Use File Manager**

**Project A (CRM) - Upload Invoice:**

```csharp
// Project A - OrderService.cs
public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
{
    // 1. Upload invoice to File Manager Service
    var fileUploadResponse = await _httpClient.PostAsync(
        "https://file-manager-api.azure.com/api/files",
        new MultipartFormDataContent
        {
            { new StreamContent(dto.InvoiceFile.OpenReadStream()), "file", dto.InvoiceFile.FileName },
            { new StringContent("ProjectA"), "projectName" },
            { new StringContent("TenantWide"), "accessLevel" }
        }
    );

    var fileData = await fileUploadResponse.Content.ReadFromJsonAsync<FileUploadResult>();

    // 2. Create order with file reference
    var order = new Order
    {
        CustomerId = dto.CustomerId,
        InvoiceFileId = fileData.FileId,  // ← Reference to file in File Manager
        Amount = dto.Amount
    };

    await _db.Orders.AddAsync(order);
    await _db.SaveChangesAsync();

    return Ok(order);
}

// Later: Download invoice
public async Task<IActionResult> DownloadInvoice(Guid orderId)
{
    var order = await _db.Orders.FindAsync(orderId);
    if (order?.InvoiceFileId == null) return NotFound();

    // Redirect to File Manager download endpoint
    return Redirect($"https://file-manager-api.azure.com/api/files/{order.InvoiceFileId}");
}
```

**Project B (Inventory) - Upload Product Image:**

```csharp
// Project B - ProductService.cs
public async Task<IActionResult> CreateProduct(CreateProductDto dto)
{
    // Upload image to SAME File Manager Service
    var fileUploadResponse = await _httpClient.PostAsync(
        "https://file-manager-api.azure.com/api/files",
        new MultipartFormDataContent
        {
            { new StreamContent(dto.ImageFile.OpenReadStream()), "file", dto.ImageFile.FileName },
            { new StringContent("ProjectB"), "projectName" },  // ← Different project
            { new StringContent("Public"), "accessLevel" }      // ← Public access for product images
        }
    );

    var fileData = await fileUploadResponse.Content.ReadFromJsonAsync<FileUploadResult>();

    var product = new Product
    {
        Name = dto.Name,
        ImageFileId = fileData.FileId,  // ← Reference to file
        Price = dto.Price
    };

    await _db.Products.AddAsync(product);
    await _db.SaveChangesAsync();

    return Ok(product);
}
```

---

## Summary

### **File Manager Service Deployment**

| Aspect                   | Recommendation                         |
| ------------------------ | -------------------------------------- |
| **Architecture**         | ONE shared File Manager Service        |
| **Storage Backend**      | Azure Blob Storage / AWS S3 (cloud)    |
| **Metadata Database**    | PostgreSQL (file info, permissions)    |
| **Multi-Tenancy**        | Yes - files scoped by tenant ID        |
| **Access Control**       | User ID + Tenant ID + File permissions |
| **Projects Integration** | HTTP API calls to File Manager Service |
| **Scaling**              | Horizontally scalable (stateless API)  |
| **Caching**              | CDN for downloads, not IMemoryCache    |
| **Cost**                 | Lower (1 service vs N services)        |

### **Deployment Checklist**

- [ ] Create File Manager Service (separate microservice)
- [ ] Set up Azure Blob Storage account
- [ ] Configure connection string in appsettings
- [ ] Create PostgreSQL database for file metadata
- [ ] Implement `IFileStorageProvider` interface
- [ ] Add JWT authentication (same as other services)
- [ ] Add multi-tenancy support (filter files by tenant)
- [ ] Implement file size/type validation
- [ ] Add virus scanning (production)
- [ ] Set up audit logging
- [ ] Configure CORS for file uploads
- [ ] Test upload/download from Project A
- [ ] Test upload/download from Project B
- [ ] Verify tenant isolation (Tenant 123 can't see Tenant 456 files)

### **Final Answer**

**YES, use ONE shared File Manager Service** across all your projects (A, B, C, etc.), just like Identity and Tenant services. This provides:

✅ Centralized file storage  
✅ Unified access control  
✅ Lower infrastructure costs  
✅ Easier file sharing across projects  
✅ Single backup/recovery strategy  
✅ Consistent security policies

**Your architecture should be:**

```
Shared Services (Deploy ONCE):
├─ Identity Service   (User authentication)
├─ Tenant Service     (Multi-tenancy config)
└─ File Manager Service   (File storage) ← NEW

Projects (Deploy per project):
├─ Project A (calls all 3 shared services)
├─ Project B (calls all 3 shared services)
└─ Project C (calls all 3 shared services)
```

---

**Last Updated:** October 19, 2025  
**Version:** 1.0.0
