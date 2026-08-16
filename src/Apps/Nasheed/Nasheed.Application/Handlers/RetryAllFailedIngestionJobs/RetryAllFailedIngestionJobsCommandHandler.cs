using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.RetryAllFailedIngestionJobs;

public class RetryAllFailedIngestionJobsCommandHandler
    : IRequestHandler<RetryAllFailedIngestionJobsCommand, RetryAllFailedIngestionJobsResultDto>
{
    private readonly ISongIngestionJobRepository _jobRepository;
    private readonly ISongRepository _songRepository;
    private readonly ILogger<RetryAllFailedIngestionJobsCommandHandler> _logger;

    public RetryAllFailedIngestionJobsCommandHandler(
        ISongIngestionJobRepository jobRepository,
        ISongRepository songRepository,
        ILogger<RetryAllFailedIngestionJobsCommandHandler> logger)
    {
        _jobRepository = jobRepository;
        _songRepository = songRepository;
        _logger = logger;
    }

    public async Task<RetryAllFailedIngestionJobsResultDto> Handle(
        RetryAllFailedIngestionJobsCommand request,
        CancellationToken cancellationToken)
    {
        var failedJobs = await _jobRepository.GetByStatusAsync(IngestionJobStatus.Failed, cancellationToken);

        var retriedCount = 0;
        var skippedCount = 0;

        foreach (var job in failedJobs)
        {
            // A song can have more than one historical Failed job of the same (SongId, JobType) —
            // once an earlier job in this same batch is reset back to Pending/Running, skip any
            // later one for the same song+type instead of racing the DB's active-job unique index.
            if (await _jobRepository.HasActiveJobAsync(job.SongId, job.JobType, cancellationToken, excludeJobId: job.Id))
            {
                skippedCount++;
                continue;
            }

            job.ResetForRetry();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            if (job.JobType == IngestionJobType.FullPipeline)
            {
                var song = await _songRepository.GetByIdAsync(job.SongId, cancellationToken);
                if (song is not null)
                {
                    song.SetState(SongState.InQueue);
                    await _songRepository.UpdateAsync(song, cancellationToken);
                }
            }

            retriedCount++;
        }

        _logger.LogInformation(
            "Bulk retry: reset {RetriedCount} failed ingestion job(s) for retry, skipped {SkippedCount} (already active)",
            retriedCount, skippedCount);

        return new RetryAllFailedIngestionJobsResultDto(retriedCount, skippedCount);
    }
}
