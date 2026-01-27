# Translation Service Integration Tests

This project contains comprehensive integration tests for the Translation Service using xUnit, FluentAssertions, and MediatR.

## Overview

The Translation Service manages multi-language translations with optional tenant-specific overrides. It uses a **global database** (not multi-tenant per database) and stores translations for all tenants in one database.

## Test Structure

```
Translation.API.Tests/
├── Infrastructure/
│   ├── CustomWebApplicationFactory.cs    # Test server setup
│   ├── IntegrationTestBase.cs           # Base class with helper methods
│   └── SequentialCollectionDefinition.cs # Sequential test execution
├── Endpoints/
│   └── TranslationEndpointsTests.cs     # All endpoint tests
├── GlobalUsings.cs                       # Global using statements
└── Translation.API.Tests.csproj         # Test project configuration
```

## Test Approach

### MediatR Handler Testing (Recommended)

Tests call MediatR handlers directly instead of making HTTP requests. This approach:

- ✅ Bypasses .NET 9.0 PipeWriter serialization issues
- ✅ Provides faster test execution
- ✅ Gives clearer error messages
- ✅ Tests business logic directly

Example:

```csharp
var query = new GetTranslationsQuery("en");
var result = await SendAsync(query); // Calls handler via MediatR
```

## Database Configuration

### PostgreSQL (Recommended for Production-Like Tests)

Set in `CustomWebApplicationFactory` constructor:

```csharp
UsePostgreSQL = true;
```

Requires PostgreSQL running on `localhost:5432` with database `translation_test`.

### SQLite (Default for Quick Tests)

Set in `CustomWebApplicationFactory` constructor:

```csharp
UsePostgreSQL = false;
```

Uses in-memory SQLite database (no setup required).

## Test Categories

### 1. GetTranslations Tests

- Get translations for specific language
- Filter by category
- Tenant-specific overrides
- Inactive key filtering
- Global vs tenant value priority

### 2. CreateTranslationKey Tests

- Create new translation keys
- Duplicate key detection
- Validation (empty key, empty category, max length)

### 3. SetTranslation Tests

- Create new translation values
- Update existing values
- Tenant-specific values
- Non-existent key handling
- Multiple updates to same key

### 4. ImportTranslations Tests

- Bulk import from JSON
- Overwrite vs skip existing
- Tenant-specific imports
- Empty data handling

### 5. Edge Cases

- Inactive keys should not appear in results
- Multiple updates keep latest value
- Tenant overrides prioritize over global

## Running Tests

### All Tests

```bash
cd src/Services/Translation/Translation.API.Tests
dotnet test
```

### Specific Test

```bash
dotnet test --filter "FullyQualifiedName~GetTranslations_ForEnglish_ShouldReturnAllEnglishTranslations"
```

### With Detailed Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Data Seeding

### Automatic Seeding

`CustomWebApplicationFactory.SeedTestData()` creates:

- **Translation Keys:**
  - `test_key_1` (category: general)
  - `test_key_2` (category: errors)

- **Translation Values:**
  - `test_key_1`: "Test Key 1 English" (en), "مفتاح الاختبار 1" (ar)
  - `test_key_2`: "Test Key 2 English" (en)

### Helper Methods

`IntegrationTestBase` provides:

```csharp
// Create translation key
var key = await CreateTestTranslationKeyAsync("my_key", "general");

// Create translation value
var value = await CreateTestTranslationValueAsync(key.Id, "en", "Hello");

// Create complete translation set
var (key, values) = await CreateCompleteTranslationAsync(
    "greeting",
    "general",
    new Dictionary<string, string> { { "en", "Hello" }, { "ar", "مرحبا" } }
);
```

## Key Features Tested

### Multi-Language Support

- English (en), Arabic (ar), and custom languages
- Language-specific value retrieval

### Tenant Overrides

- Global translations (TenantId = null)
- Tenant-specific overrides (TenantId = "tenant-xyz")
- Proper priority: Tenant > Global

### Category Filtering

- Group translations by category (ui, errors, general, etc.)
- Filter results by category

### Bulk Import

- Import multiple keys and languages at once
- Overwrite or skip existing values
- Error tracking during import

## Test Patterns

### Arrange-Act-Assert

All tests follow AAA pattern:

```csharp
// Arrange
var command = new CreateTranslationKeyCommand(...);

// Act
var result = await SendAsync(command);

// Assert
result.Should().NotBeNull();
result.Key.Should().Be("expected_key");
```

### Database Verification

Tests verify database state after operations:

```csharp
var keyFromDb = await ExecuteDbContextAsync(async context =>
{
    return await context.TranslationKeys
        .FirstOrDefaultAsync(k => k.Key == "test_key");
});

keyFromDb.Should().NotBeNull();
```

## Configuration

### Test Configuration

Set in `CustomWebApplicationFactory.GetTestConfiguration()`:

```csharp
config["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-characters-long";
config["MultiTenancy:Enabled"] = "false"; // Translation uses global DB
config["Localization:DefaultCulture"] = "en";
config["RateLimiting:Enabled"] = "false"; // Disable for tests
```

## Common Issues & Solutions

### Issue: Tests fail with "duplicate key" errors

**Solution:** Tests run sequentially (via `[Collection("Sequential")]`) to prevent database conflicts.

### Issue: DateTime comparison failures

**Solution:** All DateTime values converted to UTC before comparison. Npgsql legacy timestamp behavior disabled.

### Issue: PostgreSQL connection errors

**Solution:**

1. Ensure PostgreSQL is running
2. Create database: `createdb translation_test`
3. Or switch to SQLite: `UsePostgreSQL = false`

## Dependencies

- **xUnit** - Test framework
- **FluentAssertions** - Assertion library
- **Microsoft.AspNetCore.Mvc.Testing** - Integration test infrastructure
- **Microsoft.EntityFrameworkCore.InMemory** - In-memory database for SQLite tests
- **Moq** - Mocking framework (for future use)
- **IhsanDev.Shared.Testing** - Shared testing utilities

## Best Practices

1. ✅ Use descriptive test names: `Method_Scenario_ExpectedBehavior`
2. ✅ One assertion focus per test
3. ✅ Clean test data using helper methods
4. ✅ Verify both return values and database state
5. ✅ Test edge cases and error conditions
6. ✅ Use sequential execution to avoid conflicts

## Future Enhancements

- [ ] Add performance tests for bulk import
- [ ] Add caching behavior tests
- [ ] Add concurrent translation update tests
- [ ] Add authentication/authorization tests (when implemented)
- [ ] Add load tests for translation retrieval

## Related Documentation

- [Translation Service Guide](../TRANSLATION_SERVICE_GUIDE.md) _(to be created)_
- [Shared Testing Infrastructure](../../../Shared/IhsanDev.Shared.Testing/README.md)
- [Database Per Tenant Architecture](../../../../Doc/DATABASE_PER_TENANT_ARCHITECTURE.md)

---

**Last Updated:** January 26, 2026
