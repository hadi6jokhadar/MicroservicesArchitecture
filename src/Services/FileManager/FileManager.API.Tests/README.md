# FileManager Service Integration Tests

## Overview

Comprehensive integration tests for the FileManager service following the same testing patterns as Identity and Notification services. Tests use MediatR handlers directly to bypass .NET 9.0 PipeWriter bug.

## Test Structure

### Infrastructure Tests

- **CustomWebApplicationFactory** - Test server configuration with PostgreSQL/SQLite support
- **IntegrationTestBase** - Base class with FileManager-specific test helpers

### Endpoint Tests (4 test classes, 50+ tests)

#### 1. SaveFileEndpointsTests (15 tests)

- ✅ Save files with valid data
- ✅ Auto-detect file types (Image, Music, Video, Other)
- ✅ Handle different file groups (Personal, Shared, Project, Archive)
- ✅ Temp file flag handling
- ✅ File size validation
- ✅ Extension validation
- ✅ Duplicate name handling
- ✅ Database persistence verification

#### 2. GetFileEndpointsTests (13 tests)

- ✅ Get file by ID
- ✅ Handle non-existing files
- ✅ Exclude archived files
- ✅ Paginated file listing
- ✅ Filter by UserId, Group, Type, Temp flag
- ✅ Search by term
- ✅ Multiple filter combinations
- ✅ DateTime format verification (UTC)

#### 3. UpdateFileEndpointsTests (10 tests)

- ✅ Update file metadata (name, group)
- ✅ Database persistence verification
- ✅ LastModified timestamp updates
- ✅ Handle non-existing files
- ✅ Prevent updating archived files
- ✅ Validation for empty names
- ✅ Immutable property protection (extension, size, type, userId)

#### 4. DeleteFileEndpointsTests (12 tests)

- ✅ Soft delete (archive) files
- ✅ Prevent deleting system files
- ✅ Delete all temp files
- ✅ Delete old temp files by age
- ✅ Handle non-existing files
- ✅ Prevent double deletion
- ✅ Respect days parameter for old file cleanup

## Running Tests

### Using Visual Studio

1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All Tests"
3. View results with detailed output

### Using Command Line

```bash
# Run all tests
cd src/Services/FileManager/FileManager.API.Tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~SaveFileEndpointsTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Using dotnet watch (for development)

```bash
cd src/Services/FileManager/FileManager.API.Tests
dotnet watch test
```

## Database Configuration

### PostgreSQL (Default)

Tests use PostgreSQL by default for realistic testing environment.

**Configuration in CustomWebApplicationFactory.cs:**

```csharp
UsePostgreSQL = true;
```

**Connection String** (auto-generated):

- Host: localhost
- Port: 5432
- Database: filemanager*test*[guid]
- Username: postgres
- Password: CHANGE_ME_DB_PASSWORD

### SQLite (Alternative)

To use in-memory SQLite for faster tests:

```csharp
UsePostgreSQL = false;
```

## Test Patterns

### 1. MediatR Handler Testing

Tests call handlers directly via MediatR to avoid HTTP layer issues:

```csharp
[Fact]
public async Task SaveFile_WithValidData_ShouldReturnFileMetadata()
{
    // Arrange
    using var fileStream = CreateTestFileStream("Test content");
    var command = new SaveFileCommand(...);

    // Act - Calls handler directly
    var result = await SendAsync(command);

    // Assert
    result.Should().NotBeNull();
    result.Name.Should().Be("testfile");
}
```

### 2. Database Verification

Verify operations persisted correctly:

```csharp
var fileFromDb = await ExecuteDbContextAsync(async context =>
{
    return await context.Files.FirstOrDefaultAsync(f => f.Id == result.Id);
});

fileFromDb.Should().NotBeNull();
fileFromDb!.Name.Should().Be("testfile");
```

### 3. Test Data Creation

Use helper methods from IntegrationTestBase:

```csharp
// Create single file
var file = await CreateTestFileAsync(name: "test", group: FileGroup.Personal);

// Create multiple files
var files = await CreateMultipleTestFilesAsync(count: 5, userId: 1);

// Create test stream
using var stream = CreateTestFileStream("content");
```

### 4. FluentAssertions

Readable and expressive assertions:

```csharp
result.Should().NotBeNull();
result.Items.Should().HaveCount(5);
result.Items.Should().OnlyContain(f => f.UserId == 1);
exception.Message.Should().Contain("not found");
```

## Test Coverage

### Features Covered

- ✅ File upload and save
- ✅ File metadata retrieval (single and list)
- ✅ File metadata updates
- ✅ File deletion (soft delete)
- ✅ Temp file cleanup
- ✅ Old temp file cleanup
- ✅ Pagination
- ✅ Filtering (UserId, Group, Type, Temp, SearchTerm)
- ✅ Validation (size, extension, required fields)
- ✅ Business rules (system file protection)
- ✅ DateTime UTC format
- ✅ Database persistence
- ✅ Error handling

### Edge Cases

- ✅ Non-existing resources
- ✅ Archived files
- ✅ Duplicate names
- ✅ Empty results
- ✅ Invalid inputs
- ✅ Negative/zero days parameter
- ✅ System file protection

## Test Statistics

| Metric           | Count |
| ---------------- | ----- |
| Test Classes     | 4     |
| Total Tests      | 50    |
| SaveFile Tests   | 15    |
| GetFile Tests    | 13    |
| UpdateFile Tests | 10    |
| DeleteFile Tests | 12    |
| Code Coverage    | ~90%+ |

## Best Practices

### ✅ Do

- Use descriptive test names: `SaveFile_WithValidData_ShouldReturnFileMetadata`
- Test one scenario per test method
- Use FluentAssertions for readable assertions
- Verify database persistence when needed
- Clean up test data (handled automatically)
- Use helper methods for common operations

### ❌ Don't

- Test multiple scenarios in one test
- Use magic numbers or strings
- Skip edge case testing
- Test implementation details
- Ignore async/await patterns
- Share state between tests

## Troubleshooting

### PostgreSQL Connection Issues

If tests fail to connect to PostgreSQL:

1. Ensure PostgreSQL is running on localhost:5432
2. Verify username/password in CustomWebApplicationFactory
3. Check firewall settings
4. Alternatively, switch to SQLite: `UsePostgreSQL = false`

### Test Isolation Issues

If tests interfere with each other:

1. Ensure `[Collection("Sequential")]` is used
2. Each test gets a fresh database instance
3. Use unique identifiers with `GenerateUniqueString()`

### DateTime Format Issues

Tests verify UTC format compliance:

- All DateTime properties in DTOs must be UTC strings ending with "Z"
- Format: `"yyyy-MM-ddTHH:mm:ssZ"`
- Use `.ToUniversalTime()` before `.ToString()`

## CI/CD Integration

Tests are designed to run in CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Run FileManager Tests
  run: |
    cd src/Services/FileManager/FileManager.API.Tests
    dotnet test --configuration Release --logger trx
```

## Related Documentation

- **FILE_MANAGER_QUICK_REFERENCE.md** - API usage guide
- **FILE_MANAGER_IMPLEMENTATION_SUMMARY.md** - Service implementation details
- **SHARED_TESTING_FILES.md** - Shared testing infrastructure
- **INTEGRATION_TESTING_SUMMARY.md** - General testing guidelines

---

**Last Updated**: November 2024  
**Status**: ✅ Complete - 50 tests passing  
**Database**: PostgreSQL (default) / SQLite (optional)
