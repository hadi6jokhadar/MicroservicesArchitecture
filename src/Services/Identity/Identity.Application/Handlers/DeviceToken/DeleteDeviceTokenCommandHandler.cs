using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using MediatR;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for deleting a device token
/// </summary>
public class DeleteDeviceTokenCommandHandler : IRequestHandler<DeleteDeviceTokenCommand, bool>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;

    public DeleteDeviceTokenCommandHandler(IDeviceTokenRepository deviceTokenRepository)
    {
        _deviceTokenRepository = deviceTokenRepository;
    }

    public async Task<bool> Handle(DeleteDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        var deviceToken = await _deviceTokenRepository.GetByIdAsync(request.Id, cancellationToken);
        if (deviceToken == null)
        {
            throw new NotFoundException($"Device token with ID {request.Id} not found");
        }

        await _deviceTokenRepository.DeleteAsync(deviceToken, cancellationToken);
        return true;
    }
}
