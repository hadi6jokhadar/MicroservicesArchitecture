using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for adding a new device token
/// </summary>
public class AddDeviceTokenCommandHandler : IRequestHandler<AddDeviceTokenCommand, DeviceTokenDto>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AddDeviceTokenCommandHandler> _logger;

    public AddDeviceTokenCommandHandler(
        IDeviceTokenRepository deviceTokenRepository,
        IUserRepository userRepository,
        ILogger<AddDeviceTokenCommandHandler> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<DeviceTokenDto> Handle(AddDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Verify user exists
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"User with ID {request.UserId} not found");
        }

        // Check if token already exists
        var existingToken = await _deviceTokenRepository.GetByTokenAsync(request.Token, cancellationToken);
        if (existingToken != null)
        {
            // Update existing token
            existingToken.Platform = request.Platform;
            existingToken.DeviceIdentifier = request.DeviceIdentifier;
            existingToken.IsPrimary = request.IsPrimary;
            existingToken.LastVerifiedAt = DateTime.UtcNow;
            existingToken.LastModified = DateTime.UtcNow;

            await _deviceTokenRepository.UpdateAsync(existingToken, cancellationToken);

            return MapToDto(existingToken);
        }

        // If setting as primary, unset other primary tokens for this user and platform
        if (request.IsPrimary)
        {
            var userTokens = await _deviceTokenRepository.GetByUserIdAndPlatformAsync(
                request.UserId, 
                request.Platform, 
                cancellationToken);

            foreach (var token in userTokens.Where(t => t.IsPrimary))
            {
                token.IsPrimary = false;
                await _deviceTokenRepository.UpdateAsync(token, cancellationToken);
            }
        }

        // Create new token
        var deviceToken = new IhsanDev.Shared.Kernel.Entities.DeviceToken
        {
            UserId = request.UserId,
            Token = request.Token,
            Platform = request.Platform,
            DeviceIdentifier = request.DeviceIdentifier,
            IsPrimary = request.IsPrimary,
            LastVerifiedAt = DateTime.UtcNow,
            Created = DateTime.UtcNow
        };

        var result = await _deviceTokenRepository.AddAsync(deviceToken, cancellationToken);

        return MapToDto(result);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while adding device token for user {UserId}", request.UserId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }

    private static DeviceTokenDto MapToDto(IhsanDev.Shared.Kernel.Entities.DeviceToken deviceToken)
    {
        return new DeviceTokenDto
        {
            Id = deviceToken.Id,
            UserId = deviceToken.UserId,
            Token = deviceToken.Token,
            Platform = deviceToken.Platform,
            DeviceIdentifier = deviceToken.DeviceIdentifier,
            LastVerifiedAt = deviceToken.LastVerifiedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            IsPrimary = deviceToken.IsPrimary,
            Created = deviceToken.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
