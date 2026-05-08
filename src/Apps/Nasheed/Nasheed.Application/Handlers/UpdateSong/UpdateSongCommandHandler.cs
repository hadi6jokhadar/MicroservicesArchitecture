using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.UpdateSong;

public class UpdateSongCommandHandler : IRequestHandler<UpdateSongCommand, SongDto>
{
    private readonly ISongRepository _repository;
    private readonly ISongIngestionJobRepository _jobRepository;
    private readonly NasheedFileManagerHelper _fileManagerHelper;
    private readonly ILogger<UpdateSongCommandHandler> _logger;

    public UpdateSongCommandHandler(
        ISongRepository repository,
        ISongIngestionJobRepository jobRepository,
        NasheedFileManagerHelper fileManagerHelper,
        ILogger<UpdateSongCommandHandler> logger)
    {
        _repository = repository;
        _jobRepository = jobRepository;
        _fileManagerHelper = fileManagerHelper;
        _logger = logger;
    }

    public async Task<SongDto> Handle(UpdateSongCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.SongNotFound);

        var titleChanged = !string.IsNullOrWhiteSpace(request.Title) && !string.Equals(request.Title, entity.Title, StringComparison.Ordinal);
        var languageChanged = request.LanguageCode != null && !string.Equals(request.LanguageCode, entity.LanguageCode, StringComparison.Ordinal);
        var lyricsRawChanged = request.LyricsRaw != null && !string.Equals(request.LyricsRaw, entity.LyricsRaw, StringComparison.Ordinal);
        var lyricsVerifiedChanged = request.LyricsVerifiedLrc != null && !string.Equals(request.LyricsVerifiedLrc, entity.LyricsVerifiedLrc, StringComparison.Ordinal);
        var lyricsPlainTextChanged = request.LyricsPlainText != null && !string.Equals(request.LyricsPlainText, entity.LyricsPlainText, StringComparison.Ordinal);
        var summaryChanged = request.Summary != null && !string.Equals(request.Summary, entity.Summary, StringComparison.Ordinal);
        var vocalStyleChanged = request.VocalStyle != null && !string.Equals(request.VocalStyle, entity.VocalStyle, StringComparison.Ordinal);
        var durationChanged = request.DurationSeconds.HasValue && request.DurationSeconds != entity.DurationSeconds;
        var existingRiskLevel = entity.LegalCompliance?.CopyrightRiskLevel;
        var existingSafetyFlag = entity.LegalCompliance?.ContentSafetyFlag;
        var existingRiskReason = entity.LegalCompliance?.RiskReason;

        entity.UpdateTitle(request.Title);
        entity.UpdateMetadata(request.LanguageCode, request.LyricsRaw, request.Summary, request.VocalStyle, request.DurationSeconds);
        entity.UpdateVerifiedLyrics(request.LyricsVerifiedLrc, request.LyricsPlainText);
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

        if (titleChanged || languageChanged || lyricsRawChanged || lyricsVerifiedChanged || lyricsPlainTextChanged || summaryChanged || vocalStyleChanged || durationChanged || legalComplianceChanged)
        {
            await QueueEmbeddingGenerationAsync(entity, cancellationToken);
        }

        _logger.LogInformation("Updated Song Id {Id}", entity.Id);
        var dto = SongDto.MapFrom(entity);
        await _fileManagerHelper.EnrichSongWithFileAsync(dto, cancellationToken);
        return dto;
    }

    private async Task QueueEmbeddingGenerationAsync(SongEntity song, CancellationToken cancellationToken)
    {
        var hasActiveEmbeddingJob = await _jobRepository.HasActiveJobAsync(song.Id, IngestionJobType.EmbeddingGeneration, cancellationToken);
        if (hasActiveEmbeddingJob)
            return;

        try
        {
            var embeddingJob = SongIngestionJobEntity.Create(song.Id, song.FileId, IngestionJobType.EmbeddingGeneration);
            await _jobRepository.AddAsync(embeddingJob, cancellationToken);

            song.SetSearchIndexStatus(SearchIndexStatus.Indexing);
            await _repository.UpdateAsync(song, cancellationToken);

            _logger.LogInformation("Queued embedding job {JobId} for updated song {SongId}.", embeddingJob.Id, song.Id);
        }
        catch (DbUpdateException)
        {
            // Another concurrent request created the same active job — safe to ignore.
        }
    }
}
