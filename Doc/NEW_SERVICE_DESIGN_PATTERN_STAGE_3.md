# 🚀 New Service Design Pattern - Stage 3: Implementation, Testing & Deployment

**Version:** 1.0  
**Last Updated:** January 2025  
**Stage:** 3 of 3 - Implementation, Testing & Deployment  
**Previous Stage:** [Stage 2: Configuration & Integration](NEW_SERVICE_DESIGN_PATTERN_STAGE_2.md)

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Complete CQRS Implementation](#complete-cqrs-implementation)
3. [API Endpoint Patterns](#api-endpoint-patterns)
4. [Error Handling & Validation](#error-handling--validation)
5. [Testing Strategy](#testing-strategy)
6. [Integration Testing](#integration-testing)
7. [Unit Testing](#unit-testing)
8. [Performance Testing](#performance-testing)
9. [API Documentation](#api-documentation)
10. [Deployment Guide](#deployment-guide)
11. [Monitoring & Logging](#monitoring--logging)
12. [Production Checklist](#production-checklist)

---

## Overview

### What This Stage Covers

This document provides complete implementation, testing, and deployment patterns for your new microservice. By the end of Stage 3, you will have:

- ✅ Complete CQRS handlers implemented
- ✅ All API endpoints defined and documented
- ✅ Comprehensive integration tests
- ✅ Unit tests for critical logic
- ✅ Error handling and validation
- ✅ Deployment-ready service
- ✅ Monitoring and logging configured

### Prerequisites

Before starting Stage 3:

- [ ] Completed [Stage 1: Architecture & Structure](NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md)
- [ ] Completed [Stage 2: Configuration & Integration](NEW_SERVICE_DESIGN_PATTERN_STAGE_2.md)
- [ ] All projects configured correctly
- [ ] Database migrations applied
- [ ] Authentication configured (if needed)

---

## Complete CQRS Implementation

### Command Handler Pattern (Write Operations)

**File:** `{ServiceName}.Application/Handlers/{Feature}/Create{Entity}Handler.cs`

```csharp
using MediatR;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using {ServiceName}.Domain.Entities;
using {ServiceName}.Domain.Interfaces;
using {ServiceName}.Application.DTOs;
using {ServiceName}.Application.Commands.{Feature};

namespace {ServiceName}.Application.Handlers.{Feature};

public class Create{Entity}Handler : IRequestHandler<Create{Entity}Command, {Entity}Dto>
{
    private readonly I{Entity}Repository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<Create{Entity}Handler> _logger;

    public Create{Entity}Handler(
        I{Entity}Repository repository,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext,
        ILogger<Create{Entity}Handler> logger)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<{Entity}Dto> Handle(
        Create{Entity}Command request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new {Entity}: {Name}", request.Name);

        // 1. Extract user context (if authenticated)
        int? userId = null;
        string? userEmail = null;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            var emailClaim = httpContext.User.FindFirst(ClaimTypes.Email);

            if (userIdClaim != null)
                userId = int.Parse(userIdClaim.Value);

            userEmail = emailClaim?.Value;
        }

        // 2. Extract tenant context (if multi-tenant)
        string? tenantId = null;
        if (_tenantContext.HasTenant && _tenantContext.CurrentTenant != null)
        {
            tenantId = _tenantContext.CurrentTenant.TenantId;
            _logger.LogInformation("Creating {Entity} for tenant: {TenantId}", tenantId);
        }

        // 3. Business validation
        if (userId.HasValue)
        {
            var existingEntity = await _repository.ExistsByNameAndUserAsync(
                request.Name,
                userId.Value,
                cancellationToken);

            if (existingEntity)
            {
                _logger.LogWarning(
                    "{Entity} with name {Name} already exists for user {UserId}",
                    request.Name,
                    userId);
                throw new DuplicateEntityException($"{Entity} with name '{request.Name}' already exists");
            }
        }

        // 4. Create domain entity
        var entity = new {Entity}
        {
            Name = request.Name,
            Description = request.Description,
            Status = {Entity}Status.Active,
            UserId = userId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 5. Persist to database
        var created = await _repository.AddAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Successfully created {Entity} with ID: {Id}",
            created.Id);

        // 6. Map to DTO and return
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

### Query Handler Pattern (Read Operations)

**File:** `{ServiceName}.Application/Handlers/{Feature}/Get{Entity}ByIdHandler.cs`

```csharp
using MediatR;
using {ServiceName}.Domain.Interfaces;
using {ServiceName}.Application.DTOs;
using {ServiceName}.Application.Queries.{Feature};

namespace {ServiceName}.Application.Handlers.{Feature};

public class Get{Entity}ByIdHandler : IRequestHandler<Get{Entity}ByIdQuery, {Entity}Dto?>
{
    private readonly I{Entity}Repository _repository;
    private readonly ILogger<Get{Entity}ByIdHandler> _logger;

    public Get{Entity}ByIdHandler(
        I{Entity}Repository repository,
        ILogger<Get{Entity}ByIdHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<{Entity}Dto?> Handle(
        Get{Entity}ByIdQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching {Entity} with ID: {Id}", request.Id);

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("{Entity} with ID {Id} not found", request.Id);
            return null;
        }

        return new {Entity}Dto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Status = entity.Status.ToString(),
            UserId = entity.UserId,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
```

### Paginated Query Handler Pattern

**File:** `{ServiceName}.Application/Handlers/{Feature}/Get{Entity}PagedHandler.cs`

```csharp
using MediatR;
using {ServiceName}.Domain.Interfaces;
using {ServiceName}.Application.DTOs;
using {ServiceName}.Application.Queries.{Feature};

namespace {ServiceName}.Application.Handlers.{Feature};

public class Get{Entity}PagedHandler : IRequestHandler<Get{Entity}PagedQuery, PagedResult<{Entity}Dto>>
{
    private readonly I{Entity}Repository _repository;
    private readonly ILogger<Get{Entity}PagedHandler> _logger;

    public Get{Entity}PagedHandler(
        I{Entity}Repository repository,
        ILogger<Get{Entity}PagedHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<PagedResult<{Entity}Dto>> Handle(
        Get{Entity}PagedQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Fetching paged {Entities}: Page {Page}, Size {Size}",
            request.PageNumber,
            request.PageSize);

        var (entities, totalCount) = await _repository.GetPagedAsync(
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            searchTerm: request.SearchTerm,
            status: request.Status,
            cancellationToken: cancellationToken);

        var dtos = entities.Select(e => new {Entity}Dto
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Status = e.Status.ToString(),
            CreatedAt = e.CreatedAt
        }).ToList();

        return new PagedResult<{Entity}Dto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
```

### Update Handler Pattern

**File:** `{ServiceName}.Application/Handlers/{Feature}/Update{Entity}Handler.cs`

```csharp
using MediatR;
using {ServiceName}.Domain.Interfaces;
using {ServiceName}.Application.DTOs;
using {ServiceName}.Application.Commands.{Feature};

namespace {ServiceName}.Application.Handlers.{Feature};

public class Update{Entity}Handler : IRequestHandler<Update{Entity}Command, {Entity}Dto>
{
    private readonly I{Entity}Repository _repository;
    private readonly ILogger<Update{Entity}Handler> _logger;

    public Update{Entity}Handler(
        I{Entity}Repository repository,
        ILogger<Update{Entity}Handler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<{Entity}Dto> Handle(
        Update{Entity}Command request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating {Entity} with ID: {Id}", request.Id);

        // 1. Fetch existing entity
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("{Entity} with ID {Id} not found", request.Id);
            throw new NotFoundException($"{Entity} with ID {request.Id} not found");
        }

        // 2. Update properties
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.UpdatedAt = DateTime.UtcNow;

        // 3. Persist changes
        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("Successfully updated {Entity} with ID: {Id}", entity.Id);

        // 4. Return updated DTO
        return new {Entity}Dto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Status = entity.Status.ToString(),
            UpdatedAt = entity.UpdatedAt
        };
    }
}
```

### Delete Handler Pattern

**File:** `{ServiceName}.Application/Handlers/{Feature}/Delete{Entity}Handler.cs`

```csharp
using MediatR;
using {ServiceName}.Domain.Interfaces;
using {ServiceName}.Application.Commands.{Feature};

namespace {ServiceName}.Application.Handlers.{Feature};

public class Delete{Entity}Handler : IRequestHandler<Delete{Entity}Command, Unit>
{
    private readonly I{Entity}Repository _repository;
    private readonly ILogger<Delete{Entity}Handler> _logger;

    public Delete{Entity}Handler(
        I{Entity}Repository repository,
        ILogger<Delete{Entity}Handler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        Delete{Entity}Command request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting {Entity} with ID: {Id}", request.Id);

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("{Entity} with ID {Id} not found", request.Id);
            throw new NotFoundException($"{Entity} with ID {request.Id} not found");
        }

        await _repository.DeleteAsync(request.Id, cancellationToken);

        _logger.LogInformation("Successfully deleted {Entity} with ID: {Id}", request.Id);

        return Unit.Value;
    }
}
```

---

## API Endpoint Patterns

### Complete Endpoint Group Definition

**File:** `{ServiceName}.API/Endpoints/{Entity}Endpoints.cs`

```csharp
using MediatR;
using {ServiceName}.Application.Commands.{Feature};
using {ServiceName}.Application.Queries.{Feature};
using {ServiceName}.Application.DTOs;

namespace {ServiceName}.API.Endpoints;

public static class {Entity}Endpoints
{
    public static void Map{Entity}Endpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/{entities}")
            .WithTags("{Entities}")
            .RequireAuthorization(); // Require authentication

        // GET /api/{entities} - Get all (paginated)
        group.MapGet("/", GetAll{Entities})
            .WithName("GetAll{Entities}")
            .WithDescription("Get all {entities} with pagination and filtering")
            .Produces<PagedResult<{Entity}Dto>>(200)
            .ProducesValidationProblem()
            .WithOpenApi();

        // GET /api/{entities}/{id} - Get by ID
        group.MapGet("/{id:int}", Get{Entity}ById)
            .WithName("Get{Entity}ById")
            .WithDescription("Get a specific {entity} by ID")
            .Produces<{Entity}Dto>(200)
            .Produces(404)
            .WithOpenApi();

        // POST /api/{entities} - Create
        group.MapPost("/", Create{Entity})
            .WithName("Create{Entity}")
            .WithDescription("Create a new {entity}")
            .Produces<{Entity}Dto>(201)
            .ProducesValidationProblem()
            .WithOpenApi();

        // PUT /api/{entities}/{id} - Update
        group.MapPut("/{id:int}", Update{Entity})
            .WithName("Update{Entity}")
            .WithDescription("Update an existing {entity}")
            .Produces<{Entity}Dto>(200)
            .Produces(404)
            .ProducesValidationProblem()
            .WithOpenApi();

        // DELETE /api/{entities}/{id} - Delete
        group.MapDelete("/{id:int}", Delete{Entity})
            .WithName("Delete{Entity}")
            .WithDescription("Delete a {entity}")
            .Produces(204)
            .Produces(404)
            .WithOpenApi();

        // Admin endpoints (require Admin role)
        var adminGroup = routes.MapGroup("/api/admin/{entities}")
            .WithTags("Admin {Entities}")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminGroup.MapGet("/all", GetAll{Entities}Admin)
            .WithName("GetAll{Entities}Admin")
            .WithDescription("Admin: Get all {entities} without pagination")
            .Produces<IEnumerable<{Entity}Dto>>(200);
    }

    // ============================================
    // Handler Methods
    // ============================================

    private static async Task<IResult> GetAll{Entities}(
        [AsParameters] Get{Entity}PagedQuery query,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Get{Entity}ById(
        int id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new Get{Entity}ByIdQuery(id);
        var result = await mediator.Send(query, ct);

        return result != null
            ? Results.Ok(result)
            : Results.NotFound(new { Message = $"{Entity} with ID {id} not found" });
    }

    private static async Task<IResult> Create{Entity}(
        Create{Entity}Command command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Results.Created($"/api/{entities}/{result.Id}", result);
    }

    private static async Task<IResult> Update{Entity}(
        int id,
        Update{Entity}Command command,
        IMediator mediator,
        CancellationToken ct)
    {
        if (id != command.Id)
            return Results.BadRequest(new { Message = "ID mismatch" });

        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Delete{Entity}(
        int id,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new Delete{Entity}Command(id), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAll{Entities}Admin(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetAll{Entity}Query();
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }
}
```

### Register Endpoints in Program.cs

**File:** `{ServiceName}.API/Program.cs`

```csharp
using {ServiceName}.API.Endpoints;

var app = builder.Build();

// ============================================
// API Endpoints
// ============================================
app.Map{Entity}Endpoints();
// Add more endpoint groups as needed

app.Run();
```

---

## Error Handling & Validation

### Global Exception Handler

**File:** `{ServiceName}.API/Program.cs`

```csharp
using Microsoft.AspNetCore.Diagnostics;
using IhsanDev.Shared.Application.Exceptions;

var app = builder.Build();

// ============================================
// Global Exception Handler
// ============================================
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";

        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandlerFeature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception occurred");

        var (statusCode, message) = exception switch
        {
            NotFoundException => (404, exception.Message),
            ValidationException => (400, exception.Message),
            UnauthorizedException => (401, "Unauthorized access"),
            ForbiddenException => (403, "Forbidden"),
            DuplicateEntityException => (409, exception.Message),
            _ => (500, "An internal server error occurred")
        };

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(new
        {
            StatusCode = statusCode,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    });
});
```

### Custom Exceptions

**File:** `{ServiceName}.Application/Exceptions/{Entity}Exceptions.cs`

```csharp
using IhsanDev.Shared.Application.Exceptions;

namespace {ServiceName}.Application.Exceptions;

public class {Entity}NotFoundException : NotFoundException
{
    public {Entity}NotFoundException(int id)
        : base($"{Entity} with ID {id} was not found")
    {
    }
}

public class {Entity}AlreadyExistsException : DuplicateEntityException
{
    public {Entity}AlreadyExistsException(string name)
        : base($"{Entity} with name '{name}' already exists")
    {
    }
}

public class {Entity}ValidationException : ValidationException
{
    public {Entity}ValidationException(string message)
        : base(message)
    {
    }
}
```

### FluentValidation Behavior

**File:** `{ServiceName}.Application/Behaviors/ValidationBehavior.cs`

```csharp
using FluentValidation;
using MediatR;

namespace {ServiceName}.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
        {
            _logger.LogWarning(
                "Validation failed for {RequestType}: {Errors}",
                typeof(TRequest).Name,
                string.Join(", ", failures.Select(f => f.ErrorMessage)));

            throw new FluentValidation.ValidationException(failures);
        }

        return await next();
    }
}
```

### Register Validation Behavior

**File:** `{ServiceName}.API/Program.cs`

```csharp
using FluentValidation;
using {ServiceName}.Application.Behaviors;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// MediatR with Behaviors
// ============================================
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Create{Entity}Command).Assembly);

    // Add validation pipeline behavior
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// ============================================
// FluentValidation
// ============================================
builder.Services.AddValidatorsFromAssembly(typeof(Create{Entity}Validator).Assembly);
```

---

## Testing Strategy

### Testing Pyramid

```
                    ┌─────────┐
                    │  E2E    │ ← 5% (Manual/Automated UI tests)
                    └─────────┘
                ┌───────────────┐
                │ Integration   │ ← 35% (API endpoint tests)
                └───────────────┘
            ┌─────────────────────┐
            │   Unit Tests        │ ← 60% (Handler, validator tests)
            └─────────────────────┘
```

**Testing Distribution:**

- **60% Unit Tests**: Handlers, validators, domain logic
- **35% Integration Tests**: API endpoints, database integration
- **5% E2E Tests**: Full user flows (manual or automated)

### What to Test

**Unit Tests:**

- ✅ Command/Query handlers
- ✅ FluentValidation validators
- ✅ Domain entity business logic
- ✅ Repository interface contracts (mocked)
- ✅ Custom exceptions

**Integration Tests:**

- ✅ API endpoints (full request/response)
- ✅ Database operations (actual DbContext)
- ✅ Authentication/Authorization
- ✅ Multi-tenancy behavior
- ✅ Error handling
- ✅ Validation pipeline

**E2E Tests (Optional):**

- ✅ Complete user workflows
- ✅ Multi-service interactions
- ✅ UI-to-API flows

---

## Integration Testing

### Test Factory Setup

**File:** `{ServiceName}.API.Tests/Infrastructure/CustomWebApplicationFactory.cs`

```csharp
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using {ServiceName}.Infrastructure.Persistence;

namespace {ServiceName}.API.Tests.Infrastructure;

public class CustomWebApplicationFactory :
    IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();

        // Override configuration for tests
        config["MultiTenancy:Enabled"] = "false"; // Disable for tests
        config["Jwt:Secret"] = "test-secret-key-minimum-32-characters-long-for-testing";
        config["Jwt:Issuer"] = "TestIssuer";
        config["Jwt:Audience"] = "TestAudience";
        config["DatabaseSettings:Provider"] = "Sqlite"; // Use SQLite for tests

        return config;
    }

    protected override void SeedTestData<TDbContext>(TDbContext context)
    {
        if (context is {ServiceName}DbContext dbContext)
        {
            // Seed test data
            var testEntity = new {Entity}
            {
                Name = "Test {Entity}",
                Description = "Test Description",
                Status = {Entity}Status.Active,
                UserId = 1,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.{Entities}.Add(testEntity);
            dbContext.SaveChanges();
        }
    }
}
```

### Test Base Class

**File:** `{ServiceName}.API.Tests/Infrastructure/IntegrationTestBase.cs`

```csharp
using IhsanDev.Shared.Testing.Infrastructure;
using {ServiceName}.Infrastructure.Persistence;

namespace {ServiceName}.API.Tests.Infrastructure;

public abstract class IntegrationTestBase :
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<{ServiceName}DbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    // Helper: Create test entity
    protected async Task<{Entity}> CreateTest{Entity}Async(
        string name = "Test {Entity}",
        int userId = 1)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var entity = new {Entity}
            {
                Name = name,
                Description = "Test Description",
                Status = {Entity}Status.Active,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            context.{Entities}.Add(entity);
            await context.SaveChangesAsync();
            return entity;
        });
    }
}
```

### Complete Endpoint Tests

**File:** `{ServiceName}.API.Tests/Endpoints/{Entity}EndpointsTests.cs`

```csharp
using FluentAssertions;
using {ServiceName}.Application.Commands.{Feature};
using {ServiceName}.Application.Queries.{Feature};
using {ServiceName}.Application.DTOs;
using {ServiceName}.API.Tests.Infrastructure;

namespace {ServiceName}.API.Tests.Endpoints;

[Collection("Sequential")]
public class {Entity}EndpointsTests : IntegrationTestBase
{
    public {Entity}EndpointsTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetAll{Entities}_ShouldReturnPagedResults()
    {
        // Arrange
        await CreateTest{Entity}Async("Entity 1");
        await CreateTest{Entity}Async("Entity 2");
        await CreateTest{Entity}Async("Entity 3");

        var query = new Get{Entity}PagedQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterOrEqualTo(3);
        result.TotalCount.Should().BeGreaterOrEqualTo(3);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Get{Entity}ById_WithValidId_ShouldReturnEntity()
    {
        // Arrange
        var created = await CreateTest{Entity}Async();
        var query = new Get{Entity}ByIdQuery(created.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Name.Should().Be(created.Name);
    }

    [Fact]
    public async Task Get{Entity}ById_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var query = new Get{Entity}ByIdQuery(99999);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Create{Entity}_WithValidData_ShouldSucceed()
    {
        // Arrange
        var command = new Create{Entity}Command(
            Name: "New Test Entity",
            Description: "Test Description",
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("New Test Entity");
        result.Status.Should().Be({Entity}Status.Active.ToString());
    }

    [Fact]
    public async Task Create{Entity}_WithDuplicateName_ShouldThrow()
    {
        // Arrange
        await CreateTest{Entity}Async("Duplicate Entity");

        var command = new Create{Entity}Command(
            Name: "Duplicate Entity",
            Description: "Test",
            UserId: 1
        );

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateEntityException>(
            async () => await SendAsync(command));
    }

    [Fact]
    public async Task Update{Entity}_WithValidData_ShouldSucceed()
    {
        // Arrange
        var created = await CreateTest{Entity}Async();

        var command = new Update{Entity}Command(
            Id: created.Id,
            Name: "Updated Name",
            Description: "Updated Description"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(created.Id);
        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task Update{Entity}_WithInvalidId_ShouldThrow()
    {
        // Arrange
        var command = new Update{Entity}Command(
            Id: 99999,
            Name: "Updated",
            Description: "Test"
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command));
    }

    [Fact]
    public async Task Delete{Entity}_WithValidId_ShouldSucceed()
    {
        // Arrange
        var created = await CreateTest{Entity}Async();
        var command = new Delete{Entity}Command(created.Id);

        // Act
        await SendAsync(command);

        // Assert
        var deleted = await ExecuteDbContextAsync(async context =>
            await context.{Entities}.FindAsync(created.Id));

        deleted.Should().BeNull();
    }

    [Fact]
    public async Task Delete{Entity}_WithInvalidId_ShouldThrow()
    {
        // Arrange
        var command = new Delete{Entity}Command(99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(command));
    }
}
```

### Multi-Tenancy Integration Tests

**File:** `{ServiceName}.API.Tests/Endpoints/{Entity}MultiTenancyTests.cs`

```csharp
using FluentAssertions;
using IhsanDev.Shared.Testing.Helpers;
using {ServiceName}.API.Tests.Infrastructure;

namespace {ServiceName}.API.Tests.Endpoints;

[Collection("Sequential")]
public class {Entity}MultiTenancyTests : IntegrationTestBase
{
    public {Entity}MultiTenancyTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Create{Entity}_WithTenantId_ShouldIsolateTenantData()
    {
        // Arrange
        var tenant1Id = TenantTestHelper.GenerateUniqueTenantId("service");
        var tenant2Id = TenantTestHelper.GenerateUniqueTenantId("service");

        var command1 = new Create{Entity}Command(
            Name: "Tenant 1 Entity",
            Description: "Test",
            UserId: 1,
            TenantId: tenant1Id
        );

        var command2 = new Create{Entity}Command(
            Name: "Tenant 2 Entity",
            Description: "Test",
            UserId: 1,
            TenantId: tenant2Id
        );

        // Act
        var result1 = await SendAsync(command1);
        var result2 = await SendAsync(command2);

        // Assert
        result1.TenantId.Should().Be(tenant1Id);
        result2.TenantId.Should().Be(tenant2Id);

        // Verify isolation
        var tenant1Entities = await ExecuteDbContextAsync(async context =>
            await context.{Entities}
                .Where(e => e.TenantId == tenant1Id)
                .ToListAsync());

        var tenant2Entities = await ExecuteDbContextAsync(async context =>
            await context.{Entities}
                .Where(e => e.TenantId == tenant2Id)
                .ToListAsync());

        tenant1Entities.Should().HaveCount(1);
        tenant2Entities.Should().HaveCount(1);
        tenant1Entities.First().TenantId.Should().Be(tenant1Id);
        tenant2Entities.First().TenantId.Should().Be(tenant2Id);
    }
}
```

---

## Unit Testing

### Handler Unit Tests

**File:** `{ServiceName}.Application.Tests/Handlers/Create{Entity}HandlerTests.cs`

```csharp
using Moq;
using FluentAssertions;
using {ServiceName}.Application.Handlers.{Feature};
using {ServiceName}.Application.Commands.{Feature};
using {ServiceName}.Domain.Interfaces;

namespace {ServiceName}.Application.Tests.Handlers;

public class Create{Entity}HandlerTests
{
    private readonly Mock<I{Entity}Repository> _mockRepository;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<Create{Entity}Handler>> _mockLogger;

    public Create{Entity}HandlerTests()
    {
        _mockRepository = new Mock<I{Entity}Repository>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockTenantContext = new Mock<ITenantContext>();
        _mockLogger = new Mock<ILogger<Create{Entity}Handler>>();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateEntity()
    {
        // Arrange
        var command = new Create{Entity}Command(
            Name: "Test Entity",
            Description: "Test Description",
            UserId: 1
        );

        var expectedEntity = new {Entity}
        {
            Id = 1,
            Name = command.Name,
            Description = command.Description,
            UserId = command.UserId,
            Status = {Entity}Status.Active
        };

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<{Entity}>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntity);

        _mockTenantContext.Setup(t => t.HasTenant).Returns(false);

        var handler = new Create{Entity}Handler(
            _mockRepository.Object,
            _mockHttpContextAccessor.Object,
            _mockTenantContext.Object,
            _mockLogger.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Test Entity");
        result.Status.Should().Be({Entity}Status.Active.ToString());

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<{Entity}>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithTenantContext_ShouldIncludeTenantId()
    {
        // Arrange
        var tenantId = "test-tenant-123";
        var command = new Create{Entity}Command(
            Name: "Test Entity",
            Description: "Test",
            UserId: 1
        );

        var tenantInfo = new TenantInfo
        {
            TenantId = tenantId,
            TenantName = "Test Tenant"
        };

        _mockTenantContext.Setup(t => t.HasTenant).Returns(true);
        _mockTenantContext.Setup(t => t.CurrentTenant).Returns(tenantInfo);

        {Entity} capturedEntity = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<{Entity}>(), It.IsAny<CancellationToken>()))
            .Callback<{Entity}, CancellationToken>((entity, ct) => capturedEntity = entity)
            .ReturnsAsync((_{Entity} e, CancellationToken _) => e);

        var handler = new Create{Entity}Handler(
            _mockRepository.Object,
            _mockHttpContextAccessor.Object,
            _mockTenantContext.Object,
            _mockLogger.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        capturedEntity.Should().NotBeNull();
        capturedEntity!.TenantId.Should().Be(tenantId);
    }
}
```

### Validator Unit Tests

**File:** `{ServiceName}.Application.Tests/Validators/Create{Entity}ValidatorTests.cs`

```csharp
using FluentValidation.TestHelper;
using {ServiceName}.Application.Validators;
using {ServiceName}.Application.Commands.{Feature};

namespace {ServiceName}.Application.Tests.Validators;

public class Create{Entity}ValidatorTests
{
    private readonly Create{Entity}Validator _validator;

    public Create{Entity}ValidatorTests()
    {
        _validator = new Create{Entity}Validator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new Create{Entity}Command(
            Name: "Valid Name",
            Description: "Valid Description",
            UserId: 1
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldHaveError()
    {
        // Arrange
        var command = new Create{Entity}Command(
            Name: "",
            Description: "Valid Description",
            UserId: 1
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidUserId_ShouldHaveError(int userId)
    {
        // Arrange
        var command = new Create{Entity}Command(
            Name: "Valid Name",
            Description: "Valid Description",
            UserId: userId
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void Validate_WithNameTooLong_ShouldHaveError()
    {
        // Arrange
        var longName = new string('a', 201); // Exceeds 200 char limit
        var command = new Create{Entity}Command(
            Name: longName,
            Description: "Valid Description",
            UserId: 1
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }
}
```

---

## Performance Testing

### Load Testing Setup

**File:** `{ServiceName}.LoadTests/LoadTest.cs`

```csharp
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace {ServiceName}.LoadTests;

public class {Entity}LoadTests
{
    [Fact]
    public void LoadTest_Create{Entity}_ShouldHandleLoad()
    {
        var httpFactory = HttpClientFactory.Create();

        var scenario = Scenario.Create("create_{entity}_load_test", async context =>
        {
            var request = Http.CreateRequest("POST", "https://localhost:5001/api/{entities}")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Authorization", "Bearer test-token")
                .WithBody(new StringContent(
                    $$"""
                    {
                        "name": "Load Test Entity {{context.ScenarioInfo.ThreadId}}",
                        "description": "Performance test",
                        "userId": 1
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"));

            var response = await Http.Send(httpFactory, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert performance metrics
        var okCount = stats.ScenarioStats[0].Ok.Request.Count;
        var failCount = stats.ScenarioStats[0].Fail.Request.Count;
        var latencyP95 = stats.ScenarioStats[0].Ok.Latency.Percent95;

        okCount.Should().BeGreaterThan(2500); // ~83% success rate minimum
        failCount.Should().BeLessThan(500);
        latencyP95.Should().BeLessThan(1000); // < 1 second P95
    }
}
```

---

## API Documentation

### Swagger/OpenAPI Configuration

**File:** `{ServiceName}.API/Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// ============================================
// Swagger/OpenAPI
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "{ServiceName} API",
        Version = "v1",
        Description = "API for managing {entities}",
        Contact = new OpenApiContact
        {
            Name = "Your Team",
            Email = "team@example.com"
        }
    });

    // Add JWT authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ============================================
// Swagger UI (Development Only)
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "{ServiceName} API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });
}

app.Run();
```

### API Documentation File

**File:** `{ServiceName}.API/README.md`

````markdown
# {ServiceName} API Documentation

## Overview

This API provides CRUD operations for managing {entities}.

## Base URL

- Development: `https://localhost:5001`
- Production: `https://api.yourapp.com`

## Authentication

All endpoints require JWT authentication (except public endpoints).

### Get JWT Token

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "Password123!"
  }'
```
````

Response:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "expiresIn": 3600
}
```

## Endpoints

### GET /api/{entities}

Get all {entities} (paginated).

**Query Parameters:**

- `pageNumber` (int, optional): Page number (default: 1)
- `pageSize` (int, optional): Page size (default: 10)
- `searchTerm` (string, optional): Search term for filtering
- `status` (string, optional): Filter by status

**Example:**

```bash
curl -X GET "https://localhost:5001/api/{entities}?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer <token>"
```

**Response:**

```json
{
  "items": [
    {
      "id": 1,
      "name": "Example Entity",
      "description": "Description",
      "status": "Active",
      "createdAt": "2025-01-15T10:00:00Z"
    }
  ],
  "totalCount": 100,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 10
}
```

### POST /api/{entities}

Create a new {entity}.

**Request Body:**

```json
{
  "name": "New Entity",
  "description": "Description",
  "userId": 1
}
```

**Response:** 201 Created

```json
{
  "id": 1,
  "name": "New Entity",
  "description": "Description",
  "status": "Active",
  "userId": 1,
  "createdAt": "2025-01-15T10:00:00Z"
}
```

## Error Responses

### 400 Bad Request

```json
{
  "statusCode": 400,
  "message": "Validation failed",
  "errors": {
    "name": ["Name is required"]
  }
}
```

### 401 Unauthorized

```json
{
  "statusCode": 401,
  "message": "Unauthorized access"
}
```

### 404 Not Found

```json
{
  "statusCode": 404,
  "message": "{Entity} with ID 123 not found"
}
```

### 500 Internal Server Error

```json
{
  "statusCode": 500,
  "message": "An internal server error occurred"
}
```

````

---

## Deployment Guide

### Docker Configuration

**File:** `{ServiceName}.API/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/Services/{ServiceName}/{ServiceName}.API/{ServiceName}.API.csproj", "Services/{ServiceName}/{ServiceName}.API/"]
COPY ["src/Services/{ServiceName}/{ServiceName}.Application/{ServiceName}.Application.csproj", "Services/{ServiceName}/{ServiceName}.Application/"]
COPY ["src/Services/{ServiceName}/{ServiceName}.Domain/{ServiceName}.Domain.csproj", "Services/{ServiceName}/{ServiceName}.Domain/"]
COPY ["src/Services/{ServiceName}/{ServiceName}.Infrastructure/{ServiceName}.Infrastructure.csproj", "Services/{ServiceName}/{ServiceName}.Infrastructure/"]
COPY ["src/Shared/IhsanDev.Shared.Kernel/IhsanDev.Shared.Kernel.csproj", "Shared/IhsanDev.Shared.Kernel/"]
COPY ["src/Shared/IhsanDev.Shared.Application/IhsanDev.Shared.Application.csproj", "Shared/IhsanDev.Shared.Application/"]
COPY ["src/Shared/IhsanDev.Shared.Infrastructure/IhsanDev.Shared.Infrastructure.csproj", "Shared/IhsanDev.Shared.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "Services/{ServiceName}/{ServiceName}.API/{ServiceName}.API.csproj"

# Copy source code
COPY src/ .

# Build
WORKDIR "/src/Services/{ServiceName}/{ServiceName}.API"
RUN dotnet build "{ServiceName}.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "{ServiceName}.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "{ServiceName}.API.dll"]
````

### Docker Compose

**File:** `docker-compose.yml` (add to existing)

```yaml
services:
  {servicename}:
    image: {servicename}:latest
    build:
      context: .
      dockerfile: src/Services/{ServiceName}/{ServiceName}.API/Dockerfile
    ports:
      - "5010:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DatabaseSettings__Provider=PostgreSql
      - DatabaseSettings__ConnectionString=Host=postgres;Database={ServiceName}Db;Username=postgres;Password=yourpassword
      - Jwt__Secret=${JWT_SECRET}
      - Jwt__Issuer=IdentityService
      - Jwt__Audience=MicroservicesApp
      - MultiTenancy__Enabled=true
      - MultiTenancy__TenantServiceUrl=http://tenant:80
      - Redis__Enabled=true
      - Redis__ConnectionString=redis:6379
    depends_on:
      - postgres
      - redis
      - tenant
    networks:
      - microservices-network

networks:
  microservices-network:
    driver: bridge
```

### Environment Variables

**File:** `.env` (for Docker Compose)

```env
# JWT Configuration (MUST be same across all services)
JWT_SECRET=your-super-secret-jwt-key-minimum-32-characters-long-for-production

# Database Configuration
POSTGRES_PASSWORD=yourpassword
DATABASE_CONNECTION_STRING=Host=postgres;Database={ServiceName}Db;Username=postgres;Password=yourpassword

# Redis Configuration
REDIS_CONNECTION_STRING=redis:6379

# Service URLs
TENANT_SERVICE_URL=http://tenant:80
IDENTITY_SERVICE_URL=http://identity:80
```

---

## Monitoring & Logging

### Structured Logging Configuration

**File:** `{ServiceName}.API/Program.cs`

```csharp
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Serilog Configuration
// ============================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "{ServiceName}")
    .Enrich.WithMachineName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// Log startup
Log.Information("{ServiceName} starting up...");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "{ServiceName} terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

### Health Checks

**File:** `{ServiceName}.API/Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// ============================================
// Health Checks
// ============================================
builder.Services.AddHealthChecks()
    .AddDbContextCheck<{ServiceName}DbContext>("database")
    .AddRedis(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379",
        name: "redis",
        tags: new[] { "cache" });

var app = builder.Build();

// ============================================
// Health Check Endpoints
// ============================================
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.Run();
```

---

## Production Checklist

### Pre-Deployment

**Security:**

- [ ] JWT secrets stored in environment variables (not appsettings.json)
- [ ] Database passwords in secure vault (Azure Key Vault, AWS Secrets Manager)
- [ ] HTTPS enforced (`app.UseHttpsRedirection()`)
- [ ] CORS configured for specific origins (not `AllowAll`)
- [ ] Service-to-service authentication enabled
- [ ] SQL injection protection (use parameterized queries - EF Core does this)

**Configuration:**

- [ ] `MultiTenancy:Enabled` set correctly (`true` or `false`)
- [ ] Database provider configured (`PostgreSql`, `SqlServer`, etc.)
- [ ] Redis enabled for production (`Redis:Enabled = true`)
- [ ] Logging level set to `Information` or `Warning` (not `Debug`)
- [ ] Health checks configured

**Database:**

- [ ] Migrations applied to all tenant databases
- [ ] Database backups configured
- [ ] Connection pooling enabled
- [ ] Database indexes created (check entity configurations)

**Performance:**

- [ ] Redis caching enabled
- [ ] Database connection string optimized
- [ ] Response compression enabled
- [ ] Static files compressed (if applicable)

**Monitoring:**

- [ ] Structured logging configured (Serilog)
- [ ] Application Insights or equivalent APM tool integrated
- [ ] Health check endpoints accessible (`/health`, `/health/ready`, `/health/live`)
- [ ] Alerts configured for critical errors

### Post-Deployment

**Verification:**

- [ ] Service accessible via load balancer/API gateway
- [ ] Authentication working (JWT validation)
- [ ] Multi-tenancy working (if enabled)
- [ ] Database connections successful
- [ ] Redis caching working
- [ ] Health checks returning 200 OK
- [ ] Logs being written correctly
- [ ] Metrics being collected

**Load Testing:**

- [ ] Run load tests to verify performance
- [ ] Monitor CPU/memory usage under load
- [ ] Verify database connection pooling
- [ ] Check for memory leaks

**Documentation:**

- [ ] API documentation updated (Swagger/OpenAPI)
- [ ] README updated with deployment instructions
- [ ] Runbook created for operations team
- [ ] Architecture diagrams updated

---

## Stage 3 Checklist

Before considering your service complete, ensure:

### Implementation

- [ ] All CQRS handlers implemented (Create, Read, Update, Delete)
- [ ] All API endpoints defined and documented
- [ ] Error handling configured globally
- [ ] FluentValidation validators created
- [ ] Validation pipeline behavior registered
- [ ] Logging configured in all handlers
- [ ] Custom exceptions defined

### Testing

- [ ] Integration tests written for all endpoints (minimum 80% coverage)
- [ ] Unit tests written for handlers (minimum 80% coverage)
- [ ] Validator unit tests written
- [ ] Multi-tenancy tests written (if applicable)
- [ ] All tests passing
- [ ] Test coverage measured

### Documentation

- [ ] API documentation complete (Swagger/OpenAPI)
- [ ] README.md created with API usage examples
- [ ] Deployment guide documented
- [ ] Environment variables documented

### Deployment

- [ ] Dockerfile created and tested
- [ ] Docker Compose configuration added
- [ ] Environment variables configured
- [ ] Health checks working
- [ ] Logging working
- [ ] Service runs in container successfully

### Production Readiness

- [ ] All items in Production Checklist completed
- [ ] Load testing performed
- [ ] Security review completed
- [ ] Monitoring configured
- [ ] Alerts configured
- [ ] Runbook created

---

## Congratulations! 🎉

You've completed all 3 stages of new service creation:

1. ✅ **Stage 1**: Service Architecture & Structure
2. ✅ **Stage 2**: Configuration & Integration
3. ✅ **Stage 3**: Implementation, Testing & Deployment

Your new microservice is now **production-ready**!

---

## Additional Resources

### Related Documentation

- 📖 [00_START_HERE.md](00_START_HERE.md) - Complete documentation index
- 🏗️ [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Multi-tenant architecture
- 🔐 [SHARED_IDENTITY_SERVICE_GUIDE.md](SHARED_IDENTITY_SERVICE_GUIDE.md) - Authentication guide
- 🏢 [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Multi-tenancy deep dive
- 🧪 [SHARED_TESTING_FILES.md](SHARED_TESTING_FILES.md) - Testing infrastructure
- ⚡ [BOTTLENECKS_COMPLETION_SUMMARY.md](BOTTLENECKS_COMPLETION_SUMMARY.md) - Performance optimization

### Example Services

- **Identity Service**: `src/Services/Identity/` - Complete authentication implementation
- **Tenant Service**: `src/Services/Tenant/` - Multi-tenancy provider implementation
- **Notification Service**: `src/Services/Notification/` - Real-time notifications

### Support

For questions or issues:

- Check the documentation index: [00_START_HERE.md](00_START_HERE.md)
- Review existing service implementations
- Create a GitHub issue

---

**Built with ❤️ for Clean Architecture, CQRS & Microservices**

_Last Updated: January 2025_
