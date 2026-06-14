---
name: be-new-service
description: Step-by-step guided workflow for creating a new .NET 8 microservice from scratch in this project — database strategy selection, all four Clean Architecture layers (Domain, Application, Infrastructure, API), EF migrations, Program.cs pipeline, and solution registration. Use this whenever the user asks to add a new service, create a microservice, scaffold a service, or build a new backend module. Always invoke this skill before writing any service scaffold code.
---

# Creating a New Service Workflow

**Reference implementation:** `src/Services/Identity`

## 0. Choose a Database Strategy (DO THIS FIRST)

Read `.claude/instructions/database-strategy.instructions.md` and answer:

| Question | Strategy |
|---|---|
| System registry with no tenant data? | **A — Single Global DB** |
| Stores data per-tenant with full isolation? | **B — Per-Tenant DB** |
| Shared processing queue AND per-tenant history? | **C — Dual DB** |
| Global provider where tenants can override rows? | **D — Global + Discriminator Column** |

Reference implementations:
- Strategy A → `src/Services/Tenant/Tenant.Infrastructure/Persistence/TenantDbContext.cs`
- Strategy B → `src/Services/Identity/Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Strategy C → `src/Services/Notification/Notification.Infrastructure/Persistence/`
- Strategy D → `src/Services/Translation/Translation.Infrastructure/Persistence/TranslationDbContext.cs`

## 1. Folder Structure

```
{ServiceName}/
├── {ServiceName}.API/
├── {ServiceName}.Application/
├── {ServiceName}.Domain/
└── {ServiceName}.Infrastructure/
```

## 2. Domain Layer

### Entities

Inherit from `BaseEntity` (provides `Id`, `Created`, `CreatedBy`, `LastModified`, `LastModifiedBy`, `IsArchived`, `Status`):

```csharp
public class MyEntity : BaseEntity
{
    public string Name { get; set; }
}
```

### Repository Interfaces

Inherit from `IRepository<T>`:

```csharp
public interface IMyEntityRepository : IRepository<MyEntity>
{
    Task<MyEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
```

## 3. Application Layer

### DTOs

Inherit from `BaseDto`. Use static `MapFrom` — no AutoMapper:

```csharp
public class MyEntityDto : BaseDto
{
    public string Name { get; set; }

    public static MyEntityDto MapFrom(MyEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Created = entity.Created.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
```

### Commands and Validators

Commands: `record` implementing `IRequest<TResponse>`. Validators: inherit `LocalizedValidator<T>` — never hardcode strings.

```csharp
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

### Handlers

```csharp
public class CreateMyEntityHandler : IRequestHandler<CreateMyEntityCommand, MyEntityDto>
{
    private readonly IMyEntityRepository _repository;
    private readonly ILogger<CreateMyEntityHandler> _logger;

    public CreateMyEntityHandler(IMyEntityRepository repository, ILogger<CreateMyEntityHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<MyEntityDto> Handle(CreateMyEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = new MyEntity { Name = request.Name };
        await _repository.AddAsync(entity, cancellationToken);
        _logger.LogInformation("Created {Entity} Id {Id}", nameof(MyEntity), entity.Id);
        return MyEntityDto.MapFrom(entity);
    }
}
```

## 4. Infrastructure Layer

### DbContext

Use the exact pattern for the chosen strategy from `.claude/instructions/database-strategy.instructions.md`.

- Strategy A/D: no `ITenantContext`, options from DI
- Strategy B: `ITenantContext` in `OnConfiguring` with fallback to global DB
- Strategy C: two separate DbContext classes

### Repository Implementation

```csharp
public class MyEntityRepository : Repository<MyEntity>, IMyEntityRepository
{
    public MyEntityRepository(MyServiceDbContext context) : base(context) { }

    public async Task<MyEntity?> GetByNameAsync(string name, CancellationToken cancellationToken)
        => await _dbSet.FirstOrDefaultAsync(e => e.Name == name && !e.IsArchived, cancellationToken);
}
```

### DI Extensions

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

### Endpoints

```csharp
public static class EndpointMappingExtensions
{
    public static WebApplication MapMyEntityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/my-entities")
            .WithTags("My Entity Management")
            .RequireAuthorization();

        group.MapPost("/", MyEntityApiHandlers.Create)
            .WithName("CreateMyEntity")
            .Produces<MyEntityDto>(201)
            .AddEndpointFilter<ValidationFilter<CreateMyEntityCommand>>();

        return app;
    }
}
```

### Validation Filter

```csharp
public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
{
    public ValidationFilter(IValidator<T> validator) : base(validator) { }
}
```

### API Handlers

```csharp
public static class MyEntityApiHandlers
{
    public static async Task<IResult> Create(
        CreateMyEntityCommand command, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Results.Created($"/api/my-entities/{result.Id}", result);
    }
}
```

## 6. Program.cs Middleware Pipeline

Wire based on the strategy chosen in Step 0. Order is **critical** — see `.claude/instructions/database-strategy.instructions.md` for exact pipeline per strategy.

Always add observability:

```csharp
// DI
builder.Services.AddPlatformObservability(builder.Configuration, "MyServiceName");

// Before app.Run()
app.MapPrometheusScrapingEndpoint("/metrics");
```

And add to `appsettings.json`:
```json
"Observability": { "OtlpEndpoint": "http://localhost:4317" }
```

## 7. Register in Solution

Add 4 project entries to `MicroservicesArchitecture.sln`. Generate GUIDs with `New-Guid` in PowerShell (5 times for folder + 4 projects). Each GUID must also appear in `GlobalSection(ProjectConfigurationPlatforms)`.

## 8. EF Migrations

```powershell
cd MicroservicesArchitecture/src/Services/{SN}/{SN}.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../{SN}.API/{SN}.API.csproj --output-dir Migrations
```

## 9. Build Verification

```powershell
cd MicroservicesArchitecture
dotnet build src/Services/{SN}/{SN}.API/{SN}.API.csproj
```
