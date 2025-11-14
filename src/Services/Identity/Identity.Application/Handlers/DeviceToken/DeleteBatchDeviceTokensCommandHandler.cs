using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for deleting multiple device tokens in a single batch
/// </summary>
public class DeleteBatchDeviceTokensCommandHandler : IRequestHandler<DeleteBatchDeviceTokensCommand, int>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ILogger<DeleteBatchDeviceTokensCommandHandler> _logger;

    public DeleteBatchDeviceTokensCommandHandler(
        IDeviceTokenRepository deviceTokenRepository,
        ILogger<DeleteBatchDeviceTokensCommandHandler> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _logger = logger;
    }

    public async Task<int> Handle(DeleteBatchDeviceTokensCommand request, CancellationToken cancellationToken)
    {
        if (!request.TokenIds.Any())
        {
            return 0;
        }

        var deletedCount = 0;

        // Delete all tokens in parallel
        var tasks = request.TokenIds.Select(async tokenId =>
        {
            try
            {
                var deviceToken = await _deviceTokenRepository.GetByIdAsync(tokenId, cancellationToken);
                if (deviceToken != null)
                {
                    var deleted = await _deviceTokenRepository.DeleteAsync(deviceToken, cancellationToken);
                    return deleted ? 1 : 0;
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete device token {TokenId}", tokenId);
                return 0;
            }
        });

        var results = await Task.WhenAll(tasks);
        deletedCount = results.Sum();

        _logger.LogInformation(
            "Batch deleted {DeletedCount} of {TotalCount} device tokens",
            deletedCount,
            request.TokenIds.Count);

        return deletedCount;
    }
}
