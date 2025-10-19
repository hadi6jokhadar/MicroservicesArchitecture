# Tenant.API.Tests

Integration tests for the Tenant Service API using xUnit and FluentAssertions.

## Overview

This test project contains comprehensive integration tests for the Tenant Service, covering all endpoints and business logic scenarios. Tests use the MediatR pattern to call handlers directly, bypassing HTTP serialization to avoid .NET 9.0 PipeWriter issues.

## Test Architecture

### Test Infrastructure

- **CustomWebApplicationFactory**: Configures test environment with in-memory database and JWT settings
- **IntegrationTestBase**: Base class providing helper methods for creating test data
- **SequentialCollectionDefinition**: Forces sequential test execution to prevent database conflicts

### Test Organization

```
Endpoints/
├── TenantEndpointsTests.cs          # Public tenant endpoints
└── AdminTenantEndpointsTests.cs     # Admin-only management endpoints
```

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~TenantEndpointsTests"
dotnet test --filter "FullyQualifiedName~AdminTenantEndpointsTests"
```

### Run with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Patterns

### MediatR Handler Testing (Bypasses HTTP Layer)

```csharp
[Fact]
public async Task CreateTenant_WithValidData_ShouldReturnCreatedTenant()
{
    // Arrange
    var createCommand = new CreateTenantCommand(/* ... */);

    // Act - Call handler directly via MediatR
    var result = await SendAsync(createCommand);

    // Assert
    result.Should().NotBeNull();
    result.TenantId.Should().Be("expected-id");
}
```

### Test Data Creation

```csharp
// Create test tenant directly in database
var tenant = await CreateTestTenantAsync(
    tenantId: "custom-id",
    userId: 123,
    isActive: true
);

// Create via MediatR command
var tenant = await CreateTenantViaCommandAsync(
    tenantId: "custom-id",
    userId: 123
);
```

## Test Categories

### TenantEndpointsTests (Public Endpoints)

**Create Tenant Tests**

- ✅ Create with valid data
- ✅ Duplicate TenantId validation
- ✅ Duplicate UserId validation
- ✅ Invalid JSON validation
- ✅ Expired date handling
- ✅ Empty TenantId validation
- ✅ Date range validation
- ✅ TenantId pattern validation

**Get Tenant Tests**

- ✅ Get by TenantId (valid/invalid)
- ✅ Get by UserId (valid/invalid)
- ✅ Get tenant configuration

**Update Tenant Tests**

- ✅ Update with valid data
- ✅ Update non-existent tenant
- ✅ Update with invalid JSON

**Delete Tenant Tests**

- ✅ Soft delete valid tenant
- ✅ Delete non-existent tenant

**Get All Active Tenants Tests**

- ✅ Paginated results
- ✅ Pagination with multiple pages
- ✅ Empty database handling

### AdminTenantEndpointsTests (Admin-Only Operations)

**Admin Get All Active Tenants**

- ✅ Get all active tenants (excludes inactive)
- ✅ Large page size handling
- ✅ Small page size pagination
- ✅ Page deduplication validation

**Admin Get Tenant By User**

- ✅ Valid UserId lookup
- ✅ Non-existent UserId
- ✅ Inactive tenant retrieval

**Admin Create Tenant**

- ✅ Create with valid data
- ✅ Complex JSON configuration persistence

**Admin Update Tenant**

- ✅ Update all fields
- ✅ Toggle active state
- ✅ Extend expiration (updates IsExpired)
- ✅ Minimal field changes

**Admin Delete Tenant**

- ✅ Soft delete tenant
- ✅ Delete active tenant
- ✅ Recreate after deletion

**Admin Bulk Operations**

- ✅ Bulk tenant creation

**Admin Edge Cases**

- ✅ Get sensitive configuration data
- ✅ Partial update validation

## Key Features

### Database Configuration

Tests support both **SQLite in-memory** (default) and **PostgreSQL**:

```csharp
public TenantEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
{
    factory.UsePostgreSQL = true; // Switch to PostgreSQL
}
```

### Sequential Execution

All test classes use `[Collection("Sequential")]` to prevent parallel execution and database conflicts:

```csharp
[Collection("Sequential")]
public class TenantEndpointsTests : IntegrationTestBase
{
    // Tests run sequentially, not in parallel
}
```

### Helper Methods

**Generate Unique IDs**

```csharp
var tenantId = GenerateUniqueTenantId(); // Returns "test-tenant-{guid}"
```

**Create Test Data**

```csharp
var tenant = await CreateTestTenantAsync(
    tenantId: "custom-id",
    tenantName: "My Tenant",
    userId: 123,
    isActive: true,
    data: "{\"Jwt\":{\"Secret\":\"test\"}}"
);
```

## Dependencies

- **xUnit 2.9.2** - Test framework
- **FluentAssertions 7.0.0** - Fluent assertion library
- **Moq 4.20.72** - Mocking framework
- **Microsoft.AspNetCore.Mvc.Testing 9.0.1** - Integration testing infrastructure
- **Microsoft.EntityFrameworkCore.InMemory 9.0.0** - In-memory database for tests
- **IhsanDev.Shared.Testing** - Shared test infrastructure

## Best Practices

1. **Use MediatR SendAsync**: Always call handlers directly via `SendAsync(command)` to bypass HTTP layer
2. **Sequential Execution**: All tests in same collection run sequentially to prevent conflicts
3. **Generate Unique IDs**: Use `GenerateUniqueTenantId()` to avoid conflicts across tests
4. **Test Database Isolation**: Each test should create its own test data
5. **FluentAssertions**: Use `.Should()` syntax for readable assertions
6. **Arrange-Act-Assert**: Follow AAA pattern for clarity

## Configuration

### Test Database

CustomWebApplicationFactory configures test database automatically:

- **SQLite In-Memory**: Default, fast, isolated
- **PostgreSQL**: Optional, set `factory.UsePostgreSQL = true`

### Multi-Tenancy

Multi-tenancy is **disabled** in test environment for simplicity:

```csharp
configuration["MultiTenancy:Enabled"] = "false";
```

### JWT Settings

JWT authentication is configured with test values in `CustomWebApplicationFactory`.

## Troubleshooting

### Tests Failing with Database Conflicts

- Ensure all test classes use `[Collection("Sequential")]`
- Use `GenerateUniqueTenantId()` for unique identifiers

### PipeWriter or HTTP Serialization Errors

- Use MediatR `SendAsync(command)` instead of HTTP client
- This bypasses .NET 9.0 serialization bugs

### Connection String Issues

- Check `CustomWebApplicationFactory` database configuration
- Verify `UsePostgreSQL` flag matches your setup

## Coverage

Run tests with coverage reporting:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

View coverage report:

```bash
dotnet reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport"
```

## Shared Helper Integration

The test suite includes **reusable shared helpers** for cross-service testing:

- **Location**: `src/Shared/IhsanDev.Shared.Testing/Helpers/TenantTestHelper.cs`
- **Purpose**: Reusable tenant operations for any service
- **Tests**: 4 integration tests demonstrating usage patterns

### Shared Helper Features

```csharp
using IhsanDev.Shared.Testing.Helpers;

// Generate unique IDs
var userId = TenantTestHelper.GenerateUniqueUserId();
var tenantId = TenantTestHelper.GenerateUniqueTenantId("prefix");

// Create user and tenant (HTTP-based)
var (userId, tenantId, responseId) = await TenantTestHelper.CreateUserAndTenantAsync(httpClient);

// Get tenant by ID
var tenant = await TenantTestHelper.GetTenantByIdAsync(httpClient, tenantId);

// Check tenant support
var isEnabled = await TenantTestHelper.IsTenantEnabledAsync(httpClient);
```

### Integration Tests Location

`Integration/SharedHelperIntegrationTests.cs` - Demonstrates:

- ✅ Basic flow (Create user → Create tenant → Get tenant)
- ✅ Tenant-enabled project scenario (Project B)
- ✅ Non-tenant project scenario (Project A)
- ✅ Toggle pattern for conditional tenant loading

**Test Results**: 42/42 tests passing (100% pass rate)

## Related Documentation

- [Identity.API.Tests](../../Identity/Identity.API.Tests/README.md) - Reference implementation
- [IhsanDev.Shared.Testing](../../../../Shared/IhsanDev.Shared.Testing/README.md) - Shared test infrastructure
- [TenantTestHelper Guide](../../../../Shared/IhsanDev.Shared.Testing/Helpers/README_TENANT_HELPER.md) - Cross-service testing helper
- [Integration Testing Guide](../../../../../INTEGRATION_TESTING_SUMMARY.md) - Overall testing strategy
