using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for getting all device tokens for current tenant (tenant-wide notifications)
/// </summary>
public class GetTenantDeviceTokensQueryHandler : IRequestHandler<GetTenantDeviceTokensQuery, List<DeviceTokenDto>>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;

    public GetTenantDeviceTokensQueryHandler(IDeviceTokenRepository deviceTokenRepository)
    {
        _deviceTokenRepository = deviceTokenRepository;
    }

    public async Task<List<DeviceTokenDto>> Handle(GetTenantDeviceTokensQuery request, CancellationToken cancellationToken)
    {
        var deviceTokens = await _deviceTokenRepository.GetAllForCurrentTenantAsync(cancellationToken);

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
            LastVerifiedAt = deviceToken.LastVerifiedAt,
            IsPrimary = deviceToken.IsPrimary,
            Created = deviceToken.Created
        };
    }
}
