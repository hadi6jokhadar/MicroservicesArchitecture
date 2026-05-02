using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.RemoveIngestionJob;

public class RemoveIngestionJobCommandHandler : IRequestHandler<RemoveIngestionJobCommand, bool>
{
    private readonly ISongIngestionJobRepository _repository;
    private readonly ILogger<RemoveIngestionJobCommandHandler> _logger;

    public RemoveIngestionJobCommandHandler(ISongIngestionJobRepository repository, ILogger<RemoveIngestionJobCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(RemoveIngestionJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new NotFoundException($"Ingestion job with Id '{request.JobId}' not found.");

        job.MarkRemoved();
        await _repository.UpdateAsync(job, cancellationToken);
        _logger.LogInformation("Marked ingestion job Id {JobId} as removed", job.Id);
        return true;
    }
}
