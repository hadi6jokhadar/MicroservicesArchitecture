using IhsanDev.Shared.Application.Exceptions;
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
            ?? throw new NotFoundException($"Song with Id '{request.SongId}' not found.");

        var job = SongIngestionJobEntity.Create(song.Id, song.FileId, IngestionJobType.EmbeddingGeneration);
        await _jobRepository.AddAsync(job, cancellationToken);

        song.SetSearchIndexStatus(SearchIndexStatus.Indexing);
        await _songRepository.UpdateAsync(song, cancellationToken);

        _logger.LogInformation("Queued reindex job {JobId} for Song {SongId}", job.Id, song.Id);
        return IngestionJobDto.MapFrom(job);
    }
}
