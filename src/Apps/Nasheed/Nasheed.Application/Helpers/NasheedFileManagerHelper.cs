using IhsanDev.Shared.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Nasheed.Application.DTOs;
using Nasheed.Application.Interfaces;

namespace Nasheed.Application.Helpers;

public class NasheedFileManagerHelper
{
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly INasheedTenantCache _tenantCache;
    private readonly ILogger<NasheedFileManagerHelper> _logger;

    public NasheedFileManagerHelper(
        IFileManagerServiceClient fileManagerClient,
        INasheedTenantCache tenantCache,
        ILogger<NasheedFileManagerHelper> logger)
    {
        _fileManagerClient = fileManagerClient;
        _tenantCache = tenantCache;
        _logger = logger;
    }

    /// <summary>
    /// Enriches a single SongDto with its audio file metadata.
    /// </summary>
    public async Task EnrichSongWithFileAsync(SongDto song, CancellationToken cancellationToken = default)
    {
        if (song.FileId <= 0) return;

        try
        {
            song.File = await _fileManagerClient.GetFileByIdAsync(song.FileId, _tenantCache.Tenant!.TenantId, cancellationToken);
            if (song.File == null)
                _logger.LogWarning("File {FileId} not found for Song {SongId}", song.FileId, song.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch file {FileId} for Song {SongId}", song.FileId, song.Id);
        }
    }

    /// <summary>
    /// Enriches a list of SongDtos with audio file metadata using a single batch request.
    /// </summary>
    public async Task EnrichSongsWithFilesAsync(IEnumerable<SongDto> songs, CancellationToken cancellationToken = default)
    {
        var songList = songs.ToList();
        var fileIds = songList.Where(s => s.FileId > 0).Select(s => s.FileId).Distinct().ToList();

        if (fileIds.Count == 0) return;

        try
        {
            var filesDict = await _fileManagerClient.GetFilesByIdsAsync(fileIds, _tenantCache.Tenant!.TenantId, cancellationToken);

            foreach (var song in songList.Where(s => s.FileId > 0))
            {
                if (filesDict.TryGetValue(song.FileId, out var file))
                    song.File = file;
                else
                    _logger.LogWarning("File {FileId} not found for Song {SongId}", song.FileId, song.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch fetch files for {Count} songs", songList.Count);
        }
    }

    /// <summary>
    /// Enriches a single ArtistDto with its image file metadata.
    /// </summary>
    public async Task EnrichArtistWithImageAsync(ArtistDto artist, CancellationToken cancellationToken = default)
    {
        if (!artist.ImageFileId.HasValue) return;

        try
        {
            artist.ImageFile = await _fileManagerClient.GetFileByIdAsync(artist.ImageFileId.Value, _tenantCache.Tenant!.TenantId, cancellationToken);
            if (artist.ImageFile == null)
                _logger.LogWarning("ImageFile {FileId} not found for Artist {ArtistId}", artist.ImageFileId.Value, artist.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch image file {FileId} for Artist {ArtistId}", artist.ImageFileId.Value, artist.Id);
        }
    }

    /// <summary>
    /// Enriches a list of ArtistDtos with image file metadata using a single batch request.
    /// </summary>
    public async Task EnrichArtistsWithImagesAsync(IEnumerable<ArtistDto> artists, CancellationToken cancellationToken = default)
    {
        var artistList = artists.ToList();
        var fileIds = artistList.Where(a => a.ImageFileId.HasValue).Select(a => a.ImageFileId!.Value).Distinct().ToList();

        if (fileIds.Count == 0) return;

        try
        {
            var filesDict = await _fileManagerClient.GetFilesByIdsAsync(fileIds, _tenantCache.Tenant!.TenantId, cancellationToken);

            foreach (var artist in artistList.Where(a => a.ImageFileId.HasValue))
            {
                if (filesDict.TryGetValue(artist.ImageFileId!.Value, out var file))
                    artist.ImageFile = file;
                else
                    _logger.LogWarning("ImageFile {FileId} not found for Artist {ArtistId}", artist.ImageFileId.Value, artist.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch fetch image files for {Count} artists", artistList.Count);
        }
    }
}
