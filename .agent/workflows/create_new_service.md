---
description: Comprehensive guide and workflow for creating a new microservice within the MicroservicesArchitecture solution, detailing standard file structures, coding patterns, and shared kernel usage.
---

# Creating a New Service Workflow

This document outlines the standard procedure for creating a new microservice in the `MicroservicesArchitecture` solution, using the **Identity Service** (`@[MicroservicesArchitecture/src/Services/Identity]`) as the reference implementation.

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

**Reference**: `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Kernel/Entities/BaseEntity.cs]`

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

Repository interfaces must inherit from `IRepository<T>` where `T` is the entity type. This provides standard CRUD operations automatically.

**Reference**: `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Persistence/IRepository.cs]`

**Example Pattern:**

```csharp
using IhsanDev.Shared.Infrastructure.Persistence;
using [ServiceName].Domain.Entities;

namespace [ServiceName].Domain.Repositories;

public interface IMyEntityRepository : IRepository<MyEntity>
{
    // Add specific query methods here
    Task<MyEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
```

## 3. Application Layer

This layer handles the business logic using CQRS with MediatR.

### 1- Creating DTOs

All Data Transfer Objects (DTOs) must inherit from `BaseDto`. This ensures they carry the standard entity fields.

**Reference**: `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Kernel/Dto/BaseDto.cs]`

**Example Pattern:**

```csharp
using IhsanDev.Shared.Kernel.Dto.Identity;

namespace [ServiceName].Application.DTOs;

public class MyEntityDto : BaseDto
{
    public string Name { get; set; }
}
```

### 2- Creating Commands and Queries

Commands are defined as `record`s implementing `IRequest<TResponse>`. Validators should be placed in the same file or a `Validators` folder, inheriting from `LocalizedValidator<T>`.

**Example Pattern:**

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

Handlers implement `IRequestHandler<TRequest, TResponse>`. They should inject necessary repositories and services.

**Example Pattern:**

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
        // Business logic...
        var entity = new MyEntity { Name = request.Name };
        await _repository.AddAsync(entity, cancellationToken);
        // Map to DTO...
        return new MyEntityDto { /* ... */ };
    }
}
```

## 4. Infrastructure Layer

This layer implements the interfaces defined in the Domain and Application layers.

### 1- Persistence and DbContext

The `DbContext` must inherit from `BaseDbContext`. This base class handles the automatic setting of audit fields (`Created`, `LastModified`, etc.) during `SaveChangesAsync`.

**Reference**: `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Persistence/BaseDbContext.cs]`

**Example Pattern:**

```csharp
using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using [ServiceName].Domain.Entities;
using IhsanDev.Shared.Infrastructure.Services.Identity;

namespace [ServiceName].Infrastructure.Persistence;

public class MyServiceDbContext : BaseDbContext
{
    public MyServiceDbContext(DbContextOptions options, ICurrentUserService? currentUserService = null)
        : base(options, currentUserService) { }

    public DbSet<MyEntity> MyEntities { get; set; }
}
```

### 2- Repository Implementations

Repositories must inherit from `Repository<T>` and implement the specific domain interface. `Repository<T>` handles the standard implementation of `IRepository<T>` methods (Add, Update, Delete, GetById, etc.).

**Reference**: `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Persistence/Repository.cs]`

**Example Pattern:**

```csharp
using IhsanDev.Shared.Infrastructure.Persistence;
using [ServiceName].Domain.Repositories;
using [ServiceName].Domain.Entities;
using [ServiceName].Infrastructure.Persistence;

namespace [ServiceName].Infrastructure.Repositories;

public class MyEntityRepository : Repository<MyEntity>, IMyEntityRepository
{
    public MyEntityRepository(MyServiceDbContext context) : base(context) { }

    // Implement specific methods
    public async Task<MyEntity?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _dbSet.FirstOrDefaultAsync(e => e.Name == name && !e.IsArchived, cancellationToken);
    }
}
```

### 3- Extensions for Dependency Injection

Create an extension method to register repositories and services. This keeps `Program.cs` clean.

**Reference**: `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Extensions]`

**Example Pattern:**

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

This layer exposes the functionality via HTTP endpoints using Minimal APIs.

### 1- Creating Endpoints in Extensions

Define endpoints using extension methods on `WebApplication`. Use `MapGroup` to organize routes.

**Reference**: `@[MicroservicesArchitecture/src/Services/Identity/Identity.API/Extensions]`

**Example Pattern:**

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

Use the `ValidationFilter<T>` to simplify validation logic in your endpoints. Inherit from `SharedValidationFilter<T>` in your API project.

**Reference**: `@[MicroservicesArchitecture/src/Services/Identity/Identity.API/Filters]`

**Code Pattern:**

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

API Handlers are `static` classes/methods that act as a bridge between the HTTP request and the Application layer (MediatR).

**Reference**: `@[MicroservicesArchitecture/src/Services/Identity/Identity.API/Handlers]`

**Example Pattern:**

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

## 6. Shared Files and Resources

The `Shared` kernel provides essential building blocks that MUST be used to maintain consistency.

### Essential Shared Components:

1.  **Persistence Base Classes**:
    - `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Persistence/BaseDbContext.cs]`: Base EfCore Context with auditing.
    - `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Persistence/DatabaseSettings.cs]`: Configuration settings.
    - `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Persistence/IRepository.cs]`: Generic repository interface.
    - `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Persistence/Repository.cs]`: Generic repository implementation.

2.  **Middleware**:
    - `GlobalExceptionHandlingMiddleware.cs`: Centralized error handling.
    - `JwtTenantVerificationMiddleware.cs` & `TenantMiddleware.cs`: Multi-tenancy support.
    - `LocalizationMiddleware.cs`: Handling culture information.
    - **Reference**: `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Infrastructure/Middleware/]`

3.  **Pagination**:
    - Use `PaginatedListAsync` from `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Application/Common/Mappings/MappingExtensions.cs]` and `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Application/Common/Models/PaginatedList.cs]`.

4.  **Localization**:
    - **Do not use hardcoded strings.**
    - Use `ILocalizationService` to retrieve strings.
    - Define keys in `LocalizationKeys.cs`.
    - **Reference**: `@[MicroservicesArchitecture/src/Shared/IhsanDev.Shared.Application/Localization/]`
