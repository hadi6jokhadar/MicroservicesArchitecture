using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for getting all device tokens for a user
/// </summary>
public class GetUserDeviceTokensQueryHandler : IRequestHandler<GetUserDeviceTokensQuery, List<DeviceTokenDto>>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;

    public GetUserDeviceTokensQueryHandler(IDeviceTokenRepository deviceTokenRepository)
    {
        _deviceTokenRepository = deviceTokenRepository;
    }

    public async Task<List<DeviceTokenDto>> Handle(GetUserDeviceTokensQuery request, CancellationToken cancellationToken)
    {
        var deviceTokens = await _deviceTokenRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return deviceTokens.Select(MapToDto).ToList();
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
