using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
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
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.IngestionJobNotFound);

        await _repository.HardDeleteAsync(job, cancellationToken);
        _logger.LogInformation("Hard deleted ingestion job Id {JobId}", job.Id);
        return true;
    }
}
