# Shared Testing Infrastructure - Analysis & Recommendations

## 🎯 Question: What Can Be Shared Across Microservice Tests?

Great question! After analyzing the Identity.API.Tests implementation, here's a breakdown of what CAN and SHOULD be shared vs what needs to remain service-specific.

---

## ✅ What CAN Be Shared (Recommended)

### 1. **Base Integration Test Class** (Generic)

**File**: `IntegrationTestBase<TDbContext, TProgram>`

**Shareable Code**:

```csharp
public abstract class IntegrationTestBase<TDbContext, TProgram> :
    IClassFixture<CustomWebApplicationFactory<TProgram>>, IDisposable
    where TDbContext : DbContext
    where TProgram : class
{
    protected readonly CustomWebApplicationFactory<TProgram> Factory;
    protected readonly HttpClient Client;

    // ✅ REUSABLE: Send command/query via MediatR
    protected async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request);
    }

    // ✅ REUSABLE: Execute DB operations
    protected async Task ExecuteDbContextAsync(Func<TDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await action(context);
    }

    protected async Task<T> ExecuteDbContextAsync<T>(Func<TDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await action(context);
    }

    // ✅ REUSABLE: HTTP client helpers
    protected void SetAuthorizationHeader(string token)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    protected void ClearAuthorizationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }
}
```

### 2. **Custom Web Application Factory** (Generic)

**File**: `CustomWebApplicationFactory<TProgram>`

**Shareable Code**:

```csharp
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private DbConnection? _connection;
    public bool UsePostgreSQL { get; set; } = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // ✅ REUSABLE: SQLite in-memory setup
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext
            services.RemoveAll<DbContextOptions>();

            if (!UsePostgreSQL)
            {
                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                // Service needs to register their DbContext
                // using the provided connection
            }
        });
    }
}
```

### 3. **Test Helper Extensions**

**File**: `TestHelperExtensions.cs`

**Shareable Code**:

```csharp
public static class TestHelperExtensions
{
    // ✅ REUSABLE: Generate unique test email
    public static string GenerateUniqueEmail(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}@example.com";
    }

    // ✅ REUSABLE: Generate unique string
    public static string GenerateUniqueString(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    // ✅ REUSABLE: Create test Guid
    public static Guid GenerateTestGuid() => Guid.NewGuid();
}
```

---

## ❌ What SHOULD NOT Be Shared (Service-Specific)

### 1. **Authentication Helpers**

**Why**: Each service may have different auth mechanisms

```csharp
// ❌ NOT SHAREABLE - Identity service specific
protected async Task<string> GetAuthTokenAsync()
{
    var loginCommand = new LoginCommand("test@example.com", "Test123!");
    var result = await SendAsync(loginCommand);
    return result.AccessToken;
}

// Each service needs its own auth helper
```

### 2. **Entity Creation Helpers**

**Why**: Each service has different entities

```csharp
// ❌ NOT SHAREABLE - Identity service specific
protected async Task<User> CreateTestUserAsync(...)
{
    // Uses Identity-specific User entity
}

// Each service needs its own entity creation helpers
```

### 3. **Service-Specific Configuration**

**Why**: Each service has different settings

```csharp
// ❌ NOT SHAREABLE - Identity service specific
testConfig["Jwt:Key"] = "...";
testConfig["Jwt:Issuer"] = "...";
```

---

## 📋 Recommended Approach

### Option 1: Copy-Paste Pattern (RECOMMENDED for now)

**Why**:

- Simple and clear
- No version conflicts
- Service-specific customization easy
- No shared library maintenance

**How**:

1. Copy `IntegrationTestBase.cs` to each service's test project
2. Copy `CustomWebApplicationFactory.cs` to each service's test project
3. Customize for service-specific needs
4. Update INTEGRATION_TESTING_PROMPT.md to include these as templates

### Option 2: Shared Testing Library (Future Consideration)

**Create**: `IhsanDev.Shared.Testing` library

**Include**:

- Generic base classes
- Helper extensions
- Common test utilities

**Require Each Service**:

- Inherit from generic base classes
- Implement service-specific helpers
- Configure service-specific settings

**Challenges**:

- ⚠️ Version dependency management (.NET 9 packages need 9.0.0 versions)
- ⚠️ Central Package Management conflicts
- ⚠️ Maintenance overhead
- ⚠️ Less flexibility for service-specific customization

---

## 🎯 Practical Recommendation

### For Your Current Situation

**Use the Copy-Paste Pattern**:

1. **Keep the current setup** in Identity.API.Tests as a template
2. **Update INTEGRATION_TESTING_PROMPT.md** with the full code for:

   - `IntegrationTestBase.cs` (as generic template)
   - `CustomWebApplicationFactory.cs` (as generic template)
   - `TestHelperExtensions.cs` (NEW - add this)

3. **When creating tests for new services**, AI will:
   - Copy the template code
   - Replace `IdentityDbContext` with `[Service]DbContext`
   - Replace `User` entities with service entities
   - Add service-specific helpers
   - Customize configuration

### Benefits of This Approach

✅ **Zero dependency issues** - No shared library version conflicts  
✅ **Full customization** - Each service can modify as needed  
✅ **Clear ownership** - Each team owns their test infrastructure  
✅ **Easy debugging** - All code is local  
✅ **Simple maintenance** - No shared library to maintain  
✅ **Fast iteration** - No waiting for shared library updates

---

## 📝 Updated INTEGRATION_TESTING_PROMPT.md

I'll update the prompt to include **full template code** for:

1. **IntegrationTestBase.cs** - Generic version with `<TDbContext, TProgram>`
2. **CustomWebApplicationFactory.cs** - Generic version with `<TProgram>`
3. **TestHelperExtensions.cs** - NEW utility class

This way, when you use the prompt for other services, AI will generate:

- Complete, working code
- Service-specific customizations
- No shared library dependencies

---

## 🚀 Implementation Plan

### Immediate Actions

1. ✅ Keep Identity.API.Tests as-is (working perfectly)
2. ✅ Update INTEGRATION_TESTING_PROMPT.md with generic templates
3. ✅ Add TestHelperExtensions.cs to the prompt
4. ✅ Document the copy-paste pattern

### Future (When You Have 3+ Services Using Same Pattern)

1. Consider extracting to IhsanDev.Shared.Testing
2. Fix central package management version conflicts
3. Make base classes truly generic
4. Add comprehensive shared documentation

---

## 💡 Summary

**Answer to Your Question**:

**YES, files CAN be shared** conceptually, but the BEST approach for now is:

1. **Template-based sharing** (via INTEGRATION_TESTING_PROMPT.md)
2. **Copy-paste pattern** for each service
3. **Service-specific customization** in each test project

**What to share**:

- ✅ Template code structure
- ✅ Testing patterns
- ✅ Helper method concepts
- ✅ Documentation and best practices

**What NOT to share** (for now):

- ❌ Actual compiled shared library
- ❌ Service-specific implementations
- ❌ Entity creation helpers
- ❌ Auth token generation

**When you create tests for your next service** (e.g., Product service):

1. Use INTEGRATION_TESTING_PROMPT.md
2. AI generates the complete test infrastructure
3. Code is service-specific but follows same pattern
4. Zero dependency issues

---

## 📊 Comparison

| Aspect                | Copy-Paste Pattern | Shared Library     |
| --------------------- | ------------------ | ------------------ |
| **Setup Time**        | ⚡ Instant         | ⏰ Complex         |
| **Customization**     | ✅ Full freedom    | ⚠️ Limited         |
| **Version Conflicts** | ✅ None            | ❌ Many            |
| **Maintenance**       | ✅ Local           | ❌ Central + Local |
| **Team Autonomy**     | ✅ Complete        | ⚠️ Dependent       |
| **Debug Experience**  | ✅ Simple          | ⚠️ Complex         |
| **Code Reuse**        | ⚠️ Via templates   | ✅ Via library     |

**Verdict**: Copy-Paste Pattern is the **winner** for microservices architecture!

---

<div align="center">

## ✅ Recommendation: Use Template-Based Sharing

**Update INTEGRATION_TESTING_PROMPT.md** with full generic templates  
**Copy to each service** during test generation  
**Customize per service** as needed  
**Keep it simple** and maintainable

</div>
