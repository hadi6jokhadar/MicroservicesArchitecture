using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Get device tokens for multiple users in a single batch request
/// </summary>
public record GetBatchDeviceTokensQuery(List<int> UserIds) 
    : IRequest<Dictionary<int, List<DeviceTokenDto>>>;
