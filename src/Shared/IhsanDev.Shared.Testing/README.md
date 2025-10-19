# IhsanDev.Shared.Testing

Shared testing infrastructure for all microservices in the MicroservicesArchitecture solution.

## 📦 What's Included

### Infrastructure

- **`IntegrationTestBase<TDbContext, TFactory>`** - Generic base class for integration tests

  - MediatR command/query execution via `SendAsync()`
  - Database operations via `ExecuteDbContextAsync()`
  - Authorization header management
  - Helper methods for unique test data generation
  - Bypasses .NET 9.0 PipeWriter bug by testing handlers directly

- **`CustomWebApplicationFactory<TProgram>`** - Generic test server factory
  - SQLite in-memory database (default, fast)
  - PostgreSQL support (optional, for production-like tests)
  - Configurable test configuration
  - Database initialization and seeding hooks

### Helpers

- **`TestHelpers`** - Static utility methods
  - `GenerateUniqueEmail()` - Creates unique test email addresses
  - `GenerateUniqueString()` - Creates unique test strings
  - `GenerateUniqueInt()` - Creates unique test integers
  - `WaitForConditionAsync()` - Async condition polling with timeout
  - `WaitForCondition()` - Sync condition polling with timeout

## 🚀 Usage

### 1. Add Reference to Your Test Project

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Testing\IhsanDev.Shared.Testing.csproj" />
</ItemGroup>
```

### 2. Create Service-Specific Factory

```csharp
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace YourService.API.Tests.Infrastructure;

public class CustomWebApplicationFactory :
    IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();

        // Add service-specific configuration
        config["YourSetting"] = "YourValue";

        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Configure your DbContext
            ConfigureDbContext<YourDbContext>(services);

            // Initialize database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<YourDbContext>();

            InitializeDatabase(dbContext);
            SeedTestData(dbContext);
        });
    }

    protected override void SeedTestData<TDbContext>(TDbContext context)
    {
        // Add service-specific seed data
    }
}
```

### 3. Create Service-Specific Test Base

```csharp
using IhsanDev.Shared.Testing.Infrastructure;

namespace YourService.API.Tests.Infrastructure;

public abstract class IntegrationTestBase :
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<YourDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // Add service-specific helper methods
    protected async Task<string> GetAuthTokenAsync(string email = "test@example.com")
    {
        var command = new LoginCommand(email, "Password123!");
        var result = await SendAsync(command);
        return result.AccessToken;
    }

    protected async Task<YourEntity> CreateTestEntityAsync()
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var entity = new YourEntity
            {
                Name = GenerateUniqueString("test"),
                Email = GenerateUniqueEmail("test")
            };

            context.YourEntities.Add(entity);
            await context.SaveChangesAsync();
            return entity;
        });
    }
}
```

### 4. Write Tests

```csharp
namespace YourService.API.Tests;

public class YourEndpointsTests : IntegrationTestBase
{
    public YourEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateEntity_WithValidData_ShouldSucceed()
    {
        // Arrange
        var command = new CreateEntityCommand(
            Name: GenerateUniqueString("entity"),
            Email: GenerateUniqueEmail("entity")
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);

        // Verify in database
        var entity = await ExecuteDbContextAsync(async ctx =>
            await ctx.YourEntities.FindAsync(result.Id));
        entity.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEntity_WithDuplicateEmail_ShouldThrowConflictException()
    {
        // Arrange
        var email = GenerateUniqueEmail("duplicate");
        await CreateTestEntityAsync(); // Creates entity with same email

        var command = new CreateEntityCommand(
            Name: "Test",
            Email: email
        );

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(command));
    }
}
```

## 🎯 Key Features

### Handler-Based Testing

Tests execute MediatR handlers directly instead of going through HTTP:

```csharp
// ✅ Good - Direct handler execution
var result = await SendAsync(new CreateUserCommand(...));

// ❌ Avoid - HTTP calls (has .NET 9 PipeWriter bug)
var response = await Client.PostAsJsonAsync("/users", new { ... });
```

**Benefits:**

- Bypasses .NET 9.0 PipeWriter bug
- Faster execution (no HTTP serialization)
- Tests business logic directly
- Cleaner exception handling

### Database Testing

Two modes supported:

1. **SQLite In-Memory (Default)** - Fast, isolated
2. **PostgreSQL** - Production-like testing

```csharp
// Use PostgreSQL instead of SQLite
var factory = new CustomWebApplicationFactory
{
    UsePostgreSQL = true,
    PostgreSqlConnectionString = "Host=localhost;Database=testdb;..."
};
```

### Unique Test Data

Prevent conflicts with built-in helpers:

```csharp
var email = GenerateUniqueEmail("test");      // test-a1b2c3d4@example.com
var name = GenerateUniqueString("user");      // user-e5f6g7h8
var id = TestHelpers.GenerateUniqueInt();     // Random 6-digit number
```

## 📋 Requirements

- .NET 9.0
- xUnit 2.6.6
- FluentAssertions 6.12.0
- MediatR 12.4.1
- Microsoft.AspNetCore.Mvc.Testing 9.0.0
- Entity Framework Core 9.0.0

## 🔗 Used By

- `Identity.API.Tests` - Identity service tests (35 tests)
- Add your service tests here...

## 📝 Example: Complete Identity Service Integration

See `src/Services/Identity/Identity.API.Tests/` for a complete working example:

- **Infrastructure/CustomWebApplicationFactory.cs** - Service-specific factory
- **Infrastructure/IntegrationTestBase.cs** - Service-specific test base
- **AuthEndpointsTests.cs** - Authentication endpoint tests
- **UserEndpointsTests.cs** - User management tests
- **AdminEndpointsTests.cs** - Admin operations tests

All 35 tests pass in ~7 seconds.

## 🤝 Contributing

When adding new shared testing utilities:

1. Keep them generic and reusable
2. Avoid service-specific logic
3. Document with XML comments
4. Add examples to this README

## 📖 Related Documentation

- [Identity.API.Tests README](../../Services/Identity/Identity.API.Tests/README.md)
- [INTEGRATION_TESTING_PROMPT.md](../../../INTEGRATION_TESTING_PROMPT.md)
- [INTEGRATION_TESTING_SUMMARY.md](../../../INTEGRATION_TESTING_SUMMARY.md)
- [SHARED_TESTING_ANALYSIS.md](../../../SHARED_TESTING_ANALYSIS.md)
