using IhsanDev.Shared.Application.Exceptions;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Entities;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.API.Tests.Infrastructure;

/// <summary>
/// Base class for Nasheed API integration tests.
/// Inherits shared helpers from the platform testing library and adds
/// Nasheed-specific entity creation shortcuts.
/// </summary>
public abstract class IntegrationTestBase :
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<NasheedDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ── Artist helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Directly inserts an <see cref="ArtistEntity"/> into the test database,
    /// bypassing the application layer. Useful for setting up pre-conditions.
    /// </summary>
    protected async Task<ArtistEntity> CreateTestArtistAsync(string? name = null, string? imageFileId = null)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var artist = ArtistEntity.Create(name ?? GenerateUniqueString("Artist"), imageFileId);
            context.Artists.Add(artist);
            await context.SaveChangesAsync();
            return artist;
        });
    }

    /// <summary>
    /// Creates an artist via MediatR (exercises the full application layer).
    /// </summary>
    protected async Task<ArtistDto> CreateArtistViaCommandAsync(string? name = null, string? imageFileId = null)
    {
        return await SendAsync(new CreateArtistCommand(
            Name: name ?? GenerateUniqueString("Artist"),
            ImageFileId: imageFileId));
    }

    // ── Song helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Directly inserts a <see cref="SongEntity"/> into the test database,
    /// bypassing the application layer. Useful for setting up pre-conditions
    /// without triggering ingestion jobs.
    /// </summary>
    protected async Task<SongEntity> CreateTestSongAsync(int artistId, string? title = null, string? fileId = null)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var song = SongEntity.Create(
                artistId,
                title ?? GenerateUniqueString("Song"),
                fileId ?? $"file-{Guid.NewGuid():N}");
            context.Songs.Add(song);
            await context.SaveChangesAsync();
            return song;
        });
    }

    /// <summary>
    /// Creates a song via MediatR (exercises the full application layer including
    /// ingestion job creation). The background worker is suppressed in tests, so the
    /// job remains in the <c>Queued</c> state.
    /// </summary>
    protected async Task<SongDto> CreateSongViaCommandAsync(int artistId, string? title = null, string? fileId = null)
    {
        return await SendAsync(new CreateSongCommand(
            ArtistId: artistId,
            Title: title ?? GenerateUniqueString("Song"),
            FileId: fileId ?? $"file-{Guid.NewGuid():N}"));
    }
}
