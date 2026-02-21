using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for deleting a device token
/// </summary>
public class DeleteDeviceTokenCommandHandler : IRequestHandler<DeleteDeviceTokenCommand, bool>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ILogger<DeleteDeviceTokenCommandHandler> _logger;

    public DeleteDeviceTokenCommandHandler(
        IDeviceTokenRepository deviceTokenRepository,
        ILogger<DeleteDeviceTokenCommandHandler> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var deviceToken = await _deviceTokenRepository.GetByIdAsync(request.Id, cancellationToken);
        if (deviceToken == null)
        {
            throw new NotFoundException($"Device token with ID {request.Id} not found");
        }

        await _deviceTokenRepository.DeleteAsync(deviceToken, cancellationToken);
        return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting device token {TokenId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
