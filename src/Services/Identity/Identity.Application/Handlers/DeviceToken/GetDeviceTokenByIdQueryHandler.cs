using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for getting a device token by ID
/// </summary>
public class GetDeviceTokenByIdQueryHandler : IRequestHandler<GetDeviceTokenByIdQuery, DeviceTokenDto?>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ILogger<GetDeviceTokenByIdQueryHandler> _logger;

    public GetDeviceTokenByIdQueryHandler(
        IDeviceTokenRepository deviceTokenRepository,
        ILogger<GetDeviceTokenByIdQueryHandler> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _logger = logger;
    }

    public async Task<DeviceTokenDto?> Handle(GetDeviceTokenByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var deviceToken = await _deviceTokenRepository.GetByIdAsync(request.Id, cancellationToken);
        if (deviceToken == null)
        {
            return null;
        }

        return MapToDto(deviceToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting device token {TokenId}", request.Id);
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
