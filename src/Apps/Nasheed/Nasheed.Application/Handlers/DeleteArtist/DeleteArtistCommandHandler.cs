using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.Interfaces;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.DeleteArtist;

public class DeleteArtistCommandHandler : IRequestHandler<DeleteArtistCommand, bool>
{
    private readonly IArtistRepository _repository;
    private readonly ISongRepository _songRepository;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IRatingRepository _ratingRepository;
    private readonly IPlayLogRepository _playLogRepository;
    private readonly ISongMoodTagRepository _songMoodTagRepository;
    private readonly ISongIngestionJobRepository _songIngestionJobRepository;
    private readonly ISongSearchDocumentRepository _songSearchDocumentRepository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly INasheedTenantCache _tenantCache;
    private readonly INasheedUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteArtistCommandHandler> _logger;

    public DeleteArtistCommandHandler(
        IArtistRepository repository,
        ISongRepository songRepository,
        IFavoriteRepository favoriteRepository,
        IRatingRepository ratingRepository,
        IPlayLogRepository playLogRepository,
        ISongMoodTagRepository songMoodTagRepository,
        ISongIngestionJobRepository songIngestionJobRepository,
        ISongSearchDocumentRepository songSearchDocumentRepository,
        IFileManagerServiceClient fileManagerClient,
        INasheedTenantCache tenantCache,
        INasheedUnitOfWork unitOfWork,
        ILogger<DeleteArtistCommandHandler> logger)
    {
        _repository = repository;
        _songRepository = songRepository;
        _favoriteRepository = favoriteRepository;
        _ratingRepository = ratingRepository;
        _playLogRepository = playLogRepository;
        _songMoodTagRepository = songMoodTagRepository;
        _songIngestionJobRepository = songIngestionJobRepository;
        _songSearchDocumentRepository = songSearchDocumentRepository;
        _fileManagerClient = fileManagerClient;
        _tenantCache = tenantCache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteArtistCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.ArtistNotFound);

        var songs = await _songRepository.GetByArtistIdAsync(entity.Id, cancellationToken);

        // All DB deletes happen inside a single transaction so a partial failure leaves no orphans.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var song in songs)
            {
                await _songMoodTagRepository.DeleteBySongIdAsync(song.Id, ct);
                await _songIngestionJobRepository.DeleteBySongIdAsync(song.Id, ct);
                await _songSearchDocumentRepository.DeleteBySongIdAsync(song.Id, ct);
                await _favoriteRepository.DeleteBySongIdAsync(song.Id, ct);
                await _ratingRepository.DeleteBySongIdAsync(song.Id, ct);
                await _playLogRepository.DeleteBySongIdAsync(song.Id, ct);
                await _songRepository.DeleteAsync(song, ct);
            }

            await _repository.DeleteAsync(entity, ct);
        }, cancellationToken);

        _logger.LogInformation("Deleted Artist Id {Id} and {SongCount} songs", entity.Id, songs.Count);

        // FileManager cleanup runs after successful commit — fire-and-warn only.
        var tenantId = _tenantCache.Tenant?.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning("Skipping FileManager cleanup for Artist {ArtistId} because tenant context is unavailable.", entity.Id);
            return true;
        }

        foreach (var song in songs.Where(s => s.FileId > 0))
        {
            var ok = await _fileManagerClient.ChangeTempStatusAsync(song.FileId, "Song", song.Id.ToString(), false, tenantId, cancellationToken);
            if (!ok)
                _logger.LogWarning("Failed to mark FileId {FileId} as temporary after deleting Song {SongId}", song.FileId, song.Id);
        }

        if (entity.ImageFileId.HasValue)
        {
            var ok = await _fileManagerClient.ChangeTempStatusAsync(entity.ImageFileId.Value, "Artist", entity.Id.ToString(), false, tenantId, cancellationToken);
            if (!ok)
                _logger.LogWarning("Failed to mark ImageFileId {FileId} as temporary after deleting Artist {ArtistId}", entity.ImageFileId.Value, entity.Id);
        }

        return true;
    }
}
