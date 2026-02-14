using MediatR;
using Tenant.Application.DTOs;

namespace Tenant.Application.Commands.Tenant;

/// <summary>
/// Command to toggle tenant archived status
/// </summary>
public record ToggleTenantArchivedStatusCommand(int TenantId) : IRequest<TenantDto>;
