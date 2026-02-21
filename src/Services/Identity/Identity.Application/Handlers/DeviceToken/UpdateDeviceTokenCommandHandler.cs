using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for updating a device token
/// </summary>
public class UpdateDeviceTokenCommandHandler : IRequestHandler<UpdateDeviceTokenCommand, DeviceTokenDto>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ILogger<UpdateDeviceTokenCommandHandler> _logger;

    public UpdateDeviceTokenCommandHandler(
        IDeviceTokenRepository deviceTokenRepository,
        ILogger<UpdateDeviceTokenCommandHandler> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _logger = logger;
    }

    public async Task<DeviceTokenDto> Handle(UpdateDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var deviceToken = await _deviceTokenRepository.GetByIdAsync(request.Id, cancellationToken);
        if (deviceToken == null)
        {
            throw new NotFoundException($"Device token with ID {request.Id} not found");
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            deviceToken.Token = request.Token;
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceIdentifier))
        {
            deviceToken.DeviceIdentifier = request.DeviceIdentifier;
        }

        if (request.IsPrimary.HasValue)
        {
            // If setting as primary, unset other primary tokens for this user and platform
            if (request.IsPrimary.Value)
            {
                var userTokens = await _deviceTokenRepository.GetByUserIdAndPlatformAsync(
                    deviceToken.UserId,
                    deviceToken.Platform,
                    cancellationToken);

                foreach (var token in userTokens.Where(t => t.IsPrimary && t.Id != deviceToken.Id))
                {
                    token.IsPrimary = false;
                    await _deviceTokenRepository.UpdateAsync(token, cancellationToken);
                }
            }

            deviceToken.IsPrimary = request.IsPrimary.Value;
        }

        deviceToken.LastVerifiedAt = DateTime.UtcNow;

        await _deviceTokenRepository.UpdateAsync(deviceToken, cancellationToken);

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
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating device token {TokenId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
