using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.RetryIngestionJob;

public class RetryIngestionJobCommandHandler : IRequestHandler<RetryIngestionJobCommand, IngestionJobDto>
{
    private readonly ISongIngestionJobRepository _repository;
    private readonly ISongRepository _songRepository;
    private readonly ILogger<RetryIngestionJobCommandHandler> _logger;

    public RetryIngestionJobCommandHandler(
        ISongIngestionJobRepository repository,
        ISongRepository songRepository,
        ILogger<RetryIngestionJobCommandHandler> logger)
    {
        _repository = repository;
        _songRepository = songRepository;
        _logger = logger;
    }

    public async Task<IngestionJobDto> Handle(RetryIngestionJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.IngestionJobNotFound);

        // Guards against retrying the same song's full pipeline twice in parallel — the DB's
        // partial unique index (SongId, JobType) already blocks the actual insert/update race,
        // but this turns it into a clean 409 instead of an unhandled DbUpdateException.
        if (await _repository.HasActiveJobAsync(job.SongId, job.JobType, cancellationToken, excludeJobId: job.Id))
        {
            throw new ConflictException(LocalizationKeys.Exceptions.IngestionJobAlreadyActive);
        }

        job.ResetForRetry();
        await _repository.UpdateAsync(job, cancellationToken);

        if (job.JobType == IngestionJobType.FullPipeline)
        {
            var song = await _songRepository.GetByIdAsync(job.SongId, cancellationToken);
            if (song is not null)
            {
                song.SetState(SongState.InQueue);
                await _songRepository.UpdateAsync(song, cancellationToken);
            }
        }

        _logger.LogInformation("Reset ingestion job Id {JobId} for retry", job.Id);
        return IngestionJobDto.MapFrom(job);
    }
}
