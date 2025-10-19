# Integration Testing Prompt for Microservices

Use this prompt with AI assistants (GitHub Copilot, ChatGPT, etc.) to create comprehensive integration tests for your microservices using the **handler-based testing approach**.

---

## 🎯 Universal Prompt Template

````
I need you to create comprehensive integration tests for my [SERVICE_NAME] microservice API.

PROJECT STRUCTURE:
- Framework: .NET 9.0 with Minimal APIs
- Architecture: Clean Architecture (Domain, Application, Infrastructure, API layers)
- Pattern: CQRS with MediatR
- Database: [PostgreSQL/SQL Server/MySQL] with Entity Framework Core
- Authentication: JWT Bearer tokens

CRITICAL REQUIREMENTS:

1. TESTING APPROACH - Handler-Based (NOT HTTP-based):
   - Test MediatR handlers DIRECTLY using SendAsync() method
   - DO NOT test via HTTP endpoints (avoids .NET 9.0 PipeWriter bug)
   - Call commands/queries through IMediator.Send()
   - This is faster, more reliable, and bypasses HTTP layer issues

2. PROJECT STRUCTURE:
   Create: [SERVICE_NAME].API.Tests project with this structure:

   [SERVICE_NAME].API.Tests/
   ├── Infrastructure/
   │   ├── CustomWebApplicationFactory.cs
   │   └── IntegrationTestBase.cs
   ├── Endpoints/
   │   └── [Feature]EndpointsTests.cs (one per feature area)
   └── README.md

3. TESTING INFRASTRUCTURE:

   **CustomWebApplicationFactory.cs**:
   - Inherit from WebApplicationFactory<Program>
   - Configure test services (in-memory SQLite by default)
   - Support optional PostgreSQL with UsePostgreSQL flag
   - Override database configuration
   - Set environment to "Testing"

   **IntegrationTestBase.cs**:
   - Provide SendAsync<TResponse>(IRequest<TResponse>) method for calling handlers
   - Provide ExecuteDbContextAsync() for database operations
   - Provide CreateTest[Entity]Async() helpers for test data
   - Provide GetAuthTokenAsync() for authentication
   - Support [Collection("Sequential")] for sequential test execution

4. TEST PATTERNS TO FOLLOW:

   **Success Test Pattern**:
   ```csharp
   [Fact]
   public async Task Operation_WithValidData_ShouldSucceed()
   {
       // Arrange
       var command = new SomeCommand(...);

       // Act
       var result = await SendAsync(command);

       // Assert
       result.Should().NotBeNull();
       result.Property.Should().Be(expectedValue);
   }
````

**Exception Test Pattern**:

```csharp
[Fact]
public async Task Operation_WithInvalidData_ShouldThrowException()
{
    // Arrange
    var command = new InvalidCommand(...);

    // Act & Assert
    await Assert.ThrowsAsync<SpecificException>(
        async () => await SendAsync(command)
    );
}
```

**Database Verification Pattern**:

```csharp
[Fact]
public async Task Operation_ShouldPersistToDatabase()
{
    // Act
    var result = await SendAsync(new CreateCommand(...));

    // Assert - Verify in database
    var entity = await ExecuteDbContextAsync(async context =>
        await context.Entities.FindAsync(result.Id)
    );
    entity.Should().NotBeNull();
}
```

5. EXCEPTION TYPES TO TEST:

   - UnauthorizedException (401)
   - ConflictException (409) - duplicates, conflicts
   - NotFoundException (404)
   - BadRequestException (400)
   - ValidationException (FluentValidation failures)

6. TEST DATA ISOLATION:

   - Use GUID suffixes for unique test data: $"email-{Guid.NewGuid().ToString("N")[..8]}@test.com"
   - Prevent unique constraint violations
   - Allow parallel/sequential test execution

7. DEPENDENCIES:

   ```xml
   <PackageReference Include="xUnit" Version="2.6.6" />
   <PackageReference Include="FluentAssertions" Version="6.12.0" />
   <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
   <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
   ```

8. COVERAGE REQUIREMENTS:

   - Test ALL commands and queries
   - Test success scenarios
   - Test validation failures
   - Test business rule violations
   - Test authorization/authentication
   - Test database operations
   - Test exception handling

9. NAMING CONVENTIONS:

   - Test methods: `MethodName_Scenario_ExpectedResult`
   - Test classes: `[Feature]EndpointsTests`
   - Use descriptive names
   - Group tests by feature with #region

10. DO NOT MODIFY PRODUCTION CODE:
    - Zero changes to handlers
    - Zero changes to API endpoints
    - All testing logic stays in test project
    - Maintain clean separation

ENDPOINTS TO TEST:
[List your endpoints here, for example:]

- POST /api/[resource] - Create
- GET /api/[resource] - List
- GET /api/[resource]/{id} - Get by ID
- PUT /api/[resource]/{id} - Update
- DELETE /api/[resource]/{id} - Delete

DOMAIN ENTITIES:
[List your domain entities, for example:]

- [Entity1]: Properties: [list key properties]
- [Entity2]: Properties: [list key properties]

COMMANDS/QUERIES TO TEST:
[List your MediatR commands and queries, for example:]

- Create[Entity]Command -> Returns [EntityDto]
- Get[Entity]ByIdQuery -> Returns [EntityDto]
- Update[Entity]Command -> Returns [EntityDto]
- Delete[Entity]Command -> Returns bool

VALIDATION RULES:
[List validation rules to test, for example:]

- Email must be valid format
- Password must be strong (min 8 chars, uppercase, lowercase, number, special char)
- Required fields: [list]
- Unique constraints: [list]

AUTHENTICATION/AUTHORIZATION:
[Describe auth requirements, for example:]

- Public endpoints: [list]
- Authenticated endpoints: [list]
- Admin-only endpoints: [list]

PLEASE CREATE:

1. CustomWebApplicationFactory.cs - Test server configuration
2. IntegrationTestBase.cs - Base class with SendAsync() and helper methods
3. [Feature]EndpointsTests.cs files - Comprehensive tests for each feature area
4. README.md - Documentation of test approach and execution

EXAMPLE FROM IDENTITY SERVICE:
Reference the Identity.API.Tests project structure as the gold standard:

- Uses SendAsync() to call handlers directly
- Comprehensive test coverage (36+ tests)
- Proper exception testing
- Database verification
- Clean test data with GUID suffixes
- Zero production code modifications

OUTPUT FORMAT:

- Provide complete, working code
- Include all necessary using statements
- Add XML documentation comments
- Follow C# coding conventions
- Use FluentAssertions syntax
- Include comprehensive test scenarios

````

---

## 📋 Checklist Before Generating Tests

- [ ] Service name identified
- [ ] All endpoints documented
- [ ] Domain entities listed
- [ ] Commands/queries listed
- [ ] Validation rules documented
- [ ] Auth requirements specified
- [ ] Exception types identified
- [ ] Database provider confirmed

---

## 🔧 Customization Guide

### For Different Database Providers

**PostgreSQL**:
```csharp
services.AddDbContext<YourDbContext>(options =>
    options.UseNpgsql("Host=localhost;Database=TestDb;Username=test;Password=test"));
````

**SQL Server**:

```csharp
services.AddDbContext<YourDbContext>(options =>
    options.UseSqlServer("Server=localhost;Database=TestDb;Trusted_Connection=true"));
```

**SQLite (In-Memory - Default)**:

```csharp
services.AddDbContext<YourDbContext>(options =>
    options.UseSqlite("DataSource=:memory:"));
```

### For Different Authentication Schemes

**JWT Bearer**:

```csharp
protected async Task<string> GetAuthTokenAsync()
{
    var loginCommand = new LoginCommand("test@example.com", "Test123!");
    var result = await SendAsync(loginCommand);
    return result.AccessToken;
}
```

**API Key**:

```csharp
protected void SetApiKey(string apiKey)
{
    Client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
}
```

**OAuth2**:

```csharp
protected async Task<string> GetOAuthTokenAsync()
{
    // Implement OAuth token retrieval
}
```

### For Different CQRS Patterns

**With MediatR** (as shown):

```csharp
protected async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
{
    using var scope = Services.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    return await mediator.Send(request, CancellationToken.None);
}
```

**Without MediatR**:

```csharp
protected async Task<TResponse> ExecuteAsync<TService, TResponse>(
    Func<TService, Task<TResponse>> action)
{
    using var scope = Services.CreateScope();
    var service = scope.ServiceProvider.GetRequiredService<TService>();
    return await action(service);
}
```

---

## 🎯 Example: Creating Tests for a Product Service

### Step 1: Prepare Information

```
SERVICE: Product Catalog Service

ENDPOINTS:
- POST /api/products - CreateProduct (Admin)
- GET /api/products - GetProducts (Public)
- GET /api/products/{id} - GetProductById (Public)
- PUT /api/products/{id} - UpdateProduct (Admin)
- DELETE /api/products/{id} - DeleteProduct (Admin)

ENTITIES:
- Product: Id, Name, Description, Price, Stock, CategoryId, IsActive

COMMANDS:
- CreateProductCommand -> ProductDto
- UpdateProductCommand -> ProductDto
- DeleteProductCommand -> bool

QUERIES:
- GetProductsQuery -> PaginatedList<ProductDto>
- GetProductByIdQuery -> ProductDto

VALIDATION:
- Name: Required, Max 200 chars
- Price: Must be > 0
- Stock: Must be >= 0
- CategoryId: Must exist in database

AUTH:
- Public: GET endpoints
- Admin: POST, PUT, DELETE endpoints
```

### Step 2: Use the Prompt

Copy the universal prompt above and replace placeholders with your service details.

### Step 3: Review Generated Code

The AI should generate:

1. ✅ CustomWebApplicationFactory.cs
2. ✅ IntegrationTestBase.cs
3. ✅ ProductEndpointsTests.cs
4. ✅ README.md

### Step 4: Customize

Add any service-specific:

- Custom test helpers
- Complex validation scenarios
- Business rule tests
- Integration with external services (mocked)

---

## 🚀 Quick Start Commands

```bash
# Create new test project
dotnet new xunit -n YourService.API.Tests

# Add to solution
dotnet sln add YourService.API.Tests/YourService.API.Tests.csproj

# Add dependencies
dotnet add package xUnit --version 2.6.6
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 9.0.0
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0

# Add project references
dotnet add reference ../YourService.API/YourService.API.csproj
dotnet add reference ../YourService.Application/YourService.Application.csproj
dotnet add reference ../YourService.Infrastructure/YourService.Infrastructure.csproj

# Run tests
dotnet test
```

---

## 📚 Additional Resources

### Complete Template Code (Copy These)

#### IntegrationTestBase.cs Template

```csharp
using System.Net.Http.Headers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace [YourService].API.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests providing common utilities
/// Tests use MediatR handlers directly to bypass .NET 9.0 PipeWriter bug
/// </summary>
public abstract class IntegrationTestBase<TDbContext> :
    IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
    where TDbContext : DbContext
{
    protected readonly CustomWebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(CustomWebApplicationFactory<Program> factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Execute MediatR command/query within a scope
    /// This bypasses HTTP layer and tests handlers directly
    /// </summary>
    protected async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request);
    }

    /// <summary>
    /// Execute database operations within a scope
    /// </summary>
    protected async Task ExecuteDbContextAsync(Func<TDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await action(context);
    }

    /// <summary>
    /// Execute database operations with return value
    /// </summary>
    protected async Task<T> ExecuteDbContextAsync<T>(Func<TDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await action(context);
    }

    /// <summary>
    /// Set authorization header with bearer token
    /// </summary>
    protected void SetAuthorizationHeader(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Clear authorization header
    /// </summary>
    protected void ClearAuthorizationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Generate unique email for testing
    /// </summary>
    protected string GenerateUniqueEmail(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}@example.com";
    }

    /// <summary>
    /// Generate unique string for testing
    /// </summary>
    protected string GenerateUniqueString(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    public virtual void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

#### CustomWebApplicationFactory.cs Template

```csharp
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace [YourService].API.Tests.Infrastructure;

/// <summary>
/// Custom web application factory for integration tests
/// Supports SQLite in-memory (default) or PostgreSQL for testing
/// </summary>
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private DbConnection? _connection;

    /// <summary>
    /// Set to true to use PostgreSQL for tests instead of SQLite in-memory
    /// </summary>
    public bool UsePostgreSQL { get; set; } = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration (customize per service)
            var testConfig = new Dictionary<string, string?>
            {
                ["DatabaseSettings:EnableSensitiveDataLogging"] = "true",
                ["DatabaseSettings:EnableDetailedErrors"] = "true",
                // Add service-specific config here
            };

            if (UsePostgreSQL)
            {
                testConfig["DatabaseSettings:Provider"] = "PostgreSql";
                testConfig["DatabaseSettings:ConnectionString"] =
                    "Host=localhost;Database=testdb;Username=test;Password=test;";
            }
            else
            {
                testConfig["DatabaseSettings:Provider"] = "Sqlite";
                testConfig["DatabaseSettings:ConnectionString"] = "DataSource=:memory:";
            }

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            services.RemoveAll<DbContextOptions<[YourDbContext]>>();
            services.RemoveAll<[YourDbContext]>();

            if (UsePostgreSQL)
            {
                // Use PostgreSQL for tests
                services.AddDbContext<[YourDbContext]>(options =>
                {
                    options.UseNpgsql("Host=localhost;Database=testdb;Username=test;Password=test;");
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            }
            else
            {
                // Use SQLite in-memory (default)
                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                services.AddDbContext<[YourDbContext]>(options =>
                {
                    options.UseSqlite(_connection);
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            }

            // Build service provider and create database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<[YourDbContext]>();

            // Ensure database is created
            if (UsePostgreSQL)
            {
                dbContext.Database.Migrate();
            }
            else
            {
                dbContext.Database.EnsureCreated();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

#### Test Helper Example

```csharp
// Add service-specific helpers in your IntegrationTestBase

// Example: Authentication helper
protected async Task<string> GetAuthTokenAsync(string email = "test@example.com")
{
    var loginCommand = new LoginCommand(email, "Password123!");
    var result = await SendAsync(loginCommand);
    return result.AccessToken;
}

// Example: Entity creation helper
protected async Task<YourEntity> CreateTestEntityAsync(string name = "Test Entity")
{
    return await ExecuteDbContextAsync(async context =>
    {
        var entity = new YourEntity
        {
            Name = GenerateUniqueString(name),
            CreatedAt = DateTime.UtcNow
        };

        context.YourEntities.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    });
}
```

### Key Concepts

1. **Handler-Based Testing**: Testing business logic through MediatR handlers instead of HTTP endpoints
2. **Integration Testing**: Testing the full stack (handlers → services → database) without HTTP layer
3. **Test Isolation**: Each test is independent with unique data
4. **Fast Execution**: In-memory SQLite for speed

### Benefits of This Approach

✅ **Fast**: No HTTP overhead  
✅ **Reliable**: No framework bugs  
✅ **Clean**: Zero production code changes  
✅ **Comprehensive**: Tests actual business logic  
✅ **Maintainable**: Simple, clear patterns  
✅ **Debuggable**: Easy to step through

### When NOT to Use This Approach

❌ Need to test HTTP middleware  
❌ Need to test routing logic  
❌ Need to test request/response serialization  
❌ Not using MediatR/CQRS  
❌ Framework doesn't have PipeWriter bug

---

## 🎓 Learning from Identity Service Example

The Identity service tests demonstrate:

1. **Proper Structure**:

   - Clear separation of concerns
   - Reusable base classes
   - Helper methods for common operations

2. **Comprehensive Coverage**:

   - 36 tests across 3 feature areas
   - Success and failure scenarios
   - Database verification
   - Exception handling

3. **Best Practices**:

   - Unique test data with GUIDs
   - FluentAssertions for readability
   - Proper exception types
   - Clear test naming

4. **Zero Production Impact**:
   - No handler modifications
   - No endpoint changes
   - Clean architecture maintained

---

## 💡 Tips for Success

1. **Start Small**: Begin with one feature area
2. **Use Helpers**: Create helper methods for common operations
3. **Test Exceptions**: Don't just test happy paths
4. **Verify Database**: Check side effects in database
5. **Unique Data**: Always use GUIDs for test data
6. **Document**: Maintain good README
7. **Consistent Naming**: Follow conventions
8. **Group Tests**: Use #region for organization

---

## 🔍 Troubleshooting

### "Cannot resolve service for type IMediator"

**Solution**: Ensure MediatR is registered in test startup configuration

### "SQLite Error 19: UNIQUE constraint failed"

**Solution**: Use GUID suffixes for all test data to ensure uniqueness

### "AutoMapper configuration error"

**Solution**: Ensure AutoMapper profiles are registered in test configuration

### "Test database not found"

**Solution**: Check connection strings and ensure database provider is configured

---

<div align="center">

## 🎯 Ready to Create Tests!

**Copy the prompt template above, customize it for your service, and let AI generate your comprehensive integration tests!**

</div>

---

## 📞 Support

For questions or issues with this approach:

1. Review the Identity.API.Tests project as reference
2. Check the README.md in the test project
3. Consult the troubleshooting section above

---

**Version**: 1.0  
**Last Updated**: October 19, 2025  
**Based On**: Identity.API.Tests (36 comprehensive integration tests)
