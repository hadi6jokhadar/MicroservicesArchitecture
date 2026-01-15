using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using Identity.Application.Queries.Role;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Admin.Role;

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, List<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ICacheService _cacheService;

    public GetRolesQueryHandler(
        IRoleRepository roleRepository,
        ICacheService cacheService)
    {
        _roleRepository = roleRepository;
        _cacheService = cacheService;
    }

    public async Task<List<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Try group cache first
            const string groupKey = "admin:roles";
            var cachedRoles = await _cacheService.GetAsync<List<RoleDto>>(groupKey, cancellationToken);
            if (cachedRoles != null)
                return cachedRoles;

            // Cache miss - fetch from database
            var roles = await _roleRepository.GetAllAsync(false, cancellationToken);
            var roleDtos = roles.Select(RoleDto.MapFrom).ToList();

            // Cache under group for 30 minutes
            await _cacheService.SetAsync(groupKey, roleDtos, TimeSpan.FromMinutes(30), cancellationToken);

            return roleDtos;
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}

public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ICacheService _cacheService;

    public GetRoleByIdQueryHandler(
        IRoleRepository roleRepository,
        ICacheService cacheService)
    {
        _roleRepository = roleRepository;
        _cacheService = cacheService;
    }

    public async Task<RoleDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Try group cache first
            const string groupKey = "admin:roles";
            var cacheKey = $"{groupKey}:{request.Id}";
            var cachedRole = await _cacheService.GetAsync<RoleDto>(cacheKey, cancellationToken);
            if (cachedRole != null)
                return cachedRole;

            // Cache miss - fetch from database
            var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);
            if (role == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.RoleNotFound);

            var roleDto = RoleDto.MapFrom(role);

            // Cache under group for 30 minutes
            await _cacheService.SetAsync(cacheKey, roleDto, TimeSpan.FromMinutes(30), cancellationToken);

            return roleDto;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
