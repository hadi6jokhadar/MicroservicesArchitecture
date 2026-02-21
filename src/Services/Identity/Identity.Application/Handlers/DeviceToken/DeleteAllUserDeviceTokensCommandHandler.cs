using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for deleting all device tokens for a user
/// </summary>
public class DeleteAllUserDeviceTokensCommandHandler : IRequestHandler<DeleteAllUserDeviceTokensCommand, bool>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DeleteAllUserDeviceTokensCommandHandler> _logger;

    public DeleteAllUserDeviceTokensCommandHandler(
        IDeviceTokenRepository deviceTokenRepository,
        IUserRepository userRepository,
        ILogger<DeleteAllUserDeviceTokensCommandHandler> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteAllUserDeviceTokensCommand request, CancellationToken cancellationToken)
    {
        try
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
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting all device tokens for user {UserId}", request.UserId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
