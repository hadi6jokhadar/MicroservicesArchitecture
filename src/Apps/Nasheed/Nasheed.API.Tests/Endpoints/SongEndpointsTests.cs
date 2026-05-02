using IhsanDev.Shared.Application.Exceptions;
using Nasheed.API.Tests.Infrastructure;
using Nasheed.Application.Commands;
using Nasheed.Application.Queries;
using Nasheed.Domain.Enums;

namespace Nasheed.API.Tests.Endpoints;

/// <summary>
/// Integration tests for song management — CreateSong, UpdateSong,
/// DeleteSong, GetSongById, GetSongList.
///
/// The ingestion background worker is suppressed in tests, so created songs
/// remain in the <c>InQueue</c> state and no AI calls are made.
/// </summary>
[Collection("Sequential")]
public class SongEndpointsTests : IntegrationTestBase
{
    public SongEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── CreateSong ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSong_WithValidData_ShouldReturnSongWithId()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();

        // Act
        var result = await SendAsync(new CreateSongCommand(
            ArtistId: artist.Id,
            Title: GenerateUniqueString("Song"),
            FileId: $"file-{Guid.NewGuid():N}"));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.ArtistId.Should().Be(artist.Id);
        result.SongState.Should().Be(SongState.InQueue);
    }

    [Fact]
    public async Task CreateSong_ShouldIncrementArtistSongCount()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();

        // Act
        await SendAsync(new CreateSongCommand(
            ArtistId: artist.Id,
            Title: GenerateUniqueString("Song"),
            FileId: $"file-{Guid.NewGuid():N}"));

        // Assert — query the artist to confirm count incremented
        var updatedArtist = await SendAsync(new GetArtistByIdQuery(artist.Id));
        updatedArtist!.SongCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateSong_ShouldCreateIngestionJob()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var fileId = $"file-{Guid.NewGuid():N}";

        // Act
        await SendAsync(new CreateSongCommand(
            ArtistId: artist.Id,
            Title: GenerateUniqueString("Song"),
            FileId: fileId));

        // Assert — ingestion job should exist in DB
        var jobExists = await ExecuteDbContextAsync(async ctx =>
            await System.Threading.Tasks.Task.FromResult(
                ctx.SongIngestionJobs.Any(j => j.FileId == fileId)));

        jobExists.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSong_WhenArtistDoesNotExist_ShouldThrowNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new CreateSongCommand(
                ArtistId: int.MaxValue,
                Title: "Orphan Song",
                FileId: $"file-{Guid.NewGuid():N}")));
    }

    [Fact]
    public async Task CreateSong_WithEmptyTitle_ShouldThrowValidationException()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var command = new CreateSongCommand(
            ArtistId: artist.Id,
            Title: string.Empty,
            FileId: $"file-{Guid.NewGuid():N}");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(command));
    }

    [Fact]
    public async Task CreateSong_WithEmptyFileId_ShouldThrowValidationException()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var command = new CreateSongCommand(
            ArtistId: artist.Id,
            Title: GenerateUniqueString("Song"),
            FileId: string.Empty);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(command));
    }

    [Fact]
    public async Task CreateSong_WithTitleExceedingMaxLength_ShouldThrowValidationException()
    {
        // Arrange — Title validator has MaximumLength(500)
        var artist = await CreateTestArtistAsync();
        var command = new CreateSongCommand(
            ArtistId: artist.Id,
            Title: new string('S', 501),
            FileId: $"file-{Guid.NewGuid():N}");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(command));
    }

    // ── GetSongById ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSongById_WhenSongExists_ShouldReturnSong()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var song = await CreateTestSongAsync(artist.Id);

        // Act
        var result = await SendAsync(new GetSongByIdQuery(song.Id));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(song.Id);
        result.ArtistId.Should().Be(artist.Id);
        result.Title.Should().Be(song.Title);
    }

    [Fact]
    public async Task GetSongById_WhenSongDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await SendAsync(new GetSongByIdQuery(int.MaxValue));

        // Assert
        result.Should().BeNull();
    }

    // ── GetSongList ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSongList_WithNoFilter_ShouldReturnPaginatedResults()
    {
        // Arrange — ensure at least one song exists
        var artist = await CreateTestArtistAsync();
        await CreateTestSongAsync(artist.Id);

        // Act
        var result = await SendAsync(new GetSongListQuery(PageNumber: 1, PageSize: 10));

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSongList_FilterByArtistId_ShouldReturnOnlySongsForThatArtist()
    {
        // Arrange
        var artistA = await CreateTestArtistAsync();
        var artistB = await CreateTestArtistAsync();
        await CreateTestSongAsync(artistA.Id, title: "ArtistA-Song");
        await CreateTestSongAsync(artistB.Id, title: "ArtistB-Song");

        // Act
        var result = await SendAsync(new GetSongListQuery(ArtistId: artistA.Id, PageSize: 50));

        // Assert
        result.Items.Should().OnlyContain(s => s.ArtistId == artistA.Id);
    }

    [Fact]
    public async Task GetSongList_FilterByTextFilter_ShouldReturnMatchingSongs()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var uniqueTitle = $"UniqueTitle-{Guid.NewGuid():N}";
        await CreateTestSongAsync(artist.Id, title: uniqueTitle);

        // Act
        var result = await SendAsync(new GetSongListQuery(TextFilter: uniqueTitle));

        // Assert
        result.Items.Should().ContainSingle(s => s.Title == uniqueTitle);
    }

    // ── UpdateSong ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSong_WithValidTitle_ShouldReturnUpdatedSong()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var song = await CreateTestSongAsync(artist.Id);
        var newTitle = GenerateUniqueString("UpdatedSong");

        // Act
        var result = await SendAsync(new UpdateSongCommand(
            Id: song.Id,
            Title: newTitle,
            ArtistId: null));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(song.Id);
        result.Title.Should().Be(newTitle);
    }

    [Fact]
    public async Task UpdateSong_WhenSongDoesNotExist_ShouldThrowNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new UpdateSongCommand(Id: int.MaxValue, Title: "Ghost", ArtistId: null)));
    }

    [Fact]
    public async Task UpdateSong_ChangingArtistId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var artistA = await CreateTestArtistAsync();
        var artistB = await CreateTestArtistAsync();
        var song = await CreateTestSongAsync(artistA.Id);

        // Act & Assert — moving songs between artists is not supported
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SendAsync(new UpdateSongCommand(Id: song.Id, Title: null, ArtistId: artistB.Id)));
    }

    [Fact]
    public async Task UpdateSong_WithInvalidId_ShouldThrowValidationException()
    {
        // Arrange — Id must be > 0
        var command = new UpdateSongCommand(Id: 0, Title: "Title", ArtistId: null);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(command));
    }

    // ── DeleteSong ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSong_WhenSongExists_ShouldReturnTrue()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var song = await CreateTestSongAsync(artist.Id);

        // Act
        var result = await SendAsync(new DeleteSongCommand(song.Id));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteSong_WhenSongDoesNotExist_ShouldThrowNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new DeleteSongCommand(int.MaxValue)));
    }

    [Fact]
    public async Task DeleteSong_ShouldDecrementArtistSongCount()
    {
        // Arrange — create artist and song via commands so the song count increments
        var artist = await CreateArtistViaCommandAsync();
        var song = await CreateSongViaCommandAsync(artist.Id);

        // Act
        await SendAsync(new DeleteSongCommand(song.Id));

        // Assert
        var updatedArtist = await SendAsync(new GetArtistByIdQuery(artist.Id));
        updatedArtist!.SongCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteSong_ShouldMakeSongUnretrievable()
    {
        // Arrange
        var artist = await CreateTestArtistAsync();
        var song = await CreateTestSongAsync(artist.Id);

        // Act
        await SendAsync(new DeleteSongCommand(song.Id));

        // Assert
        var afterDelete = await SendAsync(new GetSongByIdQuery(song.Id));
        afterDelete.Should().BeNull();
    }
}
