using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using MediatR;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for deleting all device tokens for a user
/// </summary>
public class DeleteAllUserDeviceTokensCommandHandler : IRequestHandler<DeleteAllUserDeviceTokensCommand, bool>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly IUserRepository _userRepository;

    public DeleteAllUserDeviceTokensCommandHandler(
        IDeviceTokenRepository deviceTokenRepository,
        IUserRepository userRepository)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _userRepository = userRepository;
    }

    public async Task<bool> Handle(DeleteAllUserDeviceTokensCommand request, CancellationToken cancellationToken)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"User with ID {request.UserId} not found");
        }

        await _deviceTokenRepository.DeleteByUserIdAsync(request.UserId, cancellationToken);
        return true;
    }
}
