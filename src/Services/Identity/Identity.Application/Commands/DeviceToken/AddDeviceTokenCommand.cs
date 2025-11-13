using IhsanDev.Shared.Kernel.Dto;
using IhsanDev.Shared.Kernel.Enums;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Command to add a new device token
/// </summary>
public record AddDeviceTokenCommand(
    int UserId,
    string Token,
    Platform Platform,
    string? DeviceIdentifier = null,
    bool IsPrimary = false
) : IRequest<DeviceTokenDto>;
