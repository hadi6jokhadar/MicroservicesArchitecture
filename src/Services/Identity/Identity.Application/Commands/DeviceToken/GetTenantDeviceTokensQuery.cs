using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Get all device tokens for current tenant (for tenant-wide notifications)
/// </summary>
public record GetTenantDeviceTokensQuery() : IRequest<List<DeviceTokenDto>>;
