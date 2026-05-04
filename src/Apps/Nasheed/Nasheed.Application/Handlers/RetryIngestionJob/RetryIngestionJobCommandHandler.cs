using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.RetryIngestionJob;

public class RetryIngestionJobCommandHandler : IRequestHandler<RetryIngestionJobCommand, IngestionJobDto>
{
    private readonly ISongIngestionJobRepository _repository;
    private readonly ILogger<RetryIngestionJobCommandHandler> _logger;

    public RetryIngestionJobCommandHandler(ISongIngestionJobRepository repository, ILogger<RetryIngestionJobCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IngestionJobDto> Handle(RetryIngestionJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.IngestionJobNotFound);

        job.ResetForRetry();
        await _repository.UpdateAsync(job, cancellationToken);
        _logger.LogInformation("Reset ingestion job Id {JobId} for retry", job.Id);
        return IngestionJobDto.MapFrom(job);
    }
}
