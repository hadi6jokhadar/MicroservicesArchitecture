# 🏗️ New Service Design Pattern - Stage 1: Architecture & Structure

**Version:** 1.0  
**Last Updated:** January 2025  
**Stage:** 1 of 3 - Service Architecture & Structure  
**Next Stage:** [Stage 2: Configuration & Integration](NEW_SERVICE_DESIGN_PATTERN_STAGE_2.md)

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Service Architecture Decision Tree](#service-architecture-decision-tree)
3. [Clean Architecture Layers](#clean-architecture-layers)
4. [Project Structure Template](#project-structure-template)
5. [Domain Layer Design](#domain-layer-design)
6. [Application Layer Design](#application-layer-design)
7. [Infrastructure Layer Design](#infrastructure-layer-design)
8. [API Layer Design](#api-layer-design)
9. [Testing Layer Design](#testing-layer-design)
10. [File Naming Conventions](#file-naming-conventions)
11. [Checklist](#stage-1-checklist)

---

## Overview

### What This Stage Covers

This document provides the complete architectural blueprint for creating a new microservice in the MicroservicesArchitecture project. By the end of Stage 1, you will have:

- ✅ Complete understanding of Clean Architecture layers
- ✅ All projects created with correct structure
- ✅ Domain entities and interfaces defined
- ✅ Application layer structure established
- ✅ Infrastructure layer foundation ready
- ✅ API layer scaffolded

### Prerequisites Before Starting

- [ ] .NET 8.0 SDK installed
- [ ] Understanding of Clean Architecture principles
- [ ] Understanding of DDD (Domain-Driven Design) basics
- [ ] Understanding of CQRS pattern
- [ ] Read [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md)
- [ ] Read [00_START_HERE.md](00_START_HERE.md)

---

## Service Architecture Decision Tree

### Step 1: Determine Service Type

```
┌─────────────────────────────────────────────────────────────────┐
│ What type of service are you creating?                          │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                 │                 │
         ▼                 ▼                 ▼
   ┌──────────┐      ┌──────────┐     ┌──────────┐
   │ Business │      │  Shared  │     │ External │
   │ Service  │      │ Service  │     │Integration│
   │          │      │          │     │ Service  │
   └──────────┘      └──────────┘     └──────────┘
        │                 │                 │
        │                 │                 │
   Examples:         Examples:         Examples:
   • Order          • Identity        • Payment Gateway
   • Product        • Tenant          • Email Provider
   • Inventory      • Notification    • SMS Provider
   • Customer       • File Manager    • Third-party APIs
```

### Step 2: Multi-Tenancy Decision

```
┌─────────────────────────────────────────────────────────────────┐
│ Does this service need multi-tenancy support?                   │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                                   │
         ▼                                   ▼
   ┌────────────┐                      ┌────────────┐
   │ YES        │                      │ NO         │
   │ (Tenant-   │                      │ (Single    │
   │  Aware)    │                      │  Database) │
   └────────────┘                      └────────────┘
         │                                   │
         ▼                                   ▼
   Examples:                           Examples:
   • Order Service                     • Identity Service (provider)
   • Product Service                   • Tenant Service (provider)
   • Customer Service                  • Email Gateway Service
   • Invoice Service                   • Logging Service

   CRITICAL: Identity, Tenant, and Notification services
   are PROVIDERS of multi-tenancy, not CONSUMERS!
   They use static configuration from appsettings.json.
```

### Step 3: Database Strategy Decision

```
┌─────────────────────────────────────────────────────────────────┐
│ How many DbContext classes does this service need?              │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                 │                 │
         ▼                 ▼                 ▼
   ┌──────────┐      ┌──────────┐     ┌──────────┐
   │ Single   │      │ Multiple │     │   None   │
   │ DbContext│      │DbContexts│     │(No DB)   │
   └──────────┘      └──────────┘     └──────────┘
        │                 │                 │
        ▼                 ▼                 ▼
   Most common:     Advanced:         Stateless:
   • OrderService   • Multi-region   • API Gateway
   • ProductService • Read/Write     • Proxy Service
   • (1 DbContext)  • Separation     • Aggregator

   CRITICAL: Each tenant gets separate database instance
   (database-per-tenant pattern), but your code only defines
   ONE DbContext class. The middleware handles routing to
   different tenant databases dynamically.
```

### Step 4: Authentication Requirement

```
┌─────────────────────────────────────────────────────────────────┐
│ Does this service require authentication?                       │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                                   │
         ▼                                   ▼
   ┌────────────┐                      ┌────────────┐
   │ YES        │                      │ NO         │
   │ (Protected)│                      │ (Public)   │
   └────────────┘                      └────────────┘
         │                                   │
         ▼                                   ▼
   Examples:                           Examples:
   • Order Service                     • Health Check Service
   • Customer Service                  • Public Catalog Service
   • Admin Service                     • Status Page Service

   Required Setup:                     No Auth Setup Needed
   • JWT validation
   • User context
   • Role-based auth
```

### Step 5: External Dependencies Decision

```
┌─────────────────────────────────────────────────────────────────┐
│ What external dependencies does this service need?              │
└─────────────────────────────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┬─────────────┐
         │                 │                 │             │
         ▼                 ▼                 ▼             ▼
   ┌──────────┐      ┌──────────┐     ┌──────────┐  ┌──────────┐
   │Identity  │      │Notification│    │ Tenant   │  │  File    │
   │ Service  │      │  Service   │    │ Service  │  │ Manager  │
   └──────────┘      └──────────┘     └──────────┘  └──────────┘
        │                 │                 │             │
   For Auth         For Push         For Multi-     For File
   • JWT tokens     Notifications    Tenancy        Storage
   • User info      • Real-time      • Config       • Uploads
                    • Firebase       • Settings     • Downloads
```

---

## Clean Architecture Layers

### Layer Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         API Layer                                │
│  • Minimal APIs (Grouped Endpoints)                              │
│  • Program.cs (Service Configuration)                            │
│  • Middleware Pipeline                                           │
│  • appsettings.json (Configuration)                              │
│  ↓ Depends on Application & Infrastructure                       │
└─────────────────────────────────────────────────────────────────┘
                           │
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│                    Application Layer                             │
│  • Commands & Queries (CQRS)                                     │
│  • Command/Query Handlers (MediatR)                              │
│  • DTOs (Data Transfer Objects)                                  │
│  • FluentValidation Validators                                   │
│  • Application Interfaces                                        │
│  • Behaviors (Validation, Logging)                               │
│  ↓ Depends on Domain Layer ONLY                                  │
└─────────────────────────────────────────────────────────────────┘
                           │
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│                    Infrastructure Layer                          │
│  • DbContext (EF Core)                                           │
│  • Repository Implementations                                    │
│  • External Service Clients                                      │
│  • Migrations (EF Core)                                          │
│  • Service Implementations                                       │
│  ↓ Depends on Application & Domain                               │
└─────────────────────────────────────────────────────────────────┘
                           │
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│                      Domain Layer                                │
│  • Entities (Domain Models)                                      │
│  • Value Objects                                                 │
│  • Domain Events                                                 │
│  • Repository Interfaces                                         │
│  • Domain Exceptions                                             │
│  • Enums                                                         │
│  ↓ No dependencies (core business logic)                         │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      Testing Layer                               │
│  • Integration Tests (WebApplicationFactory)                     │
│  • Unit Tests (xUnit, Moq)                                       │
│  • Test Helpers & Fixtures                                       │
│  ↓ References all layers for testing                             │
└─────────────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer              | Responsibility                               | Can Reference            | Cannot Reference |
| ------------------ | -------------------------------------------- | ------------------------ | ---------------- |
| **Domain**         | Core business logic, entities, rules         | Nothing                  | All              |
| **Application**    | Use cases, CQRS handlers, orchestration      | Domain                   | Infrastructure   |
| **Infrastructure** | Database, external services, implementations | Domain, Application      | API              |
| **API**            | HTTP endpoints, middleware, configuration    | All layers               | Nothing          |
| **Tests**          | Verification, integration & unit testing     | All layers (for testing) | Nothing          |

---

## Project Structure Template

### Complete Service Structure

```
src/Services/{ServiceName}/
│
├── {ServiceName}.API/                          # API Layer (Entry Point)
│   ├── appsettings.json                        # Configuration
│   ├── appsettings.Development.json            # Dev configuration
│   ├── Program.cs                              # Service startup & DI
│   ├── {ServiceName}.API.csproj                # Project file
│   │
│   ├── Endpoints/                              # Minimal API endpoints (grouped)
│   │   ├── {Feature}Endpoints.cs               # Feature-specific endpoints
│   │   └── ... (one file per feature group)
│   │
│   └── Properties/
│       └── launchSettings.json                 # Debug settings
│
├── {ServiceName}.Application/                  # Application Layer (Use Cases)
│   ├── {ServiceName}.Application.csproj        # Project file
│   │
│   ├── Commands/                               # Write operations (CQRS)
│   │   ├── {Feature}/                          # Feature-specific commands
│   │   │   ├── Create{Entity}Command.cs        # Command definition
│   │   │   ├── Update{Entity}Command.cs
│   │   │   └── Delete{Entity}Command.cs
│   │   └── ... (grouped by feature)
│   │
│   ├── Queries/                                # Read operations (CQRS)
│   │   ├── {Feature}/                          # Feature-specific queries
│   │   │   ├── Get{Entity}ByIdQuery.cs         # Query definition
│   │   │   ├── GetAll{Entity}Query.cs
│   │   │   └── Get{Entity}PagedQuery.cs
│   │   └── ... (grouped by feature)
│   │
│   ├── Handlers/                               # Command & Query handlers
│   │   ├── {Feature}/                          # Feature-specific handlers
│   │   │   ├── Create{Entity}Handler.cs        # MediatR handler
│   │   │   ├── Update{Entity}Handler.cs
│   │   │   ├── Get{Entity}ByIdHandler.cs
│   │   │   └── ... (one handler per command/query)
│   │   └── ... (grouped by feature)
│   │
│   ├── DTOs/                                   # Data Transfer Objects
│   │   ├── {Entity}Dto.cs                      # Response DTOs
│   │   ├── {Entity}CreateDto.cs                # Request DTOs
│   │   └── ... (one DTO per entity/scenario)
│   │
│   ├── Validators/                             # FluentValidation validators
│   │   ├── Create{Entity}Validator.cs          # Validation rules
│   │   ├── Update{Entity}Validator.cs
│   │   └── ... (one validator per command)
│   │
│   ├── Behaviors/                              # MediatR pipeline behaviors
│   │   ├── ValidationBehavior.cs               # Validation pipeline
│   │   ├── LoggingBehavior.cs                  # Logging pipeline
│   │   └── TransactionBehavior.cs              # Transaction handling
│   │
│   ├── Interfaces/                             # Application interfaces
│   │   ├── I{Service}Service.cs                # Service contracts
│   │   └── ... (application-level contracts)
│   │
│   └── Exceptions/                             # Application exceptions
│       ├── {Entity}NotFoundException.cs
│       ├── ValidationException.cs
│       └── ... (custom exceptions)
│
├── {ServiceName}.Domain/                       # Domain Layer (Business Logic)
│   ├── {ServiceName}.Domain.csproj             # Project file
│   │
│   ├── Entities/                               # Domain entities
│   │   ├── {Entity}.cs                         # Main entity classes
│   │   └── ... (core business objects)
│   │
│   ├── ValueObjects/                           # Value objects (DDD)
│   │   ├── {ValueObject}.cs                    # Immutable value objects
│   │   └── ... (e.g., Money, Address)
│   │
│   ├── Enums/                                  # Enumerations
│   │   ├── {Entity}Status.cs                   # Status enums
│   │   └── ... (domain enums)
│   │
│   ├── Interfaces/                             # Repository interfaces
│   │   ├── I{Entity}Repository.cs              # Repository contracts
│   │   └── ... (domain-level interfaces)
│   │
│   ├── Events/                                 # Domain events
│   │   ├── {Entity}CreatedEvent.cs             # Domain event definitions
│   │   └── ... (business events)
│   │
│   └── Exceptions/                             # Domain exceptions
│       ├── {Entity}DomainException.cs
│       └── ... (business rule violations)
│
├── {ServiceName}.Infrastructure/               # Infrastructure Layer (External Concerns)
│   ├── {ServiceName}.Infrastructure.csproj     # Project file
│   │
│   ├── Persistence/                            # Database implementation
│   │   ├── {ServiceName}DbContext.cs           # EF Core DbContext
│   │   ├── Configurations/                     # EF Core configurations
│   │   │   ├── {Entity}Configuration.cs        # Entity configurations
│   │   │   └── ... (one config per entity)
│   │   │
│   │   ├── Repositories/                       # Repository implementations
│   │   │   ├── {Entity}Repository.cs           # Concrete repositories
│   │   │   └── ... (implement domain interfaces)
│   │   │
│   │   ├── Migrations/                         # EF Core migrations
│   │   │   └── {Timestamp}_{Name}.cs           # Auto-generated
│   │   │
│   │   └── Seeds/                              # Data seeding
│   │       └── {Entity}Seed.cs                 # Seed data
│   │
│   ├── Services/                               # Infrastructure services
│   │   ├── {Service}Service.cs                 # Service implementations
│   │   └── ... (external integrations)
│   │
│   └── Extensions/                             # Infrastructure extensions
│       └── ServiceCollectionExtensions.cs      # DI registration
│
└── {ServiceName}.API.Tests/                    # Testing Layer (Integration Tests)
    ├── {ServiceName}.API.Tests.csproj          # Test project file
    │
    ├── Infrastructure/                         # Test infrastructure
    │   ├── CustomWebApplicationFactory.cs      # Test factory
    │   └── IntegrationTestBase.cs              # Test base class
    │
    ├── Endpoints/                              # Endpoint tests
    │   ├── {Feature}EndpointsTests.cs          # Feature endpoint tests
    │   └── ... (test all endpoints)
    │
    ├── Handlers/                               # Handler tests
    │   ├── {Feature}HandlerTests.cs            # Handler unit tests
    │   └── ... (test command/query handlers)
    │
    └── Helpers/                                # Test helpers
        └── {Service}TestHelper.cs              # Test utility methods
```

---

## Domain Layer Design

### Step 1: Define Domain Entities

Domain entities represent core business concepts with identity.

#### Template: Base Entity

All entities should inherit from `BaseEntity` (from Shared.Kernel):

```csharp
using IhsanDev.Shared.Kernel.Entities;

namespace {ServiceName}.Domain.Entities;

public class {Entity} : BaseEntity
{
    // Properties
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public {Status}Enum Status { get; set; }

    // Foreign Keys
    public int UserId { get; set; }
    public string? TenantId { get; set; } // REQUIRED if multi-tenant

    // Navigation Properties
    public virtual User? User { get; set; }
    public virtual ICollection<{RelatedEntity}>? {RelatedEntities} { get; set; }

    // Domain Methods (Business Logic)
    public void Activate()
    {
        if (Status == {Status}Enum.Active)
            throw new InvalidOperationException($"{Entity} is already active");

        Status = {Status}Enum.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (Status == {Status}Enum.Inactive)
            throw new InvalidOperationException($"{Entity} is already inactive");

        Status = {Status}Enum.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

#### BaseEntity Reference (from Shared.Kernel)

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 2: Define Enums

Place all status/type enums in `Domain/Enums/`:

```csharp
namespace {ServiceName}.Domain.Enums;

public enum {Entity}Status
{
    Pending = 0,
    Active = 1,
    Inactive = 2,
    Deleted = 3
}

public enum {Entity}Type
{
    TypeA = 0,
    TypeB = 1,
    TypeC = 2
}
```

### Step 3: Define Repository Interfaces

Define data access contracts in `Domain/Interfaces/`:

```csharp
namespace {ServiceName}.Domain.Interfaces;

public interface I{Entity}Repository
{
    // Read operations
    Task<{Entity}?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<{Entity}>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<{Entity}>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    // Tenant-specific (if multi-tenant)
    Task<{Entity}?> GetByIdAndTenantAsync(int id, string tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<{Entity}>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);

    // Write operations
    Task<{Entity}> AddAsync({Entity} entity, CancellationToken cancellationToken = default);
    Task UpdateAsync({Entity} entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    // Business-specific queries
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountByStatusAsync({Entity}Status status, CancellationToken cancellationToken = default);
}
```

### Step 4: Define Value Objects (Optional)

For complex domain concepts without identity:

```csharp
namespace {ServiceName}.Domain.ValueObjects;

public class Address : ValueObject
{
    public string Street { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }
    public string PostalCode { get; private set; }

    private Address() { } // EF Core constructor

    public Address(string street, string city, string country, string postalCode)
    {
        Street = street ?? throw new ArgumentNullException(nameof(street));
        City = city ?? throw new ArgumentNullException(nameof(city));
        Country = country ?? throw new ArgumentNullException(nameof(country));
        PostalCode = postalCode ?? throw new ArgumentNullException(nameof(postalCode));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return Country;
        yield return PostalCode;
    }
}
```

### Step 5: Define Domain Exceptions

```csharp
namespace {ServiceName}.Domain.Exceptions;

public class {Entity}NotFoundException : Exception
{
    public {Entity}NotFoundException(int id)
        : base($"{Entity} with ID {id} was not found")
    {
    }
}

public class {Entity}AlreadyExistsException : Exception
{
    public {Entity}AlreadyExistsException(string name)
        : base($"{Entity} with name '{name}' already exists")
    {
    }
}
```

---

## Application Layer Design

### Step 1: Define Commands (Write Operations)

Place commands in `Application/Commands/{Feature}/`:

```csharp
using MediatR;

namespace {ServiceName}.Application.Commands.{Feature};

public record Create{Entity}Command(
    string Name,
    string Description,
    int UserId,
    string? TenantId = null // Required if multi-tenant
) : IRequest<{Entity}Dto>;

public record Update{Entity}Command(
    int Id,
    string Name,
    string Description
) : IRequest<{Entity}Dto>;

public record Delete{Entity}Command(int Id) : IRequest<Unit>;
```

### Step 2: Define Queries (Read Operations)

Place queries in `Application/Queries/{Feature}/`:

```csharp
using MediatR;

namespace {ServiceName}.Application.Queries.{Feature};

public record Get{Entity}ByIdQuery(int Id) : IRequest<{Entity}Dto?>;

public record GetAll{Entity}Query(
    string? TenantId = null,
    int? UserId = null
) : IRequest<IEnumerable<{Entity}Dto>>;

public record Get{Entity}PagedQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    {Entity}Status? Status = null
) : IRequest<PagedResult<{Entity}Dto>>;
```

### Step 3: Define DTOs

Place DTOs in `Application/DTOs/`:

```csharp
namespace {ServiceName}.Application.DTOs;

// Response DTO (full details)
public class {Entity}Dto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// List DTO (summary)
public class {Entity}ListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Paged result wrapper
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

### Step 4: Define Command/Query Handlers

Place handlers in `Application/Handlers/{Feature}/`:

```csharp
using MediatR;
using {ServiceName}.Domain.Interfaces;
using {ServiceName}.Domain.Entities;
using {ServiceName}.Application.DTOs;

namespace {ServiceName}.Application.Handlers.{Feature};

public class Create{Entity}Handler : IRequestHandler<Create{Entity}Command, {Entity}Dto>
{
    private readonly I{Entity}Repository _repository;

    public Create{Entity}Handler(I{Entity}Repository repository)
    {
        _repository = repository;
    }

    public async Task<{Entity}Dto> Handle(
        Create{Entity}Command request,
        CancellationToken cancellationToken)
    {
        var entity = new {Entity}
        {
            Name = request.Name,
            Description = request.Description,
            UserId = request.UserId,
            TenantId = request.TenantId,
            Status = {Entity}Status.Active
        };

        var created = await _repository.AddAsync(entity, cancellationToken);

        return new {Entity}Dto
        {
            Id = created.Id,
            Name = created.Name,
            Description = created.Description,
            Status = created.Status.ToString(),
            UserId = created.UserId,
            TenantId = created.TenantId,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt
        };
    }
}
```

### Step 5: Define Validators

Place validators in `Application/Validators/`:

```csharp
using FluentValidation;

namespace {ServiceName}.Application.Validators;

public class Create{Entity}Validator : AbstractValidator<Create{Entity}Command>
{
    public Create{Entity}Validator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("Valid User ID is required");
    }
}
```

---

## Infrastructure Layer Design

### Step 1: Define DbContext

Place in `Infrastructure/Persistence/`:

```csharp
using Microsoft.EntityFrameworkCore;
using {ServiceName}.Domain.Entities;

namespace {ServiceName}.Infrastructure.Persistence;

public class {ServiceName}DbContext : DbContext
{
    public {ServiceName}DbContext(DbContextOptions<{ServiceName}DbContext> options)
        : base(options)
    {
    }

    public DbSet<{Entity}> {Entities} { get; set; }
    // Add more DbSets for other entities

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof({ServiceName}DbContext).Assembly);
    }
}
```

### Step 2: Define Entity Configurations

Place in `Infrastructure/Persistence/Configurations/`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using {ServiceName}.Domain.Entities;

namespace {ServiceName}.Infrastructure.Persistence.Configurations;

public class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.ToTable("{Entities}");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.TenantId)
            .HasMaxLength(100);

        // Indexes for performance
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Status);

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### Step 3: Implement Repositories

Place in `Infrastructure/Persistence/Repositories/`:

```csharp
using Microsoft.EntityFrameworkCore;
using {ServiceName}.Domain.Entities;
using {ServiceName}.Domain.Interfaces;

namespace {ServiceName}.Infrastructure.Persistence.Repositories;

public class {Entity}Repository : I{Entity}Repository
{
    private readonly {ServiceName}DbContext _context;

    public {Entity}Repository({ServiceName}DbContext context)
    {
        _context = context;
    }

    public async Task<{Entity}?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.{Entities}
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<{Entity}>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.{Entities}
            .Include(e => e.User)
            .ToListAsync(cancellationToken);
    }

    public async Task<{Entity}> AddAsync({Entity} entity, CancellationToken cancellationToken = default)
    {
        await _context.{Entities}.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync({Entity} entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.{Entities}.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            _context.{Entities}.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // Tenant-specific methods (if multi-tenant)
    public async Task<{Entity}?> GetByIdAndTenantAsync(
        int id,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.{Entities}
            .FirstOrDefaultAsync(
                e => e.Id == id && e.TenantId == tenantId,
                cancellationToken);
    }
}
```

---

## API Layer Design

### Step 1: Configure Program.cs

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using IhsanDev.Shared.Infrastructure.Extensions;
using {ServiceName}.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Database
// ============================================
builder.Services.AddDatabaseContext<{ServiceName}DbContext>(
    builder.Configuration,
    migrationAssembly: typeof({ServiceName}DbContext).Assembly.GetName().Name);

// ============================================
// Authentication (if required)
// ============================================
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT Secret is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();

// ============================================
// Multi-Tenancy (if required)
// ============================================
builder.Services.AddMultiTenancy(builder.Configuration);

// ============================================
// Application Services
// ============================================
builder.Services.AddHttpContextAccessor();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Create{Entity}Command).Assembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Create{Entity}Validator).Assembly);

// Repositories
builder.Services.AddScoped<I{Entity}Repository, {Entity}Repository>();

var app = builder.Build();

// ============================================
// Middleware Pipeline
// ============================================
app.UseAuthentication(); // If authentication enabled
app.UseAuthorization();

// Database migration (if-else based on multi-tenancy)
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");
if (multiTenancyEnabled)
    app.UseTenantDatabaseMigration(); // Requires x-tenant-id header
else
    app.UseDefaultDatabaseMigration(); // Uses appsettings.json

// ============================================
// API Endpoints
// ============================================
var {entities}Group = app.MapGroup("/api/{entities}")
    .RequireAuthorization(); // If authentication required

{entities}Group.MapGet("/", GetAll{Entities});
{entities}Group.MapGet("/{id:int}", Get{Entity}ById);
{entities}Group.MapPost("/", Create{Entity});
{entities}Group.MapPut("/{id:int}", Update{Entity});
{entities}Group.MapDelete("/{id:int}", Delete{Entity});

app.Run();

// Handler methods (defined in Endpoints file)
```

### Step 2: Define Endpoints

Place in `API/Endpoints/{Feature}Endpoints.cs`:

```csharp
using MediatR;
using {ServiceName}.Application.Commands.{Feature};
using {ServiceName}.Application.Queries.{Feature};

namespace {ServiceName}.API.Endpoints;

public static class {Entity}Endpoints
{
    public static void Map{Entity}Endpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/{entities}")
            .WithTags("{Entities}")
            .RequireAuthorization();

        group.MapGet("/", GetAll{Entities})
            .WithName("GetAll{Entities}")
            .Produces<IEnumerable<{Entity}Dto>>(200);

        group.MapGet("/{id:int}", Get{Entity}ById)
            .WithName("Get{Entity}ById")
            .Produces<{Entity}Dto>(200)
            .Produces(404);

        group.MapPost("/", Create{Entity})
            .WithName("Create{Entity}")
            .Produces<{Entity}Dto>(201)
            .ProducesValidationProblem();

        group.MapPut("/{id:int}", Update{Entity})
            .WithName("Update{Entity}")
            .Produces<{Entity}Dto>(200)
            .Produces(404);

        group.MapDelete("/{id:int}", Delete{Entity})
            .WithName("Delete{Entity}")
            .Produces(204)
            .Produces(404);
    }

    private static async Task<IResult> GetAll{Entities}(
        IMediator mediator,
        HttpContext context)
    {
        var query = new GetAll{Entity}Query();
        var result = await mediator.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> Get{Entity}ById(
        int id,
        IMediator mediator)
    {
        var query = new Get{Entity}ByIdQuery(id);
        var result = await mediator.Send(query);
        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> Create{Entity}(
        Create{Entity}Command command,
        IMediator mediator)
    {
        var result = await mediator.Send(command);
        return Results.Created($"/api/{entities}/{result.Id}", result);
    }

    private static async Task<IResult> Update{Entity}(
        int id,
        Update{Entity}Command command,
        IMediator mediator)
    {
        if (id != command.Id)
            return Results.BadRequest("ID mismatch");

        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> Delete{Entity}(
        int id,
        IMediator mediator)
    {
        await mediator.Send(new Delete{Entity}Command(id));
        return Results.NoContent();
    }
}
```

---

## Testing Layer Design

### Step 1: Create Test Factory

```csharp
using IhsanDev.Shared.Testing.Infrastructure;

namespace {ServiceName}.API.Tests.Infrastructure;

public class CustomWebApplicationFactory :
    IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();

        // Override configuration for tests
        config["MultiTenancy:Enabled"] = "false";
        config["Jwt:Secret"] = "test-secret-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestIssuer";
        config["Jwt:Audience"] = "TestAudience";

        return config;
    }
}
```

### Step 2: Create Test Base Class

```csharp
using IhsanDev.Shared.Testing.Infrastructure;
using {ServiceName}.Infrastructure.Persistence;

namespace {ServiceName}.API.Tests.Infrastructure;

public abstract class IntegrationTestBase :
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<{ServiceName}DbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
    }
}
```

---

## File Naming Conventions

### General Rules

| File Type     | Naming Convention              | Example                    |
| ------------- | ------------------------------ | -------------------------- |
| Entity        | `{Entity}.cs`                  | `Order.cs`                 |
| Enum          | `{Entity}Status.cs`            | `OrderStatus.cs`           |
| Interface     | `I{Entity}Repository.cs`       | `IOrderRepository.cs`      |
| Repository    | `{Entity}Repository.cs`        | `OrderRepository.cs`       |
| Command       | `{Action}{Entity}Command.cs`   | `CreateOrderCommand.cs`    |
| Query         | `Get{Entity}{Filter}Query.cs`  | `GetOrderByIdQuery.cs`     |
| Handler       | `{Action}{Entity}Handler.cs`   | `CreateOrderHandler.cs`    |
| DTO           | `{Entity}Dto.cs`               | `OrderDto.cs`              |
| Validator     | `{Action}{Entity}Validator.cs` | `CreateOrderValidator.cs`  |
| Configuration | `{Entity}Configuration.cs`     | `OrderConfiguration.cs`    |
| DbContext     | `{ServiceName}DbContext.cs`    | `OrderServiceDbContext.cs` |
| Endpoints     | `{Feature}Endpoints.cs`        | `OrderEndpoints.cs`        |
| Test          | `{Feature}EndpointsTests.cs`   | `OrderEndpointsTests.cs`   |

---

## Stage 1 Checklist

Before proceeding to Stage 2, ensure you have:

### Domain Layer

- [ ] Created `{ServiceName}.Domain` project
- [ ] Defined all entities in `Entities/` folder
- [ ] Created all enums in `Enums/` folder
- [ ] Defined repository interfaces in `Interfaces/` folder
- [ ] Created domain exceptions in `Exceptions/` folder
- [ ] Added value objects (if needed) in `ValueObjects/` folder

### Application Layer

- [ ] Created `{ServiceName}.Application` project
- [ ] Referenced `{ServiceName}.Domain` project
- [ ] Defined commands in `Commands/{Feature}/` folders
- [ ] Defined queries in `Queries/{Feature}/` folders
- [ ] Created DTOs in `DTOs/` folder
- [ ] Implemented command handlers in `Handlers/{Feature}/` folders
- [ ] Implemented query handlers in `Handlers/{Feature}/` folders
- [ ] Created validators in `Validators/` folder

### Infrastructure Layer

- [ ] Created `{ServiceName}.Infrastructure` project
- [ ] Referenced `{ServiceName}.Domain` and `{ServiceName}.Application` projects
- [ ] Created DbContext in `Persistence/{ServiceName}DbContext.cs`
- [ ] Created entity configurations in `Persistence/Configurations/`
- [ ] Implemented repositories in `Persistence/Repositories/`
- [ ] Added necessary NuGet packages (EF Core, etc.)

### API Layer

- [ ] Created `{ServiceName}.API` project
- [ ] Referenced all other projects
- [ ] Configured `Program.cs` with DI, middleware, and endpoints
- [ ] Created endpoint handlers in `Endpoints/` folder
- [ ] Added `appsettings.json` and `appsettings.Development.json`
- [ ] Configured `launchSettings.json` with correct port

### Testing Layer

- [ ] Created `{ServiceName}.API.Tests` project
- [ ] Referenced Shared.Testing library
- [ ] Created `CustomWebApplicationFactory.cs`
- [ ] Created `IntegrationTestBase.cs`
- [ ] Set up test infrastructure

---

## Next Steps

**Congratulations!** You've completed Stage 1 - Service Architecture & Structure.

**Next:** Proceed to [Stage 2: Configuration & Integration](NEW_SERVICE_DESIGN_PATTERN_STAGE_2.md) to:

- Configure authentication (JWT)
- Enable multi-tenancy (if needed)
- Set up database providers
- Configure shared libraries
- Add middleware and behaviors

---

**Built with ❤️ for Clean Architecture & DDD**

_Last Updated: January 2025_
