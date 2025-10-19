# Shared Testing Helper - Cross-Service Integration Testing

## Overview

The `TenantTestHelper` provides reusable utilities for testing tenant-related functionality across multiple services. This helper demonstrates how to write tests that work for both tenant-enabled and non-tenant services.

## Files Created

### 1. Shared Helper (Reusable)

- **Location**: `src/Shared/IhsanDev.Shared.Testing/Helpers/TenantTestHelper.cs`
- **Purpose**: Common utilities for tenant operations that can be used by any service

### 2. Integration Tests (Example Usage)

- **Location**: `src/Services/Tenant/Tenant.API.Tests/Integration/SharedHelperIntegrationTests.cs`
- **Purpose**: Demonstrates how to use the shared helper in different scenarios

## Quick Start

### Using Shared Helper in Your Tests

```csharp
using IhsanDev.Shared.Testing.Helpers;

// Generate unique IDs
var userId = TenantTestHelper.GenerateUniqueUserId();
var tenantId = TenantTestHelper.GenerateUniqueTenantId("my-prefix");

// Create user and tenant (for HTTP-based tests)
var (userId, tenantId, tenantResponseId) = await TenantTestHelper.CreateUserAndTenantAsync(
    httpClient,
    tenantId: "custom-tenant-id",  // optional
    userId: 12345                   // optional
);

// Get tenant by ID
var tenant = await TenantTestHelper.GetTenantByIdAsync(httpClient, tenantId);

// Check if service has tenant support
var isTenantEnabled = await TenantTestHelper.IsTenantEnabledAsync(httpClient);
```

## Testing Patterns

### Pattern 1: Basic Flow (Create User → Create Tenant → Get Tenant)

```csharp
[Fact]
public async Task CreateUser_CreateTenant_GetTenant_UsingSharedHelper_ShouldSucceed()
{
    // Arrange - Generate unique IDs using shared helper
    var userId = TenantTestHelper.GenerateUniqueUserId();
    var tenantId = TenantTestHelper.GenerateUniqueTenantId();

    // Act 1 - Create tenant
    var createCommand = new CreateTenantCommand(
        TenantId: tenantId,
        TenantName: "Test Tenant",
        UserId: userId,
        StartDate: DateTime.UtcNow,
        ExpireDate: DateTime.UtcNow.AddYears(1),
        Data: "{\"Jwt\":{\"Secret\":\"test-secret\"}}"
    );

    var createdTenant = await SendAsync(createCommand);

    // Act 2 - Get tenant by ID
    var getTenantQuery = new GetTenantByIdQuery(tenantId);
    var retrievedTenant = await SendAsync(getTenantQuery);

    // Assert
    createdTenant.Should().NotBeNull();
    retrievedTenant.Should().NotBeNull();
    retrievedTenant!.TenantId.Should().Be(tenantId);
}
```

### Pattern 2: Tenant-Enabled Project (Project B)

```csharp
[Fact]
public async Task TenantEnabled_Project_ShouldLoadTenantSettings()
{
    // Arrange - Simulate Project B (tenant-enabled)
    var userId = TenantTestHelper.GenerateUniqueUserId();
    var tenantId = TenantTestHelper.GenerateUniqueTenantId("project-b");

    // Act - Create tenant
    var createCommand = new CreateTenantCommand(
        TenantId: tenantId,
        TenantName: "Project B Tenant",
        UserId: userId,
        StartDate: DateTime.UtcNow,
        ExpireDate: DateTime.UtcNow.AddYears(1),
        Data: "{\"Jwt\":{\"Secret\":\"project-b-secret\"}}"
    );

    var tenant = await SendAsync(createCommand);

    // Assert - Tenant settings are loaded
    tenant.Should().NotBeNull();
    tenant.TenantId.Should().Be(tenantId);
}
```

### Pattern 3: Non-Tenant Project (Project A)

```csharp
[Fact]
public void NonTenant_Project_ShouldUseDefaultSettings()
{
    // Arrange - Simulate Project A (non-tenant)
    var isTenantEnabled = false; // Project A setting

    // Act & Assert - Non-tenant project flow
    if (isTenantEnabled)
    {
        Assert.Fail("Project A should not have tenant enabled");
    }
    else
    {
        // Project A: Load default settings
        var defaultSettings = GetDefaultSettings();

        defaultSettings.Should().NotBeNull();
        defaultSettings.AppName.Should().Be("Project A");
        defaultSettings.UseTenantSettings.Should().BeFalse();
    }
}
```

### Pattern 4: Conditional Tenant Load (Toggle Pattern)

```csharp
[Fact]
public async Task ConditionalTenantLoad_BasedOnProjectConfiguration()
{
    // Arrange
    var projectName = "Project B"; // Could come from config
    var isTenantEnabled = projectName == "Project B";

    // Act & Assert
    if (isTenantEnabled)
    {
        // Project B: Tenant-enabled flow
        var userId = TenantTestHelper.GenerateUniqueUserId();
        var tenantId = TenantTestHelper.GenerateUniqueTenantId();

        var tenant = await CreateTenantAsync(tenantId, userId);
        tenant.Should().NotBeNull();
    }
    else
    {
        // Project A: Non-tenant flow
        var settings = GetDefaultSettings();
        settings.UseTenantSettings.Should().BeFalse();
    }
}
```

## API Reference

### TenantTestHelper Methods

#### `GenerateUniqueUserId()`

- **Returns**: `int` - Thread-safe unique user ID
- **Starting Value**: 2000 (to avoid conflicts with service-specific counters)
- **Thread Safety**: Yes (uses `Interlocked.Increment`)

#### `GenerateUniqueTenantId(string prefix = "shared-tenant")`

- **Parameters**:
  - `prefix` - Optional prefix for the tenant ID
- **Returns**: `string` - Format: `{prefix}-{8-char-guid}`
- **Example**: `"shared-tenant-a1b2c3d4"`

#### `CreateUserAndTenantAsync(HttpClient httpClient, string? tenantId = null, int? userId = null)`

- **Parameters**:
  - `httpClient` - HTTP client configured for tenant service
  - `tenantId` - Optional custom tenant ID
  - `userId` - Optional custom user ID
- **Returns**: `(int UserId, string TenantId, int TenantResponseId)`
- **Use Case**: Cross-service HTTP-based testing

#### `GetTenantByIdAsync(HttpClient httpClient, string tenantId)`

- **Parameters**:
  - `httpClient` - HTTP client configured for tenant service
  - `tenantId` - The tenant ID to retrieve
- **Returns**: `TenantResponse?` - Tenant data or null if not found

#### `IsTenantEnabledAsync(HttpClient httpClient, string healthEndpoint = "/health")`

- **Parameters**:
  - `httpClient` - HTTP client configured for the service to check
  - `healthEndpoint` - Optional health endpoint (defaults to `/health`)
- **Returns**: `bool` - True if tenant-enabled, false otherwise
- **Use Case**: Dynamically detect tenant support

## Response Models

### CreateTenantResponse

```csharp
public class CreateTenantResponse
{
    public int Id { get; set; }
    public string TenantId { get; set; }
    public string TenantName { get; set; }
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}
```

### TenantResponse

```csharp
public class TenantResponse
{
    public int Id { get; set; }
    public string TenantId { get; set; }
    public string TenantName { get; set; }
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}
```

## Project Scenarios

### Scenario 1: Project A (Non-Tenant)

- **Description**: Simple project without multi-tenancy
- **Configuration**: `isTenantEnabled = false`
- **Settings Source**: appsettings.json or environment variables
- **Test Approach**: Use default settings, skip tenant lookups

### Scenario 2: Project B (Tenant-Enabled)

- **Description**: Multi-tenant project with tenant isolation
- **Configuration**: `isTenantEnabled = true`
- **Settings Source**: Tenant-specific database records
- **Test Approach**: Create tenant, load tenant-specific settings

## Best Practices

### 1. Use Shared Helper for ID Generation

```csharp
// ✅ Good - Reusable across services
var userId = TenantTestHelper.GenerateUniqueUserId();

// ❌ Avoid - Hardcoded IDs cause conflicts
var userId = 1;
```

### 2. Use Prefixes for Organization

```csharp
// ✅ Good - Clear test organization
var tenantId = TenantTestHelper.GenerateUniqueTenantId("integration");
var tenantId2 = TenantTestHelper.GenerateUniqueTenantId("performance");

// ❌ Avoid - Generic IDs harder to trace
var tenantId = TenantTestHelper.GenerateUniqueTenantId();
```

### 3. Test Both Scenarios

```csharp
// ✅ Good - Toggle pattern supports both
if (isTenantEnabled) { /* tenant flow */ }
else { /* non-tenant flow */ }

// ❌ Avoid - Only testing one scenario
// Always assume tenant-enabled
```

### 4. Clean, Minimal Tests

```csharp
// ✅ Good - Single responsibility
[Fact]
public async Task CreateTenant_ShouldSucceed()

// ❌ Avoid - Testing multiple concepts
[Fact]
public async Task CreateTenant_UpdateTenant_DeleteTenant_GetTenant()
```

## Test Results

```
Test summary: total: 42, failed: 0, succeeded: 42, skipped: 0
✅ All tests passing (100% pass rate)
```

### Test Breakdown

- **Original Tests**: 38 (Endpoints + CRUD operations)
- **New Integration Tests**: 4 (Shared helper usage + Toggle patterns)
  - ✅ Basic flow (Create → Get)
  - ✅ Tenant-enabled project
  - ✅ Non-tenant project
  - ✅ Conditional tenant load

## Future Services

When creating tests for new services (e.g., Order Service, Product Service):

### 1. Reference Shared.Testing

```xml
<ItemGroup>
  <ProjectReference Include="..\..\Shared\IhsanDev.Shared.Testing\IhsanDev.Shared.Testing.csproj" />
</ItemGroup>
```

### 2. Import Helper

```csharp
using IhsanDev.Shared.Testing.Helpers;
```

### 3. Use Helper Methods

```csharp
var userId = TenantTestHelper.GenerateUniqueUserId();
var tenantId = TenantTestHelper.GenerateUniqueTenantId("order-service");
```

### 4. Implement Toggle Pattern

```csharp
var isTenantEnabled = configuration.GetValue<bool>("UseTenantSettings");
if (isTenantEnabled)
{
    // Load tenant-specific settings
}
else
{
    // Load default settings
}
```

## Key Learning Points

1. **Reusability**: Shared helpers eliminate code duplication
2. **Thread Safety**: ID generators use `Interlocked.Increment` for concurrency
3. **Flexibility**: Toggle pattern supports multiple project types
4. **Clean Tests**: Minimal, focused tests with clear intent
5. **Scalability**: Easy to extend for new services

## See Also

- [Identity.API.Tests](../../../Services/Identity/Identity.API.Tests/README.md) - Original test patterns
- [Tenant.API.Tests](../../../Services/Tenant/Tenant.API.Tests/README.md) - Tenant-specific tests
- [Shared.Testing](../../IhsanDev.Shared.Testing/README.md) - Base testing infrastructure
