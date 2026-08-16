using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.RemoveAllFailedIngestionJobs;

public class RemoveAllFailedIngestionJobsCommandHandler
    : IRequestHandler<RemoveAllFailedIngestionJobsCommand, RemoveAllFailedIngestionJobsResultDto>
{
    private readonly ISongIngestionJobRepository _repository;
    private readonly ILogger<RemoveAllFailedIngestionJobsCommandHandler> _logger;

    public RemoveAllFailedIngestionJobsCommandHandler(
        ISongIngestionJobRepository repository,
        ILogger<RemoveAllFailedIngestionJobsCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RemoveAllFailedIngestionJobsResultDto> Handle(
        RemoveAllFailedIngestionJobsCommand request,
        CancellationToken cancellationToken)
    {
        var failedJobs = await _repository.GetByStatusAsync(IngestionJobStatus.Failed, cancellationToken);

        foreach (var job in failedJobs)
        {
            await _repository.HardDeleteAsync(job, cancellationToken);
        }

        _logger.LogInformation("Bulk remove: hard deleted {Count} failed ingestion job(s)", failedJobs.Count);

        return new RemoveAllFailedIngestionJobsResultDto(failedJobs.Count);
    }
}
