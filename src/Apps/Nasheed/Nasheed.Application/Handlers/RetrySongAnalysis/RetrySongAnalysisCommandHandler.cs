using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.RetrySongAnalysis;

public class RetrySongAnalysisCommandHandler : IRequestHandler<RetrySongAnalysisCommand, SongDto>
{
    private readonly ISongRepository _songRepository;
    private readonly ISongIngestionJobRepository _jobRepository;
    private readonly NasheedFileManagerHelper _fileManagerHelper;
    private readonly ILogger<RetrySongAnalysisCommandHandler> _logger;

    public RetrySongAnalysisCommandHandler(
        ISongRepository songRepository,
        ISongIngestionJobRepository jobRepository,
        NasheedFileManagerHelper fileManagerHelper,
        ILogger<RetrySongAnalysisCommandHandler> logger)
    {
        _songRepository = songRepository;
        _jobRepository = jobRepository;
        _fileManagerHelper = fileManagerHelper;
        _logger = logger;
    }

    public async Task<SongDto> Handle(RetrySongAnalysisCommand request, CancellationToken cancellationToken)
    {
        var song = await _songRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.SongNotFound);

        song.SetLyricsVerified(false);
        song.SetState(SongState.InQueue);

        if (await _jobRepository.HasActiveJobAsync(song.Id, IngestionJobType.FullPipeline, cancellationToken))
        {
            _logger.LogInformation("Full-pipeline job already active for Song {SongId}; skipping duplicate job creation", song.Id);
        }
        else
        {
            var job = SongIngestionJobEntity.Create(song.Id, song.FileId, IngestionJobType.FullPipeline);
            await _jobRepository.AddAsync(job, cancellationToken);
            _logger.LogInformation("Queued full-pipeline retry job {JobId} for Song {SongId}", job.Id, song.Id);
        }

        await _songRepository.UpdateAsync(song, cancellationToken);

        var dto = SongDto.MapFrom(song, song.MoodTags?.Select(t => t.Tag).ToList() ?? []);
        await _fileManagerHelper.EnrichSongWithFileAsync(dto, cancellationToken);
        return dto;
    }
}
