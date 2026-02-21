using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for getting device tokens by user ID and platform
/// </summary>
public class GetUserDeviceTokensByPlatformQueryHandler : IRequestHandler<GetUserDeviceTokensByPlatformQuery, List<DeviceTokenDto>>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ILogger<GetUserDeviceTokensByPlatformQueryHandler> _logger;

    public GetUserDeviceTokensByPlatformQueryHandler(
        IDeviceTokenRepository deviceTokenRepository,
        ILogger<GetUserDeviceTokensByPlatformQueryHandler> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _logger = logger;
    }

    public async Task<List<DeviceTokenDto>> Handle(GetUserDeviceTokensByPlatformQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var deviceTokens = await _deviceTokenRepository.GetByUserIdAndPlatformAsync(
            request.UserId,
            request.Platform,
            cancellationToken);

        return deviceTokens.Select(MapToDto).ToList();
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting device tokens by platform for user {UserId}", request.UserId);
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
