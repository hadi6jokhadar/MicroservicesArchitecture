using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Query to get a device token by token string
/// </summary>
public record GetDeviceTokenByTokenQuery(string Token) : IRequest<DeviceTokenDto?>;
