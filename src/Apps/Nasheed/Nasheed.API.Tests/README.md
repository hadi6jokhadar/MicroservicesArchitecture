# Nasheed.API.Tests

Integration tests for the Nasheed Library Service using xUnit and FluentAssertions.

## Overview

Tests call MediatR handlers directly (bypassing the HTTP layer) to avoid the .NET 9
`PipeWriter` serialisation bug and keep tests fast and deterministic.

External dependencies (TenantService, AI API, Redis, background workers) are either
removed or replaced with Moq stubs in `CustomWebApplicationFactory`.

---

## Project Structure

```
Nasheed.API.Tests/
├── Endpoints/
│   ├── ArtistEndpointsTests.cs   # CreateArtist, GetArtist, UpdateArtist, DeleteArtist
│   └── SongEndpointsTests.cs     # CreateSong, GetSong, UpdateSong, DeleteSong
├── Infrastructure/
│   ├── CustomWebApplicationFactory.cs   # Test host — suppresses workers, mocks AI client
│   ├── IntegrationTestBase.cs           # Base class with entity creation helpers
│   └── SequentialCollectionDefinition.cs # Forces sequential execution
├── GlobalUsings.cs
└── README.md  ← you are here
```

---

## Test Infrastructure

### CustomWebApplicationFactory

Inherits `IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>` and:

| What it does                                                    | Why                                                           |
| --------------------------------------------------------------- | ------------------------------------------------------------- |
| Uses PostgreSQL (`nasheed_testdb`)                              | FK constraints and EF migrations match production             |
| Removes `NasheedTenantLoaderService`                            | It calls TenantService which is not running in tests          |
| Removes `NasheedIngestionWorker`                                | It polls DB and calls AI API which are not available in tests |
| Replaces `IAiApiClient` with `Mock<IAiApiClient>`               | Prevents HTTP calls to AI service                             |
| Replaces `INasheedTenantCache` with `Mock<INasheedTenantCache>` | The real cache waits for the loader service                   |
| Replaces `NasheedDbContext` with a test DB                      | Isolates tests from developer / production data               |
| Sets `MultiTenancy:Enabled = false`                             | Makes `NasheedDbContext` use the direct connection string     |

### IntegrationTestBase

Base class for test classes. Key helper methods:

| Method                             | Description                                                                 |
| ---------------------------------- | --------------------------------------------------------------------------- |
| `SendAsync(IRequest)`              | Execute a MediatR command or query                                          |
| `ExecuteDbContextAsync(...)`       | Run EF Core operations directly against the test DB                         |
| `CreateTestArtistAsync(...)`       | Insert an `ArtistEntity` directly into the DB (bypasses app layer)          |
| `CreateArtistViaCommandAsync(...)` | Create an artist through MediatR (exercises app layer)                      |
| `CreateTestSongAsync(...)`         | Insert a `SongEntity` directly (no ingestion job created)                   |
| `CreateSongViaCommandAsync(...)`   | Create a song through MediatR (ingestion job created but worker suppressed) |
| `GenerateUniqueString(prefix)`     | Produce a unique string to avoid test data collisions                       |

---

## Running Tests

### Prerequisites

- PostgreSQL running locally (`localhost:5432`, user `postgres`, password `CHANGE_ME_DB_PASSWORD`)
- The test database `nasheed_testdb` will be created automatically on first run

### All tests

```powershell
cd MicroservicesArchitecture
dotnet test src/Apps/Nasheed/Nasheed.API.Tests/Nasheed.API.Tests.csproj
```

### Specific class

```powershell
dotnet test --filter "FullyQualifiedName~ArtistEndpointsTests"
dotnet test --filter "FullyQualifiedName~SongEndpointsTests"
```

### With coverage

```powershell
dotnet test src/Apps/Nasheed/Nasheed.API.Tests/Nasheed.API.Tests.csproj --collect:"XPlat Code Coverage"
```

---

## Test Patterns

### Happy-path CRUD test

```csharp
[Fact]
public async Task CreateArtist_WithValidData_ShouldReturnArtistWithId()
{
    // Arrange
    var name = GenerateUniqueString("Artist");

    // Act
    var result = await SendAsync(new CreateArtistCommand(Name: name, ImageFileId: null));

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().BeGreaterThan(0);
    result.Name.Should().Be(name);
}
```

### Not-found test

```csharp
[Fact]
public async Task UpdateArtist_WhenArtistDoesNotExist_ShouldThrowNotFoundException()
{
    await Assert.ThrowsAsync<NotFoundException>(() =>
        SendAsync(new UpdateArtistCommand(Id: int.MaxValue, Name: "Ghost", ImageFileId: null)));
}
```

### Validation test

```csharp
[Fact]
public async Task CreateArtist_WithEmptyName_ShouldThrowValidationException()
{
    await Assert.ThrowsAnyAsync<Exception>(() =>
        SendAsync(new CreateArtistCommand(Name: string.Empty, ImageFileId: null)));
}
```

### Side-effect test (artist song count)

```csharp
[Fact]
public async Task CreateSong_ShouldIncrementArtistSongCount()
{
    var artist = await CreateTestArtistAsync();
    await SendAsync(new CreateSongCommand(ArtistId: artist.Id, Title: "My Song", FileId: "file-123"));

    var updatedArtist = await SendAsync(new GetArtistByIdQuery(artist.Id));
    updatedArtist!.SongCount.Should().Be(1);
}
```

---

## Adding New Tests

1. Create a new `*Tests.cs` file in `Endpoints/`
2. Inherit `IntegrationTestBase` and add `[Collection("Sequential")]`
3. Use `SendAsync(command/query)` to exercise handlers
4. Use `CreateTestArtistAsync()` / `CreateTestSongAsync()` for test setup
5. Use `ExecuteDbContextAsync(ctx => ...)` to assert DB state directly

---

## Known Limitations

| Limitation                        | Reason                                                                |
| --------------------------------- | --------------------------------------------------------------------- |
| Ingestion pipeline not tested     | Worker is suppressed; AI calls are mocked                             |
| Search / AI generation not tested | Requires real AI service; add as separate integration category        |
| HTTP layer not tested             | Avoided due to .NET 9 PipeWriter bug; use `SendAsync` pattern instead |

---

## Related Documentation

- `Doc/NASHEED_LIBRARY_BACKEND.md` — full backend overview
- `Doc/SHARED_TESTING_FILES.md` — shared test base classes
- `src/Shared/IhsanDev.Shared.Testing/README.md` — shared testing library guide
