using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Command to update an existing device token
/// </summary>
public record UpdateDeviceTokenCommand(
    int Id,
    string? Token = null,
    string? DeviceIdentifier = null,
    bool? IsPrimary = null
) : IRequest<DeviceTokenDto>;
