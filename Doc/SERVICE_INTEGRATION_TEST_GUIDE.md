# Service Integration Test Guide

**Purpose:** Step-by-step process for creating integration tests for any service or app in this repository.  
**Last Updated:** June 17, 2026

---

## Key Decision: MediatR Handler Testing, Not HTTP

All integration tests in this repository call **MediatR handlers directly** via `SendAsync()`.  
Do **not** use `HttpClient` / `client.PostAsJsonAsync()` / `GetFromJsonAsync()`.

**Why:** Calling handlers bypasses the HTTP pipeline entirely, making tests fast, deterministic, and
bug-free. This pattern was adopted to avoid `PipeWriter` serialisation issues in `WebApplicationFactory`
tests and remains the established convention for all services.

---

## 1. Create the Test Project

### Folder location

| Service type           | Test project path                       |
| ---------------------- | --------------------------------------- |
| `src/Services/{Name}/` | `src/Services/{Name}/{Name}.API.Tests/` |
| `src/Apps/{Name}/`     | `src/Apps/{Name}/{Name}.API.Tests/`     |

### 1a. Create `{Name}.API.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference the service's API project so Program is accessible -->
    <ProjectReference Include="..\{Name}.API\{Name}.API.csproj" />
    <!-- Reference the shared test library for the base classes -->
    <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Testing\IhsanDev.Shared.Testing.csproj" />
  </ItemGroup>
</Project>
```

> All package versions are managed centrally in `Directory.Packages.props` at the solution root.
> Do **not** add `Version=""` attributes — the central file provides them.

### 1b. Create `GlobalUsings.cs`

```csharp
global using Xunit;
global using FluentAssertions;
```

---

## 2. Create `appsettings.Test.json` in the API Project

### Why this file exists

`appsettings.json` in every service has its real database password replaced with the placeholder
`CHANGE_ME_DB_PASSWORD` so credentials are never committed in plaintext.
`appsettings.Test.json` provides the **real test credentials** only for local test runs without
polluting `appsettings.json`.

**Location:** `{ServiceName}.API/appsettings.Test.json` — same folder as `appsettings.json`.

The shared `CustomWebApplicationFactory` loads it automatically:

```csharp
config.AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false);
```

After loading it, the factory reads `DatabaseSettings:ConnectionString` and updates
`PostgreSqlConnectionString` if the value does **not** contain `CHANGE_ME`:

```csharp
var connStr = builtConfig["DatabaseSettings:ConnectionString"];
if (!string.IsNullOrEmpty(connStr) && !connStr.Contains("CHANGE_ME"))
    PostgreSqlConnectionString = connStr;
```

This means the real password flows into both `ConfigureDbContext<TDbContext>()` **and** any
Hangfire / DI-resolved service that calls `IConfiguration["DatabaseSettings:ConnectionString"]`
lazily at resolve time.

---

### Minimal file template

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database={service}_test;Username=postgres;Password=your_real_password;Minimum Pool Size=5;Maximum Pool Size=50;Connection Idle Lifetime=300;Connection Pruning Interval=10;Pooling=true;"
  },
  "Redis": {
    "Enabled": false
  }
}
```

---

### Configuration load order (highest priority wins)

| Priority | Source | Notes |
| -------- | ------ | ----- |
| 1 (highest) | `GetTestConfiguration()` in-memory dict | Always wins — set JWT, `MultiTenancy:Enabled`, `Logging:FilePath` here |
| 2 | `appsettings.Test.json` | Real DB credentials, Redis toggle |
| 3 | `appsettings.Testing.json` | Convention-based; loaded when `UseEnvironment("Testing")` is set |
| 4 (lowest) | `appsettings.json` | Has `CHANGE_ME_DB_PASSWORD` placeholder |

---

### Available options

Every key in `appsettings.json` can be overridden. Keys typically used in `appsettings.Test.json`:

| Key | Purpose | Typical test value |
| --- | ------- | ------------------ |
| `DatabaseSettings:Provider` | DB engine | `"PostgreSql"` |
| `DatabaseSettings:ConnectionString` | Real test DB password | `"...;Password=real_password;"` |
| `Redis:Enabled` | Enable/disable Redis in-process | `false` for most services |
| `Hangfire:Dashboard:Username` | Hangfire Basic Auth | Any string — dashboard is not hit by tests |
| `Hangfire:Dashboard:Password` | Hangfire Basic Auth | Any string |

Keys that belong in `GetTestConfiguration()` override instead (do NOT put these in `appsettings.Test.json`):

| Key | Reason it belongs in `GetTestConfiguration()` |
| --- | --------------------------------------------- |
| `Logging:FilePath` | Must point to `Path.GetTempPath()` — base factory sets this automatically |
| `MultiTenancy:Enabled` | Service-specific; set to `"false"` in the service `CustomWebApplicationFactory` |
| `Jwt:Secret` | Service-specific; set in the service `CustomWebApplicationFactory` |
| `RateLimiting:Enabled` | Service-specific; set to `"false"` in the service `CustomWebApplicationFactory` |

---

### Redis handling

When `Redis:Enabled` is `true` in `appsettings.Test.json`, the shared factory **rejects the test run
immediately** if Redis is not reachable on `localhost:6379`:

```
InvalidOperationException: Redis is required for this service's tests but Redis is not running
on localhost:6379. Start Redis before running tests.
```

Set `Redis:Enabled: false` to skip Redis entirely — the factory replaces the real
`IDistributedCache` with `MemoryDistributedCache` and registers an in-memory `ICacheService` stub.

`run-all-tests.mjs` starts Redis automatically before any test suite begins, so this error only
appears when running tests manually without Redis.

---

### The Hangfire timing problem (and fix)

Hangfire calls `services.AddHangfire(config => ...)` during `Program.cs` startup — **before**
`WebApplicationFactory.ConfigureAppConfiguration` callbacks run. If the connection string is
captured directly from `IConfiguration` at that point, it still has `CHANGE_ME_DB_PASSWORD`.

**Fix:** use the `(IServiceProvider, IGlobalConfiguration)` overload. This defers the
connection-string read to DI resolve time, which happens after all configuration overrides
(including `appsettings.Test.json`) have been applied:

```csharp
// ✅ Correct — lazy read at DI resolve time (after all config overrides)
services.AddHangfire((sp, config) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()
        ["DatabaseSettings:ConnectionString"]
        ?? throw new InvalidOperationException("DatabaseSettings:ConnectionString not configured");

    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(
            options => options.UseNpgsqlConnection(connectionString),
            new PostgreSqlStorageOptions { SchemaName = "hangfire_{service}" });
});

// ❌ Wrong — captures IConfiguration at registration time before test overrides apply
services.AddHangfire(config =>
{
    config.UsePostgreSqlStorage(
        options => options.UseNpgsqlConnection(configuration["DatabaseSettings:ConnectionString"]),
        ...);
});
```

Services that require this pattern: **Tenant, Category, FileManager, Notification**.

---

### Logging path timing problem (and fix)

The same early-capture issue applies to `Logging:FilePath`. `appsettings.json` often contains a
placeholder path like `C:\Users\YOUR_USERNAME\...` that resolves to a non-writable path.

The shared `CustomWebApplicationFactory.GetTestConfiguration()` already overrides `Logging:FilePath`
to `Path.GetTempPath() + "MicroservicesTestLogs"`. For this to work, `LoggingExtensions` must
read the path **lazily** inside the factory lambda instead of at registration time:

```csharp
// ✅ Correct — reads path lazily inside the factory lambda
services.AddSingleton<ILoggerManager>(serviceProvider =>
{
    var conf = serviceProvider.GetRequiredService<IConfiguration>();
    var logsPath = conf["Logging:FilePath"] ?? "Logs";
    return new LoggerManager(logger, logsPath);
});

// ❌ Wrong — captures configuration["Logging:FilePath"] at registration time
var logsPath = configuration["Logging:FilePath"] ?? "Logs";
services.AddSingleton<ILoggerManager>(new LoggerManager(logger, logsPath));
```

---

## 3. Create the Infrastructure Folder

Three files always go in `Infrastructure/`:

### 2a. `Infrastructure/SequentialCollectionDefinition.cs`

Prevents parallel DB access between test classes.

```csharp
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollectionDefinition { }
```

### 2b. `Infrastructure/CustomWebApplicationFactory.cs`

Inherits the shared factory and overrides configuration for this service.

```csharp
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using {Name}.Infrastructure.Data; // your DbContext namespace

public class CustomWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        UsePostgreSQL = true;
        PostgreSqlConnectionString =
            "Host=localhost;Port=5432;Database={name}_testdb;Username=postgres;Password=CHANGE_ME_DB_PASSWORD";
    }

    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();

        // JWT (required by all services)
        config["Jwt:Secret"]   = "TestSecretKeyForJWT_MustBe32CharactersLong!";
        config["Jwt:Issuer"]   = "TestIssuer";
        config["Jwt:Audience"] = "TestAudience";

        // Disable multi-tenancy so DbContext uses the direct connection string
        config["MultiTenancy:Enabled"] = "false";

        // Disable Redis (not running in tests)
        config["Redis:Enabled"] = "false";

        // Disable service-to-service calls (other services not running)
        config["ServiceCommunication:Enabled"] = "false";

        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // ── Remove background workers ──────────────────────────────
            // Any hosted service that calls an external service must be
            // removed here to prevent startup failures.
            RemoveHostedService<{Name}SomeBackgroundWorker>(services);

            // ── Replace external HTTP clients with mocks ───────────────
            // Example: replace IAiApiClient with a Moq mock
            // var descriptor = services.Single(d => d.ServiceType == typeof(IAiApiClient));
            // services.Remove(descriptor);
            // var mock = new Mock<IAiApiClient>();
            // mock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            //     .ReturnsAsync("mock-response");
            // services.AddSingleton(mock.Object);

            // ── Configure DbContext ────────────────────────────────────
            ConfigureDbContext<{Name}DbContext>(services);
        });

        // Create schema and run EF migrations on the test DB
        InitializeDatabase<{Name}DbContext>(builder);
    }

    // Helper used inside ConfigureWebHost to remove a hosted service by type
    private static void RemoveHostedService<TService>(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d =>
            d.ImplementationType == typeof(TService));
        if (descriptor != null)
            services.Remove(descriptor);
    }
}
```

**Checklist for `CustomWebApplicationFactory`:**

- [ ] Use PostgreSQL with a dedicated `{name}_testdb` database
- [ ] Override `GetTestConfiguration()` — set JWT, disable MultiTenancy/Redis/ServiceCommunication
- [ ] Remove every `IHostedService` that calls an external service
- [ ] Replace every HTTP client (e.g. `IAiApiClient`) with a Moq mock
- [ ] Call `ConfigureDbContext<TDbContext>()` so EF uses the test DB
- [ ] Call `InitializeDatabase<TDbContext>()` to create schema on first run

### 2c. `Infrastructure/IntegrationTestBase.cs`

Service-specific base class with entity-creation helpers.

```csharp
using IhsanDev.Shared.Testing.Infrastructure;
using {Name}.Infrastructure.Data;

public abstract class IntegrationTestBase :
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<{Name}DbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Entity creation helpers ────────────────────────────────────────────
    // Add helpers that insert entities directly into the DB (fast, no app layer)
    // and helpers that go through MediatR commands (exercises validation/handlers).

    // Direct DB insert — use when the entity is just setup data
    protected async Task<SomeEntity> CreateTestEntityAsync(string name = "Test Entity")
    {
        return await ExecuteDbContextAsync(async ctx =>
        {
            var entity = new SomeEntity { Name = name };
            ctx.SomeEntities.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        });
    }

    // Via MediatR — use when testing side-effects of creation
    protected async Task<SomeDto> CreateEntityViaCommandAsync(string name = "Test Entity")
    {
        return await SendAsync(new CreateSomeEntityCommand(Name: name));
    }
}
```

---

## 4. Create Test Files

### File naming

- One file per logical feature area: `{Feature}EndpointsTests.cs`
- All test classes must carry `[Collection("Sequential")]`

### Standard test class shell

```csharp
[Collection("Sequential")]
public class {Feature}EndpointsTests : IntegrationTestBase
{
    public {Feature}EndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // tests here
}
```

### Test patterns

#### Happy-path CRUD

```csharp
[Fact]
public async Task Create_WithValidData_ReturnsCreatedEntity()
{
    // Arrange
    var name = GenerateUniqueString("Entity");

    // Act
    var result = await SendAsync(new CreateEntityCommand(Name: name));

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().BeGreaterThan(0);
    result.Name.Should().Be(name);
}
```

#### Not-found (throws `NotFoundException`)

```csharp
[Fact]
public async Task Update_WhenEntityDoesNotExist_ThrowsNotFoundException()
{
    await Assert.ThrowsAsync<NotFoundException>(() =>
        SendAsync(new UpdateEntityCommand(Id: int.MaxValue, Name: "Ghost")));
}
```

#### Validation failure

```csharp
[Fact]
public async Task Create_WithEmptyName_ThrowsValidationException()
{
    await Assert.ThrowsAnyAsync<Exception>(() =>
        SendAsync(new CreateEntityCommand(Name: string.Empty)));
}
```

#### Side-effect test (DB assertion)

```csharp
[Fact]
public async Task Delete_ShouldMakeEntityUnretrievable()
{
    var entity = await CreateTestEntityAsync();
    await SendAsync(new DeleteEntityCommand(entity.Id));

    var result = await SendAsync(new GetEntityByIdQuery(entity.Id));
    result.Should().BeNull();
}
```

#### Counter / aggregate side-effect

```csharp
[Fact]
public async Task CreateChild_ShouldIncrementParentChildCount()
{
    var parent = await CreateTestParentAsync();
    await SendAsync(new CreateChildCommand(ParentId: parent.Id, Name: "Child"));

    var updated = await SendAsync(new GetParentByIdQuery(parent.Id));
    updated!.ChildCount.Should().Be(1);
}
```

---

## 5. Add Project to Solution

Run from the `MicroservicesArchitecture/` folder:

```powershell
dotnet sln add "src\{Path}\{Name}.API.Tests\{Name}.API.Tests.csproj" --solution-folder "{Name}"
```

The `--solution-folder` value must match the solution folder that already groups the other projects
for this service (e.g. `"Nasheed"`, `"Identity"`, `"Tenant"`).

---

## 6. Build and Verify

```powershell
cd MicroservicesArchitecture
dotnet build "src\{Path}\{Name}.API.Tests\{Name}.API.Tests.csproj"
```

Fix all errors before running tests. Common issues:

| Error                       | Fix                                                                    |
| --------------------------- | ---------------------------------------------------------------------- |
| `Program is inaccessible`   | Add `<InternalsVisibleTo>` in the API project or make `Program` public |
| `Cannot resolve DbContext`  | Check `ConfigureDbContext<TDbContext>()` is called in factory          |
| Worker startup exception    | Remove the offending `IHostedService` in `ConfigureWebHost`            |
| HTTP 404 from mocked client | Verify the mock `Setup()` covers the exact method signature called     |

---

## 7. Create README.md in the Test Project

Every test project must have a `README.md` covering:

1. What is tested (table of test files and their coverage)
2. What is stubbed / excluded and why
3. How to run tests (PowerShell commands)
4. Test patterns with code examples
5. How to add new tests (bullet list)
6. Known limitations

See `src/Apps/Nasheed/Nasheed.API.Tests/README.md` as the canonical example.

---

## Reference: Existing Test Projects

| Service      | Test project                                        | DB         | `appsettings.Test.json` | Notable stubs |
| ------------ | --------------------------------------------------- | ---------- | ----------------------- | ------------- |
| Identity     | `src/Services/Identity/Identity.API.Tests/`         | PostgreSQL | Yes — DB credentials    | None |
| Tenant       | `src/Services/Tenant/Tenant.API.Tests/`             | PostgreSQL | Yes — DB credentials    | Hangfire uses `(sp, config)` lazy overload |
| FileManager  | `src/Services/FileManager/FileManager.API.Tests/`   | PostgreSQL | Yes — DB credentials    | Hangfire uses `(sp, config)` lazy overload |
| Translation  | `src/Services/Translation/Translation.API.Tests/`   | PostgreSQL | Yes — DB credentials    | None |
| Notification | `src/Services/Notification/Notification.API.Tests/` | PostgreSQL | Yes — DB credentials    | Hangfire uses `(sp, config)` lazy overload |
| Category     | `src/Services/Category/Category.API.Tests/`         | PostgreSQL | Yes — DB credentials    | Hangfire uses `(sp, config)` lazy overload; `IFileManagerServiceClient` mocked; Redis disabled |
| Nasheed      | `src/Apps/Nasheed/Nasheed.API.Tests/`               | PostgreSQL | Yes — DB credentials    | `NasheedTenantLoaderService`, `NasheedIngestionWorker`, `IAiApiClient`, `INasheedTenantCache` |

---

## Reference: Shared Testing Library

`src/Shared/IhsanDev.Shared.Testing/` provides the base classes all test projects inherit.

| Class                                       | What it provides                                                                 |
| ------------------------------------------- | -------------------------------------------------------------------------------- |
| `CustomWebApplicationFactory<TProgram>`     | SQLite/PostgreSQL DB swap, `ConfigureDbContext<T>()`, `InitializeDatabase<T>()`  |
| `IntegrationTestBase<TDbContext, TFactory>` | `SendAsync()`, `ExecuteDbContextAsync()`, `GenerateUniqueString()`, auth helpers |

Full API: `src/Shared/IhsanDev.Shared.Testing/README.md`  
Migration log: `Doc/SHARED_TESTING_FILES.md`
