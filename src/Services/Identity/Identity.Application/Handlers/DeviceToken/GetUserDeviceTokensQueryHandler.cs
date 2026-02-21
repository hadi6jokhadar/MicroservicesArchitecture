using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for getting all device tokens for a user
/// </summary>
public class GetUserDeviceTokensQueryHandler : IRequestHandler<GetUserDeviceTokensQuery, List<DeviceTokenDto>>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ILogger<GetUserDeviceTokensQueryHandler> _logger;

    public GetUserDeviceTokensQueryHandler(
        IDeviceTokenRepository deviceTokenRepository,
        ILogger<GetUserDeviceTokensQueryHandler> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _logger = logger;
    }

    public async Task<List<DeviceTokenDto>> Handle(GetUserDeviceTokensQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var deviceTokens = await _deviceTokenRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return deviceTokens.Select(MapToDto).ToList();
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting device tokens for user {UserId}", request.UserId);
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
