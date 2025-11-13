using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for getting a device token by ID
/// </summary>
public class GetDeviceTokenByIdQueryHandler : IRequestHandler<GetDeviceTokenByIdQuery, DeviceTokenDto?>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;

    public GetDeviceTokenByIdQueryHandler(IDeviceTokenRepository deviceTokenRepository)
    {
        _deviceTokenRepository = deviceTokenRepository;
    }

    public async Task<DeviceTokenDto?> Handle(GetDeviceTokenByIdQuery request, CancellationToken cancellationToken)
    {
        var deviceToken = await _deviceTokenRepository.GetByIdAsync(request.Id, cancellationToken);
        if (deviceToken == null)
        {
            return null;
        }

        return MapToDto(deviceToken);
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
            LastVerifiedAt = deviceToken.LastVerifiedAt,
            IsPrimary = deviceToken.IsPrimary,
            Created = deviceToken.Created
        };
    }
}
