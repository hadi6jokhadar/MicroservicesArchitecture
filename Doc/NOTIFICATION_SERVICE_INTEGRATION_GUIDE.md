# 🔔 Notification Service Integration Guide

## Complete Step-by-Step Implementation

**Last Updated:** November 4, 2025  
**Version:** 1.0.0  
**Implementation:** 2 Phases

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Architecture Refinements](#architecture-refinements)
3. [Prerequisites](#prerequisites)
4. [Phase 1: Core Service Setup](#phase-1-core-service-setup)
5. [Phase 2: SignalR & Background Processing](#phase-2-signalr--background-processing)
6. [Key Improvements Applied](#key-improvements-applied)

---

## Overview

### What We're Building

A robust, multi-tenant notification system with:

- ✅ **Queue-first architecture** (enqueue → process → deliver)
- ✅ **Two-database model** (Global DB + Tenant DBs)
- ✅ **Dual delivery channels** (SignalR + Firebase)
- ✅ **Two priority levels** (Immediate + Waitable)
- ✅ **JWT authentication** (tenant-aware or standalone)
- ✅ **Device token management** in Identity Service
- ✅ **SignalR hub** with tenant-based grouping

### Service Communication Pattern

```
┌──────────────────────────────────────────────────────────────────┐
│                     ANY SERVICE (Orders, etc.)                    │
└───────────────────────────┬──────────────────────────────────────┘
                            │
                            │ POST /api/notifications/send
                            │ (with TenantId in header/body)
                            ▼
┌────────────────────────────────────────────────────────────────────┐
│                    NOTIFICATION SERVICE                             │
│                                                                      │
│  Global DB (NotificationQueue):                                    │
│  ├─ Queue notification first (reliability)                         │
│  └─ Track delivery status & retries                                │
│                                                                      │
│  Tenant DBs (Notifications):                                       │
│  ├─ Store notification history per tenant                          │
│  └─ User read/unread status                                        │
│                                                                      │
│  Delivery Channels:                                                │
│  ├─ SignalR: Real-time push (with acknowledgment)                 │
│  └─ Firebase: Mobile push (fire-and-forget)                        │
└────────────────────────────────────────────────────────────────────┘
```

---

## Architecture Refinements

### Key Improvements Applied

Based on your requirements, we've refined the design:

#### **1. TenantId from Header** ✅

- SignalR hub reads `x-tenant-id` from connection header
- Creates tenant-specific groups automatically
- No need for separate `TenantKey` field

#### **2. Simplified Queue Model** ✅

```csharp
public class NotificationQueueItem
{
    public Guid Id { get; set; }
    public string? TenantId { get; set; }  // ✅ Only TenantId (no TenantKey)
    public int? UserId { get; set; }
    // ... rest of properties
}
```

#### **3. SignalR Received API** ✅

```csharp
// New endpoint for client acknowledgment
POST /api/notifications/received
{
    "queueItemId": "guid",
    "connectionId": "string"
}
```

#### **4. Device Token Management in Identity Service** ✅

- `UserDeviceToken` table moved to Identity Service
- Notification Service queries Identity Service for tokens
- Better separation of concerns

#### **5. Access User Table via Tenant DB** ✅

- When TenantId provided → resolve tenant connection string
- Access tenant's User table directly
- No need for separate user lookup service

#### **6. JWT Security Model** ✅

```csharp
// Multi-tenant mode: Use tenant-specific JWT
if (tenantId != null)
    → Validate with tenant's JWT secret

// Standalone mode: Use appsettings.json JWT
else
    → Validate with shared JWT secret
```

---

## Prerequisites

### Required Knowledge

- ✅ .NET 9.0 development
- ✅ Clean Architecture principles
- ✅ MediatR pattern (CQRS)
- ✅ SignalR real-time communication
- ✅ Multi-tenancy concepts

### Required Services

- ✅ **Identity Service** (running on port 5001)
- ✅ **Tenant Service** (running on port 5002)
- ✅ **PostgreSQL Database** (local or cloud)

### Development Tools

- ✅ Visual Studio 2022 or VS Code
- ✅ .NET 9.0 SDK
- ✅ Postman or similar API testing tool
- ✅ SignalR client (for testing)

---

## Phase 1: Core Service Setup

### Step 1.1: Create Project Structure

**Action:** Create the Clean Architecture project structure.

```bash
# Navigate to Services directory
cd src/Services

# Create Notification service folder
mkdir Notification
cd Notification

# Create projects
dotnet new webapi -n Notification.API -o Notification.API
dotnet new classlib -n Notification.Application -o Notification.Application
dotnet new classlib -n Notification.Domain -o Notification.Domain
dotnet new classlib -n Notification.Infrastructure -o Notification.Infrastructure

# Add projects to solution (from repository root)
cd ../../..
dotnet sln add src/Services/Notification/Notification.API/Notification.API.csproj
dotnet sln add src/Services/Notification/Notification.Application/Notification.Application.csproj
dotnet sln add src/Services/Notification/Notification.Domain/Notification.Domain.csproj
dotnet sln add src/Services/Notification/Notification.Infrastructure/Notification.Infrastructure.csproj
```

**Expected Result:**

```
src/Services/Notification/
├── Notification.API/
├── Notification.Application/
├── Notification.Domain/
└── Notification.Infrastructure/
```

---

### Step 1.2: Add Project References

**Action:** Set up dependencies between projects.

```bash
cd src/Services/Notification

# API references
cd Notification.API
dotnet add reference ../Notification.Application/Notification.Application.csproj
dotnet add reference ../Notification.Infrastructure/Notification.Infrastructure.csproj
dotnet add reference ../../../Shared/IhsanDev.Shared.Application/IhsanDev.Shared.Application.csproj
dotnet add reference ../../../Shared/IhsanDev.Shared.Infrastructure/IhsanDev.Shared.Infrastructure.csproj

# Application references
cd ../Notification.Application
dotnet add reference ../Notification.Domain/Notification.Domain.csproj
dotnet add reference ../../../Shared/IhsanDev.Shared.Application/IhsanDev.Shared.Application.csproj
dotnet add reference ../../../Shared/IhsanDev.Shared.Kernel/IhsanDev.Shared.Kernel.csproj

# Infrastructure references
cd ../Notification.Infrastructure
dotnet add reference ../Notification.Domain/Notification.Domain.csproj
dotnet add reference ../Notification.Application/Notification.Application.csproj
dotnet add reference ../../../Shared/IhsanDev.Shared.Infrastructure/IhsanDev.Shared.Infrastructure.csproj

# Domain references (only Shared.Kernel)
cd ../Notification.Domain
dotnet add reference ../../../Shared/IhsanDev.Shared.Kernel/IhsanDev.Shared.Kernel.csproj
```

---

### Step 1.3: Add Required NuGet Packages

**Action:** Add necessary packages to each project.

**Notification.API:**

```bash
cd Notification.API
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Swashbuckle.AspNetCore
dotnet add package FluentValidation.AspNetCore
```

**Notification.Application:**

```bash
cd ../Notification.Application
dotnet add package MediatR
dotnet add package FluentValidation
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection
```

**Notification.Infrastructure:**

```bash
cd ../Notification.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package FirebaseAdmin
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

---

### Step 1.4: Create Domain Entities

**Action:** Create domain models following the refined architecture.

**File:** `Notification.Domain/Entities/NotificationQueueItem.cs`

```csharp
using IhsanDev.Shared.Kernel.Entities;

namespace Notification.Domain.Entities;

/// <summary>
/// Global queue item for managing notification delivery workflow
/// Stored in Global DB (NotificationQueue database)
/// </summary>
public class NotificationQueueItem : BaseEntity
{
    // Tenant routing (only TenantId needed)
    public string? TenantId { get; set; }

    // User targeting (null = broadcast to all users in tenant)
    public int? UserId { get; set; }

    // Delivery configuration
    public DeliveryType DeliveryType { get; set; }  // SignalR | Firebase | Both
    public Priority Priority { get; set; }           // Immediate | Waitable

    // Notification payload (snapshot for processing)
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Data { get; set; }  // JSON payload

    // Status tracking
    public QueueStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Error { get; set; }

    // Reference to persisted notification in tenant DB
    public Guid? NotificationId { get; set; }
}

public enum DeliveryType
{
    SignalR = 1,
    Firebase = 2,
    Both = 3
}

public enum Priority
{
    Waitable = 0,    // Background processing
    Immediate = 1     // Process immediately
}

public enum QueueStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Expired = 4
}
```

**File:** `Notification.Domain/Entities/Notification.cs`

```csharp
using IhsanDev.Shared.Kernel.Entities;

namespace Notification.Domain.Entities;

/// <summary>
/// Notification record stored per tenant
/// Stored in Tenant DBs (each tenant's database)
/// </summary>
public class Notification : BaseEntity
{
    // null = notification for all users in tenant
    public int? UserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Data { get; set; }  // JSON payload

    // User interaction
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Reference back to queue item (for tracking)
    public Guid? QueueItemId { get; set; }
}
```

---

### Step 1.5: Create Database Contexts

**Action:** Create two DbContext classes (Global + Tenant).

**File:** `Notification.Infrastructure/Persistence/NotificationDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;

namespace Notification.Infrastructure.Persistence;

/// <summary>
/// Global database context for notification queue management
/// This database is NOT tenant-specific - it's shared across all tenants
/// </summary>
public class NotificationDbContext : BaseDbContext
{
    public NotificationDbContext(
        DbContextOptions<NotificationDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options, currentUserService)
    {
    }

    public DbSet<NotificationQueueItem> NotificationQueue { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure NotificationQueueItem
        modelBuilder.Entity<NotificationQueueItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TenantId)
                .HasMaxLength(100);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Message)
                .HasMaxLength(2000);

            entity.Property(e => e.Data)
                .HasColumnType("jsonb");  // PostgreSQL JSON type

            entity.Property(e => e.Error)
                .HasMaxLength(1000);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.Status, e.ExpiresAt });
        });
    }
}
```

**File:** `Notification.Infrastructure/Persistence/TenantNotificationDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Notification.Infrastructure.Persistence;

/// <summary>
/// Tenant-specific database context for notification history
/// Each tenant has their own notifications table in their own database
/// </summary>
public class TenantNotificationDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<TenantNotificationDbContext>? _logger;

    public TenantNotificationDbContext(
        DbContextOptions<TenantNotificationDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<TenantNotificationDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<Domain.Entities.Notification> Notifications { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // If already configured (from DI), skip
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        string? connectionString = null;
        string? provider = null;

        // Check if multi-tenancy is enabled and tenant has custom database settings
        if (_tenantContext?.HasTenant == true &&
            _tenantContext.CurrentTenant?.Configuration?.Database != null)
        {
            var tenantDb = _tenantContext.CurrentTenant.Configuration.Database;

            if (!string.IsNullOrWhiteSpace(tenantDb.ConnectionString))
            {
                connectionString = tenantDb.ConnectionString;
                provider = tenantDb.Provider ?? "PostgreSql";

                _logger?.LogInformation(
                    "Using tenant-specific database connection for tenant: {TenantId}",
                    _tenantContext.CurrentTenant.TenantId);
            }
        }

        // Fallback to appsettings.json if no tenant-specific database
        if (string.IsNullOrWhiteSpace(connectionString) && _configuration != null)
        {
            connectionString = _configuration["DatabaseSettings:ConnectionString"];
            provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

            _logger?.LogDebug("Using default database connection from appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured");
        }

        // Configure database provider
        switch (provider)
        {
            case "PostgreSql":
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(TenantNotificationDbContext).Assembly.GetName().Name);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
                break;

            case "Sqlite":
                optionsBuilder.UseSqlite(connectionString);
                break;

            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported");
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Notification
        modelBuilder.Entity<Domain.Entities.Notification>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Message)
                .HasMaxLength(2000);

            entity.Property(e => e.Data)
                .HasColumnType("jsonb");  // PostgreSQL JSON type

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsRead);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
        });
    }
}
```

---

### Step 1.6: Create Application DTOs

**Action:** Create request/response DTOs for the API.

**File:** `Notification.Application/DTOs/SendNotificationRequest.cs`

```csharp
namespace Notification.Application.DTOs;

public class SendNotificationRequest
{
    public string? TenantId { get; set; }
    public int? UserId { get; set; }  // null = broadcast to all users

    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Data { get; set; }  // JSON payload

    public string DeliveryType { get; set; } = "Both";  // SignalR, Firebase, Both
    public string Priority { get; set; } = "Immediate";  // Immediate, Waitable
}
```

**File:** `Notification.Application/DTOs/SendNotificationResponse.cs`

```csharp
namespace Notification.Application.DTOs;

public class SendNotificationResponse
{
    public Guid QueueItemId { get; set; }
    public string Status { get; set; } = "Queued";
    public DateTime QueuedAt { get; set; }
}
```

**File:** `Notification.Application/DTOs/NotificationReceivedRequest.cs`

```csharp
namespace Notification.Application.DTOs;

/// <summary>
/// Request DTO for SignalR client acknowledgment
/// </summary>
public class NotificationReceivedRequest
{
    public Guid QueueItemId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
}
```

**File:** `Notification.Application/DTOs/NotificationResponse.cs`

```csharp
namespace Notification.Application.DTOs;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Data { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
```

---

### Step 1.7: Create MediatR Commands

**Action:** Create commands for sending notifications.

**File:** `Notification.Application/Commands/SendNotificationCommand.cs`

```csharp
using MediatR;
using Notification.Application.DTOs;

namespace Notification.Application.Commands;

public record SendNotificationCommand : IRequest<SendNotificationResponse>
{
    public string? TenantId { get; init; }
    public int? UserId { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? Message { get; init; }
    public string? Data { get; init; }

    public string DeliveryType { get; init; } = "Both";
    public string Priority { get; init; } = "Immediate";
}
```

**File:** `Notification.Application/Commands/MarkNotificationAsReadCommand.cs`

```csharp
using MediatR;

namespace Notification.Application.Commands;

public record MarkNotificationAsReadCommand : IRequest<bool>
{
    public Guid NotificationId { get; init; }
    public int UserId { get; init; }
}
```

---

### Step 1.8: Create Configuration Files

**Action:** Configure the Notification Service.

**File:** `Notification.API/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.SignalR": "Information",
      "Microsoft.AspNetCore.Http.Connections": "Information"
    }
  },
  "AllowedHosts": "*",

  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "Shared",
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  },

  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=notifications_global;Username=postgres;Password=postgres"
  },

  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters-must-match-identity-service",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 60
  },

  "SignalR": {
    "EnableDetailedErrors": true,
    "ClientTimeoutInterval": "00:01:00",
    "KeepAliveInterval": "00:00:15"
  },

  "Firebase": {
    "Enabled": false,
    "ProjectId": "",
    "ServiceAccountKeyPath": ""
  },

  "NotificationProcessing": {
    "WaitableBatchSize": 100,
    "WaitableBatchIntervalSeconds": 30,
    "MaxRetryCount": 3,
    "RetryDelaySeconds": 10,
    "ExpiryDays": 1,
    "CleanupIntervalHours": 1
  },

  "IdentityService": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

**File:** `Notification.API/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.AspNetCore.SignalR": "Debug",
      "Microsoft.AspNetCore.Http.Connections": "Debug"
    }
  },

  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=notifications_global;Username=postgres;Password=postgres"
  },

  "SignalR": {
    "EnableDetailedErrors": true,
    "ClientTimeoutInterval": "00:02:00",
    "KeepAliveInterval": "00:00:30"
  },

  "NotificationProcessing": {
    "WaitableBatchIntervalSeconds": 10,
    "MaxRetryCount": 2
  }
}
```

---

## ✅ Phase 1 Completion Checklist

Before proceeding to Phase 2, verify:

- [ ] All projects created and added to solution
- [ ] All project references configured correctly
- [ ] All NuGet packages installed
- [ ] Domain entities created (NotificationQueueItem, Notification)
- [ ] Database contexts created (Global + Tenant)
- [ ] DTOs created (Request/Response models)
- [ ] MediatR commands created
- [ ] Configuration files set up
- [ ] Projects build successfully (`dotnet build`)

**Verify Build:**

```bash
cd src/Services/Notification
dotnet build
```

Expected output: `Build succeeded. 0 Error(s)`

---

## 🎯 What's Next?

**Phase 2 will cover:**

1. ✅ Command handlers implementation
2. ✅ NotificationProcessor service (tenant resolution, persistence, delivery)
3. ✅ SignalR Hub with tenant grouping
4. ✅ Firebase integration
5. ✅ Background services (Waitable processor, Cleanup service)
6. ✅ API endpoints (Minimal APIs)
7. ✅ Program.cs setup (complete middleware pipeline)
8. ✅ Database migrations
9. ✅ Testing instructions

---

## 📞 Support

**Questions about Phase 1?**

- Review the Identity Service implementation for reference
- Check the NEW_SERVICE_INTEGRATION_GUIDE.md
- Ensure all dependencies are correctly installed

**Ready for Phase 2?**
Let me know and I'll provide the complete implementation for handlers, services, SignalR hub, and background processing!

---

**Last Updated:** November 4, 2025  
**Next Phase:** Phase 2 - Service Implementation & SignalR Integration
