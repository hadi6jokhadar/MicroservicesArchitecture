using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
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
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.SongNotFound);

        var titleChanged = !string.IsNullOrWhiteSpace(request.Title) && !string.Equals(request.Title, entity.Title, StringComparison.Ordinal);
        var existingRiskLevel = entity.LegalCompliance?.CopyrightRiskLevel;
        var existingSafetyFlag = entity.LegalCompliance?.ContentSafetyFlag;
        var existingRiskReason = entity.LegalCompliance?.RiskReason;

        entity.UpdateTitle(request.Title);
        entity.UpdateLegalComplianceFromAi(request.CopyrightRiskLevel, request.ContentSafetyFlag, request.RiskReason);

        if (request.ArtistId.HasValue && request.ArtistId != entity.ArtistId)
        {
            throw new BadRequestException(LocalizationKeys.Exceptions.SongArtistChangeNotSupported);
        }

        await _repository.UpdateAsync(entity, cancellationToken);

        var legalComplianceChanged =
            !string.Equals(existingRiskLevel, entity.LegalCompliance?.CopyrightRiskLevel, StringComparison.Ordinal) ||
            !string.Equals(existingSafetyFlag, entity.LegalCompliance?.ContentSafetyFlag, StringComparison.Ordinal) ||
            !string.Equals(existingRiskReason, entity.LegalCompliance?.RiskReason, StringComparison.Ordinal);

        if (titleChanged || legalComplianceChanged)
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
