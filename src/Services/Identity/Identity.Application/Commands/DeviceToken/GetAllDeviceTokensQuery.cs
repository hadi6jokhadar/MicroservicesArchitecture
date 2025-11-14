using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Get all device tokens (for global notifications)
/// </summary>
public record GetAllDeviceTokensQuery() : IRequest<List<DeviceTokenDto>>;
