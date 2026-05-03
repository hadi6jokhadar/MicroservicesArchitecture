using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.UpdateSong;

public class UpdateSongCommandHandler : IRequestHandler<UpdateSongCommand, SongDto>
{
    private readonly ISongRepository _repository;
    private readonly ISongIngestionJobRepository _jobRepository;
    private readonly ILogger<UpdateSongCommandHandler> _logger;

    public UpdateSongCommandHandler(
        ISongRepository repository,
        ISongIngestionJobRepository jobRepository,
        ILogger<UpdateSongCommandHandler> logger)
    {
        _repository = repository;
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<SongDto> Handle(UpdateSongCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Song with Id '{request.Id}' not found.");

        var titleChanged = !string.IsNullOrWhiteSpace(request.Title) && !string.Equals(request.Title, entity.Title, StringComparison.Ordinal);

        entity.UpdateTitle(request.Title);

        if (request.ArtistId.HasValue && request.ArtistId != entity.ArtistId)
        {
            throw new InvalidOperationException("Changing artist on an existing song is not supported. Archive the song and create a new one.");
        }

        await _repository.UpdateAsync(entity, cancellationToken);

        if (titleChanged)
        {
            await QueueEmbeddingGenerationAsync(entity, cancellationToken);
        }

        _logger.LogInformation("Updated Song Id {Id}", entity.Id);
        return SongDto.MapFrom(entity);
    }

    private async Task QueueEmbeddingGenerationAsync(SongEntity song, CancellationToken cancellationToken)
    {
        var hasActiveEmbeddingJob = await _jobRepository.HasActiveJobAsync(song.Id, IngestionJobType.EmbeddingGeneration, cancellationToken);
        if (hasActiveEmbeddingJob)
        {
            return;
        }

        var embeddingJob = SongIngestionJobEntity.Create(song.Id, song.FileId, IngestionJobType.EmbeddingGeneration);
        await _jobRepository.AddAsync(embeddingJob, cancellationToken);

        song.SetSearchIndexStatus(SearchIndexStatus.Indexing);
        await _repository.UpdateAsync(song, cancellationToken);

        _logger.LogInformation("Queued embedding job {JobId} for updated song {SongId}.", embeddingJob.Id, song.Id);
    }
}
