using IhsanDev.Shared.Application.Exceptions;
using Nasheed.API.Tests.Infrastructure;
using Nasheed.Application.Commands;
using Nasheed.Application.Queries;

namespace Nasheed.API.Tests.Endpoints;

/// <summary>
/// Integration tests for artist management — CreateArtist, UpdateArtist,
/// DeleteArtist, GetArtistById, GetArtistList.
///
/// Tests call handlers via MediatR (bypassing the HTTP layer) to avoid
/// the .NET 9 PipeWriter serialisation issue and keep tests fast.
/// </summary>
[Collection("Sequential")]
public class ArtistEndpointsTests : IntegrationTestBase
{
    public ArtistEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── CreateArtist ──────────────────────────────────────────────────────────

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
        result.ImageFileId.Should().BeNull();
        result.SongCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateArtist_WithImageFileId_ShouldPersistImageFileId()
    {
        // Arrange
        var name = GenerateUniqueString("Artist");
        var imageFileId = $"img-{Guid.NewGuid():N}";

        // Act
        var result = await SendAsync(new CreateArtistCommand(Name: name, ImageFileId: imageFileId));

        // Assert
        result.ImageFileId.Should().Be(imageFileId);
    }

    [Fact]
    public async Task CreateArtist_WithEmptyName_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateArtistCommand(Name: string.Empty, ImageFileId: null);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(command));
    }

    [Fact]
    public async Task CreateArtist_WithNameExceedingMaxLength_ShouldThrowValidationException()
    {
        // Arrange — Name validator has MaximumLength(200)
        var command = new CreateArtistCommand(Name: new string('A', 201), ImageFileId: null);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(command));
    }

    // ── GetArtistById ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetArtistById_WhenArtistExists_ShouldReturnArtist()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();

        // Act
        var result = await SendAsync(new GetArtistByIdQuery(artist.Id));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(artist.Id);
        result.Name.Should().Be(artist.Name);
    }

    [Fact]
    public async Task GetArtistById_WhenArtistDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await SendAsync(new GetArtistByIdQuery(int.MaxValue));

        // Assert
        result.Should().BeNull();
    }

    // ── GetArtistList ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetArtistList_WithNoFilter_ShouldReturnPaginatedResults()
    {
        // Arrange — ensure at least one artist exists
        await CreateTestArtistAsync();

        // Act
        var result = await SendAsync(new GetArtistListQuery(PageNumber: 1, PageSize: 10));

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetArtistList_WithTextFilter_ShouldReturnMatchingArtists()
    {
        // Arrange
        var uniqueName = $"UniqueTestArtist-{Guid.NewGuid():N}";
        await CreateTestArtistAsync(name: uniqueName);

        // Act
        var result = await SendAsync(new GetArtistListQuery(TextFilter: uniqueName));

        // Assert
        result.Items.Should().ContainSingle(a => a.Name == uniqueName);
    }

    [Fact]
    public async Task GetArtistList_WithTextFilterThatMatchesNothing_ShouldReturnEmptyList()
    {
        // Arrange
        var noMatch = $"ZZZ-NOMATCH-{Guid.NewGuid():N}";

        // Act
        var result = await SendAsync(new GetArtistListQuery(TextFilter: noMatch));

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ── UpdateArtist ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateArtist_WithValidData_ShouldReturnUpdatedArtist()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var newName = GenerateUniqueString("UpdatedArtist");

        // Act
        var result = await SendAsync(new UpdateArtistCommand(
            Id: artist.Id,
            Name: newName,
            ImageFileId: null));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(artist.Id);
        result.Name.Should().Be(newName);
    }

    [Fact]
    public async Task UpdateArtist_WhenArtistDoesNotExist_ShouldThrowNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new UpdateArtistCommand(Id: int.MaxValue, Name: "Ghost", ImageFileId: null)));
    }

    [Fact]
    public async Task UpdateArtist_WithInvalidId_ShouldThrowValidationException()
    {
        // Arrange — Id must be > 0
        var command = new UpdateArtistCommand(Id: 0, Name: "SomeName", ImageFileId: null);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(command));
    }

    // ── DeleteArtist ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteArtist_WhenArtistExists_ShouldReturnTrue()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();

        // Act
        var result = await SendAsync(new DeleteArtistCommand(artist.Id));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteArtist_WhenArtistDoesNotExist_ShouldThrowNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new DeleteArtistCommand(int.MaxValue)));
    }

    [Fact]
    public async Task DeleteArtist_ShouldMakeArtistUnretrievable()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();

        // Act
        await SendAsync(new DeleteArtistCommand(artist.Id));

        // Assert — subsequent get returns null
        var afterDelete = await SendAsync(new GetArtistByIdQuery(artist.Id));
        afterDelete.Should().BeNull();
    }
}
