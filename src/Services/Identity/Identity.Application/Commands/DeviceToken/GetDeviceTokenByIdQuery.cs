using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Query to get a device token by ID
/// </summary>
public record GetDeviceTokenByIdQuery(int Id) : IRequest<DeviceTokenDto?>;
