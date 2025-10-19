# 🔑 Project Isolation Strategy Guide

## Critical Question: Should Users Be Shared Across Projects or Isolated?

**User's Scenario:** "Some users will use same email in different projects but same identity DB. How do I handle this?"

---

## 📋 Table of Contents

1. [The Core Problem](#the-core-problem)
2. [Two Architecture Patterns](#two-architecture-patterns)
3. [Pattern 1: Shared Users (Recommended)](#pattern-1-shared-users-recommended)
4. [Pattern 2: Project-Isolated Users](#pattern-2-project-isolated-users)
5. [Hybrid Approach](#hybrid-approach)
6. [Implementation Examples](#implementation-examples)
7. [Decision Matrix](#decision-matrix)
8. [Migration Strategy](#migration-strategy)

---

## The Core Problem

### **Scenario Description**

You have:

- **ONE Identity Service** (shared database with users)
- **Multiple Projects** (Project A, B, C)
- **Potential Issue:** User `john@example.com` might exist in:
  - Project A (as a customer)
  - Project B (as a supplier)
  - Different roles, different data, different contexts

**Question:** Should John have ONE user account across all projects, or SEPARATE accounts per project?

---

## Two Architecture Patterns

### **Pattern 1: Shared Users (Single Identity) ✅ RECOMMENDED**

```
┌─────────────────────────────────────────────┐
│         IDENTITY SERVICE                     │
│                                               │
│  Users Table:                                │
│  ┌─────────────────────────────────────┐    │
│  │ UserId: 1                           │    │
│  │ Email: john@example.com             │    │
│  │ Password: hashed...                 │    │
│  │ GlobalRole: User                    │    │
│  └─────────────────────────────────────┘    │
│                                               │
│  UserProjects Table: (Mapping)               │
│  ┌─────────────────────────────────────┐    │
│  │ UserId: 1, Project: A, Role: Admin  │    │
│  │ UserId: 1, Project: B, Role: Viewer │    │
│  │ UserId: 1, Project: C, Role: Editor │    │
│  └─────────────────────────────────────┘    │
└─────────────────────────────────────────────┘
```

**Key Points:**

- ✅ ONE email = ONE user account
- ✅ User can access multiple projects
- ✅ Different roles per project
- ✅ Single sign-on across all projects

### **Pattern 2: Project-Isolated Users (NOT Recommended)**

```
┌─────────────────────────────────────────────┐
│         IDENTITY SERVICE                     │
│                                               │
│  Users Table:                                │
│  ┌─────────────────────────────────────┐    │
│  │ UserId: 1                           │    │
│  │ Email: john@example.com             │    │
│  │ ProjectId: A                        │ ←──┐
│  │ Password: hashed...                 │    │ Different
│  └─────────────────────────────────────┘    │ users!
│  ┌─────────────────────────────────────┐    │
│  │ UserId: 2                           │    │
│  │ Email: john@example.com             │ ←──┘ Same email
│  │ ProjectId: B                        │      but different
│  │ Password: hashed...                 │      account
│  └─────────────────────────────────────┘    │
└─────────────────────────────────────────────┘
```

**Problems:**

- ❌ Same email registered multiple times
- ❌ User must remember which password for which project
- ❌ No single sign-on
- ❌ User updates email in Project A, but Project B still has old email

---

## Pattern 1: Shared Users (Recommended)

### **Architecture: One User, Multiple Project Memberships**

#### **Database Schema**

```sql
-- Identity Service Database

-- Main Users table (ONE record per email)
CREATE TABLE Users (
    Id UUID PRIMARY KEY,
    Email VARCHAR(255) UNIQUE NOT NULL,  -- ← UNIQUE constraint
    PasswordHash VARCHAR(500) NOT NULL,
    FirstName VARCHAR(100),
    LastName VARCHAR(100),
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT NOW()
);

-- Project Memberships (Many-to-Many relationship)
CREATE TABLE UserProjects (
    Id UUID PRIMARY KEY,
    UserId UUID REFERENCES Users(Id) ON DELETE CASCADE,
    ProjectId VARCHAR(50) NOT NULL,      -- "ProjectA", "ProjectB", "ProjectC"
    Role VARCHAR(50) NOT NULL,           -- "Admin", "Editor", "Viewer"
    IsActive BOOLEAN DEFAULT TRUE,
    JoinedAt TIMESTAMP DEFAULT NOW(),

    UNIQUE(UserId, ProjectId)            -- ← User can join each project only once
);

-- Optional: Project-specific permissions
CREATE TABLE UserProjectPermissions (
    Id UUID PRIMARY KEY,
    UserProjectId UUID REFERENCES UserProjects(Id) ON DELETE CASCADE,
    Permission VARCHAR(100) NOT NULL,    -- "canUploadFiles", "canDeleteOrders", etc.

    UNIQUE(UserProjectId, Permission)
);
```

#### **Example Data**

```sql
-- Users table
INSERT INTO Users (Id, Email, PasswordHash, FirstName, LastName)
VALUES
    ('user-123', 'john@example.com', 'hash...', 'John', 'Doe'),
    ('user-456', 'jane@example.com', 'hash...', 'Jane', 'Smith');

-- UserProjects table (John is in multiple projects with different roles)
INSERT INTO UserProjects (Id, UserId, ProjectId, Role)
VALUES
    ('up-1', 'user-123', 'ProjectA', 'Admin'),      -- John is Admin in Project A
    ('up-2', 'user-123', 'ProjectB', 'Viewer'),     -- John is Viewer in Project B
    ('up-3', 'user-123', 'ProjectC', 'Editor'),     -- John is Editor in Project C
    ('up-4', 'user-456', 'ProjectA', 'Editor');     -- Jane is Editor in Project A
```

#### **JWT Token Structure**

When John logs in, the JWT token includes project memberships:

```json
{
  "sub": "user-123",
  "email": "john@example.com",
  "name": "John Doe",
  "projects": [
    { "projectId": "ProjectA", "role": "Admin" },
    { "projectId": "ProjectB", "role": "Viewer" },
    { "projectId": "ProjectC", "role": "Editor" }
  ],
  "iss": "IdentityService",
  "aud": "MicroservicesApp",
  "exp": 1729360800
}
```

**Benefits:**

- ✅ John logs in ONCE with `john@example.com`
- ✅ Can access Project A, B, C with same token
- ✅ Each project sees John's role for that project
- ✅ Update email once, applies to all projects

#### **How Projects Use This**

**Project A (CRM) - Check if user has access:**

```csharp
[Authorize]
[HttpGet("orders")]
public async Task<IActionResult> GetOrders()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var projectsClaim = User.FindFirst("projects")?.Value;

    // Parse projects from JWT
    var projects = JsonSerializer.Deserialize<List<UserProject>>(projectsClaim);
    var projectA = projects?.FirstOrDefault(p => p.ProjectId == "ProjectA");

    if (projectA == null)
        return Forbid("You don't have access to Project A");

    // Check role
    if (projectA.Role == "Admin" || projectA.Role == "Editor")
    {
        // Can view and edit orders
        var orders = await _db.Orders.Where(o => o.TenantId == _tenantContext.TenantId).ToListAsync();
        return Ok(orders);
    }
    else if (projectA.Role == "Viewer")
    {
        // Can only view orders
        var orders = await _db.Orders.Where(o => o.TenantId == _tenantContext.TenantId).ToListAsync();
        return Ok(orders);
    }

    return Forbid("Insufficient permissions");
}
```

**Project B (Inventory) - Check user's role:**

```csharp
[Authorize]
[HttpPost("products")]
public async Task<IActionResult> CreateProduct(CreateProductDto dto)
{
    var projectsClaim = User.FindFirst("projects")?.Value;
    var projects = JsonSerializer.Deserialize<List<UserProject>>(projectsClaim);
    var projectB = projects?.FirstOrDefault(p => p.ProjectId == "ProjectB");

    if (projectB == null)
        return Forbid("You don't have access to Project B");

    // Only Admin and Editor can create products
    if (projectB.Role != "Admin" && projectB.Role != "Editor")
        return Forbid("Only Admin/Editor can create products");

    // Create product...
    return Ok(product);
}
```

---

## Pattern 2: Project-Isolated Users

### **Architecture: Separate User Accounts Per Project**

**⚠️ WARNING: NOT RECOMMENDED - Only use if you have strict regulatory requirements**

#### **Database Schema**

```sql
-- Identity Service Database

-- Users table (Multiple records for same email across projects)
CREATE TABLE Users (
    Id UUID PRIMARY KEY,
    Email VARCHAR(255) NOT NULL,             -- ← NO UNIQUE constraint
    ProjectId VARCHAR(50) NOT NULL,          -- ← Added ProjectId
    PasswordHash VARCHAR(500) NOT NULL,
    FirstName VARCHAR(100),
    LastName VARCHAR(100),
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT NOW(),

    UNIQUE(Email, ProjectId)                 -- ← Unique per project only
);
```

#### **Example Data**

```sql
-- John has SEPARATE accounts for each project
INSERT INTO Users (Id, Email, ProjectId, PasswordHash, FirstName, LastName)
VALUES
    ('user-123-a', 'john@example.com', 'ProjectA', 'hash1...', 'John', 'Doe'),
    ('user-123-b', 'john@example.com', 'ProjectB', 'hash2...', 'John', 'Doe'),  -- Different password!
    ('user-123-c', 'john@example.com', 'ProjectC', 'hash3...', 'John', 'Doe');
```

#### **Login Flow (Requires Project Selection)**

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    // ⚠️ User must specify which project they're logging into
    var user = await _db.Users
        .FirstOrDefaultAsync(u =>
            u.Email == request.Email &&
            u.ProjectId == request.ProjectId);  // ← Must provide ProjectId

    if (user == null)
        return Unauthorized("Invalid email/project combination");

    if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        return Unauthorized("Invalid password");

    // Generate JWT token with ProjectId embedded
    var token = GenerateJwtToken(user);
    return Ok(new { accessToken = token });
}

// Login request must include projectId
public class LoginRequest
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string ProjectId { get; set; }  // ← Required!
}
```

**User Experience Problems:**

```
1. John visits Project A website
   → Login form asks: Email, Password, Project (dropdown: A, B, C)
   → John selects "Project A"
   → Enters password (must remember which password for Project A)

2. John visits Project B website
   → Must login AGAIN
   → Select "Project B"
   → Enter DIFFERENT password (confusion!)

3. John updates email in Project A to john.doe@company.com
   → Project B still shows john@example.com
   → John can't receive notifications from Project B
```

**Why This Is Bad:**

- ❌ User must remember multiple passwords
- ❌ No single sign-on
- ❌ Data inconsistency (email, name changes)
- ❌ Poor user experience

---

## Hybrid Approach

### **Scenario: Some Projects Should Be Isolated**

**Example:** You have:

- **Public Projects** (A, B, C) - Users can freely join
- **Internal Project** (Admin Portal) - Separate authentication

**Solution: Two Identity Services**

```
┌─────────────────────────────────────┐
│  IDENTITY SERVICE (Public)          │
│  Users for Projects A, B, C         │
└─────────────────────────────────────┘
         │
         ├─────▶ Project A (Public)
         ├─────▶ Project B (Public)
         └─────▶ Project C (Public)

┌─────────────────────────────────────┐
│  IDENTITY SERVICE (Internal)        │
│  Users for Admin Portal             │
└─────────────────────────────────────┘
         │
         └─────▶ Admin Portal (Internal)
```

**But even then, use Pattern 1 within each Identity Service!**

---

## Implementation Examples

### **File Manager Service with Project Memberships**

**Updated FileMetadata Entity:**

```csharp
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

    // ✅ NEW: Project tracking
    public string ProjectId { get; set; } = string.Empty;  // "ProjectA", "ProjectB", "ProjectC"

    // Access control
    public FileAccessLevel AccessLevel { get; set; }

    // Metadata
    public DateTime UploadedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted { get; set; }
}
```

**Updated Upload Endpoint:**

```csharp
[HttpPost]
[Authorize]
public async Task<IActionResult> Upload(
    IFormFile file,
    [FromForm] string projectId,  // ✅ Required: Which project is this file for?
    [FromForm] FileAccessLevel accessLevel = FileAccessLevel.Private)
{
    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown";
    var tenantId = _tenantContext.CurrentTenant?.TenantId ?? throw new Exception("No tenant");

    // ✅ Verify user has access to this project
    var projectsClaim = User.FindFirst("projects")?.Value;
    var projects = JsonSerializer.Deserialize<List<UserProject>>(projectsClaim);
    var userProject = projects?.FirstOrDefault(p => p.ProjectId == projectId);

    if (userProject == null)
        return Forbid($"You don't have access to {projectId}");

    // Upload to storage with project-specific path
    var storagePath = await _storage.UploadAsync(
        file.OpenReadStream(),
        file.FileName,
        tenantId,
        projectId,  // ✅ Project-specific folder
        userId
    );

    // Save metadata with ProjectId
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
        ProjectId = projectId,  // ✅ Store which project owns this file
        AccessLevel = accessLevel,
        UploadedAt = DateTime.UtcNow
    };

    await _db.Files.AddAsync(fileMetadata);
    await _db.SaveChangesAsync();

    return Ok(new { fileId = fileMetadata.Id, projectId = projectId });
}
```

**Updated List Files Endpoint (Filter by Project):**

```csharp
[HttpGet]
[Authorize]
public async Task<IActionResult> ListFiles(
    [FromQuery] string? projectId = null,  // ✅ Optional: Filter by project
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var tenantId = _tenantContext.CurrentTenant?.TenantId;

    // Get user's projects from JWT
    var projectsClaim = User.FindFirst("projects")?.Value;
    var userProjects = JsonSerializer.Deserialize<List<UserProject>>(projectsClaim);
    var accessibleProjectIds = userProjects?.Select(p => p.ProjectId).ToList() ?? new List<string>();

    var query = _db.Files
        .Where(f => f.TenantId == tenantId && !f.IsDeleted)
        .Where(f => accessibleProjectIds.Contains(f.ProjectId));  // ✅ Only show files from user's projects

    // Optional: Filter by specific project
    if (!string.IsNullOrEmpty(projectId))
    {
        if (!accessibleProjectIds.Contains(projectId))
            return Forbid($"You don't have access to {projectId}");

        query = query.Where(f => f.ProjectId == projectId);
    }

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
            f.ProjectId,  // ✅ Include project info
            f.UploadedAt,
            f.OwnerEmail
        })
        .ToListAsync();

    return Ok(new { files, totalCount, page, pageSize });
}
```

**Storage Path Structure (with ProjectId):**

```
Azure Blob Storage: "files"
├─ tenant_123/
│  ├─ ProjectA/
│  │  ├─ user_456/
│  │  │  ├─ invoice_001.pdf
│  │  │  └─ contract.pdf
│  │  └─ user_789/
│  │     └─ report.xlsx
│  ├─ ProjectB/
│  │  ├─ user_456/         // ← Same user (456) but different project
│  │  │  └─ product.png
│  │  └─ user_789/
│  │     └─ manual.pdf
│  └─ ProjectC/
│     └─ user_456/
│        └─ design.fig
└─ tenant_456/
   └─ ... (same structure)
```

---

## Decision Matrix

### **When to Use Pattern 1 (Shared Users) ✅**

| Scenario                                                   | Use Shared Users? |
| ---------------------------------------------------------- | ----------------- |
| Projects are parts of the same organization                | ✅ YES            |
| Users need access to multiple projects                     | ✅ YES            |
| Want single sign-on experience                             | ✅ YES            |
| Projects share same tenant/company context                 | ✅ YES            |
| Want to reduce user management overhead                    | ✅ YES            |
| Normal SaaS application                                    | ✅ YES            |
| **Examples:** CRM + Inventory + Reporting for same company | ✅ YES            |

### **When to Use Pattern 2 (Isolated Users) ⚠️**

| Scenario                                        | Use Isolated Users?                               |
| ----------------------------------------------- | ------------------------------------------------- |
| Projects are completely separate businesses     | ⚠️ Maybe (use separate Identity Services instead) |
| Regulatory compliance requires isolation        | ⚠️ Maybe                                          |
| Projects owned by different companies           | ⚠️ Use separate Identity Services                 |
| User data cannot be shared due to legal reasons | ⚠️ Use separate Identity Services                 |
| **Examples:** Acquired company's legacy system  | ⚠️ Migrate to shared users eventually             |

**Recommendation: Use Pattern 1 for 99% of cases.**

---

## Migration Strategy

### **If You Already Have Isolated Users (Pattern 2)**

**Step 1: Identify Duplicate Emails**

```sql
-- Find emails that exist across multiple projects
SELECT Email, COUNT(DISTINCT ProjectId) as ProjectCount
FROM Users
GROUP BY Email
HAVING COUNT(DISTINCT ProjectId) > 1;

-- Result:
-- john@example.com    3 projects
-- jane@example.com    2 projects
```

**Step 2: Merge Accounts**

```sql
-- Create new merged users table
CREATE TABLE Users_New (
    Id UUID PRIMARY KEY,
    Email VARCHAR(255) UNIQUE NOT NULL,
    PasswordHash VARCHAR(500) NOT NULL,
    FirstName VARCHAR(100),
    LastName VARCHAR(100),
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT NOW()
);

-- Create project memberships table
CREATE TABLE UserProjects (
    Id UUID PRIMARY KEY,
    UserId UUID REFERENCES Users_New(Id),
    ProjectId VARCHAR(50) NOT NULL,
    Role VARCHAR(50) NOT NULL,
    JoinedAt TIMESTAMP DEFAULT NOW(),
    UNIQUE(UserId, ProjectId)
);

-- Migration script
DO $$
DECLARE
    distinct_email VARCHAR(255);
    new_user_id UUID;
    old_user RECORD;
BEGIN
    -- For each unique email
    FOR distinct_email IN
        SELECT DISTINCT Email FROM Users
    LOOP
        -- Create one merged user
        new_user_id := gen_random_uuid();

        INSERT INTO Users_New (Id, Email, PasswordHash, FirstName, LastName, CreatedAt)
        SELECT
            new_user_id,
            Email,
            PasswordHash,  -- Use password from first occurrence
            FirstName,
            LastName,
            MIN(CreatedAt)
        FROM Users
        WHERE Email = distinct_email
        GROUP BY Email, PasswordHash, FirstName, LastName
        LIMIT 1;

        -- Create project memberships for each old account
        FOR old_user IN
            SELECT * FROM Users WHERE Email = distinct_email
        LOOP
            INSERT INTO UserProjects (Id, UserId, ProjectId, Role, JoinedAt)
            VALUES (
                gen_random_uuid(),
                new_user_id,
                old_user.ProjectId,
                'Editor',  -- Default role, adjust as needed
                old_user.CreatedAt
            );
        END LOOP;
    END LOOP;
END $$;
```

**Step 3: Update Application Code**

```csharp
// Old code (Pattern 2)
var user = await _db.Users
    .FirstOrDefaultAsync(u => u.Email == email && u.ProjectId == projectId);

// New code (Pattern 1)
var user = await _db.Users
    .Include(u => u.Projects)
    .FirstOrDefaultAsync(u => u.Email == email);

var hasProjectAccess = user.Projects.Any(p => p.ProjectId == projectId);
```

**Step 4: Notify Users**

```
Subject: Account Consolidation - One Login for All Projects

Dear John,

We've improved your experience! You can now use a single login
(john@example.com) to access all projects:
- Project A (Admin role)
- Project B (Viewer role)
- Project C (Editor role)

Your password is now unified. If you had different passwords before,
please use the "Forgot Password" link to reset.

Thank you,
The Team
```

---

## Summary & Recommendations

### **✅ RECOMMENDED: Pattern 1 (Shared Users)**

```
One User Account per Email
├─ Single login across all projects
├─ Different roles per project (via UserProjects table)
├─ ProjectId stored in data tables (Files, Orders, Products, etc.)
└─ JWT includes list of projects user has access to
```

**Implementation Checklist:**

- [ ] Users table: Email is UNIQUE (one user per email)
- [ ] UserProjects table: Maps users to projects with roles
- [ ] JWT token includes "projects" claim with user's project memberships
- [ ] Each service checks user's project access before operations
- [ ] Data tables include ProjectId column (Files, Orders, etc.)
- [ ] Storage paths include project folder (tenant_123/ProjectA/...)

**Benefits:**

- ✅ Single sign-on
- ✅ Better user experience
- ✅ Easier user management
- ✅ Data consistency
- ✅ Industry standard

### **❌ NOT RECOMMENDED: Pattern 2 (Isolated Users)**

Only consider this if you have:

- Strict regulatory requirements (HIPAA, financial regulations)
- Complete business isolation needed
- Different companies/organizations

**Even then, consider using separate Identity Services instead of projectId in same database.**

---

**Last Updated:** October 19, 2025  
**Version:** 1.0.0
