using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Query to get all device tokens for a user
/// </summary>
public record GetUserDeviceTokensQuery(int UserId) : IRequest<List<DeviceTokenDto>>;
