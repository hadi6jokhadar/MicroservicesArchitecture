using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.ReindexSong;

public class ReindexSongCommandHandler : IRequestHandler<ReindexSongCommand, IngestionJobDto>
{
    private readonly ISongRepository _songRepository;
    private readonly ISongIngestionJobRepository _jobRepository;
    private readonly ILogger<ReindexSongCommandHandler> _logger;

    public ReindexSongCommandHandler(
        ISongRepository songRepository,
        ISongIngestionJobRepository jobRepository,
        ILogger<ReindexSongCommandHandler> logger)
    {
        _songRepository = songRepository;
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<IngestionJobDto> Handle(ReindexSongCommand request, CancellationToken cancellationToken)
    {
        var song = await _songRepository.GetByIdAsync(request.SongId, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.SongNotFound);

        // Return the existing active job instead of inserting a duplicate.
        // The unique index IX_SongIngestionJobs_ActiveJobUnique enforces one active
        // (Pending/Running) job per (SongId, JobType) at the DB level — this check
        // makes the API idempotent rather than crashing on a concurrent reindex request.
        if (await _jobRepository.HasActiveJobAsync(song.Id, IngestionJobType.EmbeddingGeneration, cancellationToken))
        {
            var existing = await _jobRepository.GetBySongIdAsync(song.Id, cancellationToken);
            if (existing is not null)
            {
                _logger.LogInformation("Reindex job already active {JobId} for Song {SongId}; returning existing", existing.Id, song.Id);
                return IngestionJobDto.MapFrom(existing);
            }
        }

        var job = SongIngestionJobEntity.Create(song.Id, song.FileId, IngestionJobType.EmbeddingGeneration);
        await _jobRepository.AddAsync(job, cancellationToken);

        song.SetSearchIndexStatus(SearchIndexStatus.Indexing);
        await _songRepository.UpdateAsync(song, cancellationToken);

        _logger.LogInformation("Queued reindex job {JobId} for Song {SongId}", job.Id, song.Id);
        return IngestionJobDto.MapFrom(job);
    }
}
