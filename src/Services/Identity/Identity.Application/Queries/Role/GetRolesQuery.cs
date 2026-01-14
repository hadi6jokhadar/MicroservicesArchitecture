using IhsanDev.Shared.Application.Localization;
using MediatR;
using Identity.Application.DTOs;

namespace Identity.Application.Queries.Role;

public record GetRolesQuery : IRequest<List<RoleDto>>;

public record GetRoleByIdQuery(int Id) : IRequest<RoleDto>;
