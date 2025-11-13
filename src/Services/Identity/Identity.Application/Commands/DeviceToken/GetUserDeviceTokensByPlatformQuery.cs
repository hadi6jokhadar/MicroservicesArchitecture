using IhsanDev.Shared.Kernel.Dto;
using IhsanDev.Shared.Kernel.Enums;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Query to get device tokens by user ID and platform
/// </summary>
public record GetUserDeviceTokensByPlatformQuery(int UserId, Platform Platform) : IRequest<List<DeviceTokenDto>>;
