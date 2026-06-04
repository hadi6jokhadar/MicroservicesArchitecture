---
agent: "agent"
description: "Comprehensive guide for creating a new microservice in the MicroservicesArchitecture solution, detailing standard file structures, coding patterns, shared kernel usage, and database strategy selection."
---

# Creating a New Service Workflow

This document outlines the standard procedure for creating a new microservice in the `MicroservicesArchitecture` solution, using the **Identity Service** (`src/Services/Identity`) as the reference implementation.

## 0. Choose a Database Strategy (DO THIS FIRST)

Before writing any code, read [database-strategy.instructions.md](../instructions/database-strategy.instructions.md) and answer:

| Question                                                            | Answer → Strategy                     |
| ------------------------------------------------------------------- | ------------------------------------- |
| Is this a system registry with no tenant data?                      | **A — Single Global DB**              |
| Does it store data per-tenant with full isolation?                  | **B — Per-Tenant DB**                 |
| Does it have both a shared processing queue AND per-tenant history? | **C — Dual DB**                       |
| Is it a global provider where tenants can override rows?            | **D — Global + Discriminator Column** |

The chosen strategy determines:

- Whether the DbContext injects `ITenantContext`
- Whether `AddMultiTenancy()` is called in DI
- The exact `Program.cs` middleware pipeline order
- Which attributes (`[BypassTenant]`, `[OptionalTenant]`) apply

> **Reference implementations:**
>
> - Strategy A → `src/Services/Tenant/Tenant.Infrastructure/Persistence/TenantDbContext.cs`
> - Strategy B → `src/Services/Identity/Identity.Infrastructure/Persistence/IdentityDbContext.cs`
> - Strategy C → `src/Services/Notification/Notification.Infrastructure/Persistence/`
> - Strategy D → `src/Services/Translation/Translation.Infrastructure/Persistence/TranslationDbContext.cs`

---

## 1. Folder Structure Overview

Each service follows a Clean Architecture pattern with four main projects:

- **API**: The entry point (Controllers/Endpoints).
- **Application**: Business logic (CQRS, DTOs, Validators).
- **Domain**: Core entities and repository interfaces.
- **Infrastructure**: Database implementation, external services.

## 2. Domain Layer

This layer contains the core business objects and interfaces.

### 1- Creating Entities

Entities must inherit from `BaseEntity` to ensure standard auditing fields (`Id`, `Created`, `CreatedBy`, `LastModified`, `LastModifiedBy`, `IsArchived`, `Status`).

**Reference**: `src/Shared/IhsanDev.Shared.Kernel/Entities/BaseEntity.cs`

**Example Pattern:**

```csharp
using IhsanDev.Shared.Kernel.Entities;

namespace [ServiceName].Domain.Entities;

public class MyEntity : BaseEntity
{
    public string Name { get; set; }
    // Add other properties
}
```

### 2- Creating Repository Interfaces

Repository interfaces must inherit from `IRepository<T>` where `T` is the entity type.

**Reference**: `src/Shared/IhsanDev.Shared.Infrastructure/Persistence/IRepository.cs`

**Example Pattern:**

```csharp
using IhsanDev.Shared.Infrastructure.Persistence;
using [ServiceName].Domain.Entities;

namespace [ServiceName].Domain.Repositories;

public interface IMyEntityRepository : IRepository<MyEntity>
{
    Task<MyEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
```

## 3. Application Layer

### 1- Creating DTOs

All DTOs must inherit from `BaseDto`.

**Reference**: `src/Shared/IhsanDev.Shared.Kernel/Dto/BaseDto.cs`

```csharp
using IhsanDev.Shared.Kernel.Dto.Identity;

namespace [ServiceName].Application.DTOs;

public class MyEntityDto : BaseDto
{
    public string Name { get; set; }
}
```

### 2- Creating Commands and Queries

Commands are `record`s implementing `IRequest<TResponse>`. Validators inherit from `LocalizedValidator<T>`.

```csharp
using MediatR;
using FluentValidation;
using IhsanDev.Shared.Application.Validation;
using IhsanDev.Shared.Application.Localization;

namespace [ServiceName].Application.Commands;

public record CreateMyEntityCommand(string Name) : IRequest<MyEntityDto>;

public class CreateMyEntityCommandValidator : LocalizedValidator<CreateMyEntityCommand>
{
    public CreateMyEntityCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Name"));
    }
}
```

### 3- Creating Handlers

```csharp
using MediatR;
using [ServiceName].Domain.Repositories;
using [ServiceName].Application.DTOs;

namespace [ServiceName].Application.Handlers;

public class CreateMyEntityHandler : IRequestHandler<CreateMyEntityCommand, MyEntityDto>
{
    private readonly IMyEntityRepository _repository;

    public CreateMyEntityHandler(IMyEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<MyEntityDto> Handle(CreateMyEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = new MyEntity { Name = request.Name };
        await _repository.AddAsync(entity, cancellationToken);
        return new MyEntityDto { /* ... */ };
    }
}
```

## 4. Infrastructure Layer

### 1- Persistence and DbContext

The `DbContext` must inherit from `BaseDbContext`.

**Reference**: `src/Shared/IhsanDev.Shared.Infrastructure/Persistence/BaseDbContext.cs`

> ⚠️ **Use the DbContext pattern that matches the strategy you chose in Step 0.**
> All complete patterns (with `OnConfiguring`, `ITenantContext`, fallback logic) are in
> [database-strategy.instructions.md](../instructions/database-strategy.instructions.md).

**Strategy A / D — Global DB (no tenant switching):**

```csharp
namespace [ServiceName].Infrastructure.Persistence;

public class MyServiceDbContext : BaseDbContext
{
    public MyServiceDbContext(
        DbContextOptions<MyServiceDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options, currentUserService) { }

    public DbSet<MyEntity> MyEntities => Set<MyEntity>();
}
```

**Strategy B — Per-Tenant DB (copy `OnConfiguring` from IdentityDbContext):**

```csharp
namespace [ServiceName].Infrastructure.Persistence;

public class MyServiceDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<MyServiceDbContext>? _logger;

    public MyServiceDbContext(
        DbContextOptions<MyServiceDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<MyServiceDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<MyEntity> MyEntities => Set<MyEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // See full implementation in database-strategy.instructions.md — Strategy B
    }
}
```

### 2- Repository Implementations

**Reference**: `src/Shared/IhsanDev.Shared.Infrastructure/Persistence/Repository.cs`

```csharp
using IhsanDev.Shared.Infrastructure.Persistence;
using [ServiceName].Domain.Repositories;
using [ServiceName].Domain.Entities;
using [ServiceName].Infrastructure.Persistence;

namespace [ServiceName].Infrastructure.Repositories;

public class MyEntityRepository : Repository<MyEntity>, IMyEntityRepository
{
    public MyEntityRepository(MyServiceDbContext context) : base(context) { }

    public async Task<MyEntity?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _dbSet.FirstOrDefaultAsync(e => e.Name == name && !e.IsArchived, cancellationToken);
    }
}
```

### 3- Extensions for Dependency Injection

```csharp
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IMyEntityRepository, MyEntityRepository>();
        return services;
    }
}
```

## 5. API Layer

### 1- Creating Endpoints in Extensions

**Reference**: `src/Services/Identity/Identity.API/Extensions`

```csharp
public static class EndpointMappingExtensions
{
    public static WebApplication MapMyEntityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/my-entities")
            .WithTags("My Entity Management")
            .RequireAuthorization()
            .WithOpenApi();

        group.MapPost("/", MyEntityApiHandlers.CreateHandler)
            .WithName("CreateMyEntity")
            .Produces<MyEntityDto>(201)
            .AddEndpointFilter<ValidationFilter<CreateMyEntityCommand>>();

        return app;
    }
}
```

### 2- Using SharedValidationFilter

**Reference**: `src/Services/Identity/Identity.API/Filters`

```csharp
using FluentValidation;
using IhsanDev.Shared.Infrastructure.Filters;

namespace [ServiceName].API.Filters;

public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
{
    public ValidationFilter(IValidator<T> validator) : base(validator) { }
}
```

### 3- Creating API Handlers

**Reference**: `src/Services/Identity/Identity.API/Handlers`

```csharp
public static class MyEntityApiHandlers
{
    public static async Task<IResult> CreateHandler(
        CreateMyEntityCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }
}
```

## 6. Program.cs — Middleware Pipeline

Wire the pipeline based on the strategy chosen in Step 0. The order is **critical**.

> Full DI registration + pipeline code for each strategy is in
> [database-strategy.instructions.md](../instructions/database-strategy.instructions.md).

**Always add observability** (same for every strategy):

```csharp
// After AddCustomLogging:
builder.Services.AddPlatformObservability(builder.Configuration, "MyServiceName");

// Before app.Run():
app.MapPrometheusScrapingEndpoint("/metrics");
```

Also add to `appsettings.json`:

```json
"Observability": {
  "OtlpEndpoint": "http://localhost:4317"
}
```

And add the service port to `prometheus.yml` in the repo root.

**Strategy A / D — Global DB (no tenant middleware):**

```csharp
// DI
builder.Services.AddDatabaseContext<MyServiceDbContext>(builder.Configuration, "MyService.Infrastructure");
builder.Services.AddPlatformObservability(builder.Configuration, "MyServiceName");
// Pipeline
app.UseDefaultDatabaseMigration<MyServiceDbContext>();
app.UseAuthentication();
app.UseAuthorization();
app.MapPrometheusScrapingEndpoint("/metrics");
```

**Strategy B — Per-Tenant DB:**

```csharp
// DI
builder.Services.AddMultiTenancy(builder.Configuration);
builder.Services.AddDatabaseContext<MyServiceDbContext>(builder.Configuration, "MyService.Infrastructure");
builder.Services.AddPlatformObservability(builder.Configuration, "MyServiceName");
// Pipeline (ORDER IS NON-NEGOTIABLE)
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();                               // replaces UseCors()
app.UseJwtTenantVerification(builder.Configuration);
app.UseDefaultDatabaseMigration<MyServiceDbContext>();  // always first
if (multiTenancyEnabled) app.UseTenantDatabaseMigration<MyServiceDbContext>(builder.Configuration);
app.UseAuthentication();
app.UseAuthorization();
app.MapPrometheusScrapingEndpoint("/metrics");
```

**Strategy C — Dual DB:**

```csharp
// DI
builder.Services.AddMultiTenancy(builder.Configuration);
builder.Services.AddDatabaseContext<MyServiceGlobalDbContext>(builder.Configuration, "...");
builder.Services.AddDatabaseContext<MyServiceTenantDbContext>(builder.Configuration, "...");
builder.Services.AddPlatformObservability(builder.Configuration, "MyServiceName");
// Pipeline
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();
app.UseJwtTenantVerification(builder.Configuration);
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<MyServiceGlobalDbContext>(builder.Configuration);
    app.UseTenantDatabaseMigration<MyServiceTenantDbContext>(builder.Configuration);
}
else
{
    app.UseDefaultDatabaseMigration<MyServiceGlobalDbContext>();
    app.UseDefaultDatabaseMigration<MyServiceTenantDbContext>();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapPrometheusScrapingEndpoint("/metrics");
```

---

## 7. Shared Files and Resources

### Essential Shared Components

1. **Persistence Base Classes**:
   - `src/Shared/IhsanDev.Shared.Infrastructure/Persistence/BaseDbContext.cs`
   - `src/Shared/IhsanDev.Shared.Infrastructure/Persistence/IRepository.cs`
   - `src/Shared/IhsanDev.Shared.Infrastructure/Persistence/Repository.cs`

2. **Middleware** (`src/Shared/IhsanDev.Shared.Infrastructure/Middleware/`):
   - `GlobalExceptionHandlingMiddleware.cs`
   - `TenantMiddleware.cs`
   - `LocalizationMiddleware.cs`
   - `DatabaseMigrationMiddleware.cs`
   - `JwtTenantVerificationMiddleware.cs`

3. **Attributes** (`src/Shared/IhsanDev.Shared.Infrastructure/Attributes/`):
   - `BypassTenantAttribute.cs` — skip tenant resolution (SuperAdmin/global endpoints)
   - `OptionalTenantAttribute.cs` — tenant resolution succeeds with or without x-tenant-id

4. **Pagination**:
   - `src/Shared/IhsanDev.Shared.Application/Common/Mappings/MappingExtensions.cs`
   - `src/Shared/IhsanDev.Shared.Application/Common/Models/PaginatedList.cs`

5. **Localization** (`src/Shared/IhsanDev.Shared.Application/Localization/`):
   - Do **not** use hardcoded strings.
   - Use `ILocalizationService` to retrieve strings.
   - Define keys in `LocalizationKeys.cs`.
